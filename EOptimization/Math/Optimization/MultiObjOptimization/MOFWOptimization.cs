// This is an open source non-commercial project. Dear PVS-Studio, please check it. PVS-Studio Static
// Code Analyzer for C, C++ and C#: http://www.viva64.com
namespace EOpt.Math.Optimization.MOOpt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using Help;

    using Math;
    using Math.Optimization;
    using Math.Random;

    using Nds;

    /// <summary>
    /// Optimization method Fireworks. 
    /// </summary>
    public class MOFWOptimizer : BaseFW<IEnumerable<double>, IMOOptProblem>, IMOOptimizer<FWParams>
    {
        private PointND _idealPoint, _nadirPoint;

        private Ndsort<double> _nds;

        private void EvalFunctionForCharges(Func<IReadOnlyList<double>, IEnumerable<double>> Function)
        {
            for (int i = 0; i < _parameters.NP; i++)
            {
                _chargePoints[i].Eval(Function);
            }
        }

        private void EvalFunctionForDebris(Func<IReadOnlyList<double>, IEnumerable<double>> Function)
        {
            for (int i = 0; i < _debris.Length; i++)
            {
                foreach (var splinter in _debris[i])
                {
                    splinter.Eval(Function);
                }
            }
        }

        /// <summary>
        /// Find amount debris for each point of charge. 
        /// </summary>
        private void FindAmountDebris()
        {
            double s = 0;

            double denumerator = 0;

            int dimObjs = _chargePoints[0].Objs.Count;

            for (int i = 0; i < _parameters.NP; i++)
            {
                denumerator += PointND.Distance(_nadirPoint, _chargePoints[i].Objs);
            }

            if (denumerator < Constants.VALUE_AVOID_DIV_BY_ZERO)
            {
                denumerator += Constants.VALUE_AVOID_DIV_BY_ZERO;
            }

            for (int i = 0; i < _parameters.NP; i++)
            {
                s = _parameters.M * (PointND.Distance(_nadirPoint, _chargePoints[i].Objs) + Constants.VALUE_AVOID_DIV_BY_ZERO) / denumerator;

                base.FindAmountDebrisForCharge(s, i, dimObjs);
            }
        }

        /// <summary>
        /// Determine debris position. 
        /// </summary>
        /// <param name="ChargeFronts"></param>
        /// <param name="LowerBounds"> </param>
        /// <param name="UpperBounds"> </param>
        /// <param name="Function">    </param>
        private void GenerateDebris(IReadOnlyList<double> LowerBounds, IReadOnlyList<double> UpperBounds, Func<IReadOnlyList<double>, IEnumerable<double>> Function)
        {
            double denumerator = 0;

            for (int j = 0; j < _parameters.NP; j++)
            {
                denumerator += PointND.Distance(_chargePoints[j].Objs, _idealPoint);
            }

            if (denumerator < Constants.VALUE_AVOID_DIV_BY_ZERO)
            {
                denumerator += Constants.VALUE_AVOID_DIV_BY_ZERO;
            }

            for (int i = 0; i < _parameters.NP; i++)
            {
                double amplitude = 0;

                // Amplitude of explosion.
                amplitude = _parameters.Amax * (PointND.Distance(_chargePoints[i].Objs, _idealPoint) + Constants.VALUE_AVOID_DIV_BY_ZERO) / denumerator;

                base.GenerateDebrisForCharge(LowerBounds, UpperBounds, amplitude, i);
            }
        }

        /// <summary>
        /// Generate current population. 
        /// </summary>
        private void GenerateNextAgents(IEnumerable<KeyValuePair<int, Agent>> ChargesAndDebris)
        {
            int firstFrontCount = ChargesAndDebris.Count(kvalue => kvalue.Key == 0);

            int actualSizeMatrix = 0;

            if (firstFrontCount > _parameters.NP)
            {
                actualSizeMatrix = firstFrontCount;

                base.ResetMatrixAndTrimWeights(actualSizeMatrix);

                int index = 0;

                foreach (var agent in ChargesAndDebris.Where(kvalue => kvalue.Key == 0).Select(kvalue => kvalue.Value))
                {
                    _weightedAgents[index++].Agent.SetAt(agent);
                }
            }
            else
            {
                // The total count minus non-dominated solutions.
                actualSizeMatrix = _parameters.NP - firstFrontCount;

                for (int k = 0; k < _debris.Length; k++)
                {
                    actualSizeMatrix += _debris[k].Count;
                }

                base.ResetMatrixAndTrimWeights(actualSizeMatrix);

                int index = 0;

                foreach (var frontAgent in ChargesAndDebris)
                {
                    // Solutions from all fronts except zero front are taken.
                    if (frontAgent.Key != 0)
                    {
                        _weightedAgents[index++].Agent.SetAt(frontAgent.Value);
                    }
                }
            }


            base.CalculateDistances();

           
            int startIndex = 0;

            int totalToTake = 0;

            if (firstFrontCount > _parameters.NP)
            {
                totalToTake = _parameters.NP;
            }
            else
            {
                foreach (var agent in ChargesAndDebris.Where(kvalue => kvalue.Key == 0).Select(kvalue => kvalue.Value))
                {
                    _chargePoints[startIndex++].SetAt(agent);
                }

                totalToTake = _parameters.NP - firstFrontCount;
            }


            if (totalToTake > 0)
            {
                base.TakeAgents(actualSizeMatrix, totalToTake);
            }

            for (int i = 0; i < _weightedAgents.Count; i++)
            {
                if (_weightedAgents[i].IsTake)
                {
                    _chargePoints[startIndex++].SetAt(_weightedAgents[i].Agent);
                }
            }
        }

        protected override void Clear()
        {

        }

        protected override void FirstStep(IMOOptProblem Problem)
        {
            if (Problem == null)
            {
                throw new ArgumentNullException(nameof(Problem));
            }

            InitAgents(Problem.LowerBounds, Problem.UpperBounds, Problem.CountObjs);

            EvalFunctionForCharges(Problem.TargetFunction);
        }

        protected override void NextStep(IMOOptProblem Problem)
        {
            FindIdealAndNadirPoint();

            FindAmountDebris();

            GenerateDebris(Problem.LowerBounds, Problem.UpperBounds, Problem.TargetFunction);

            EvalFunctionForDebris(Problem.TargetFunction);

            var allAgents = _chargePoints.Concat(_debris.SelectMany(coll => coll.Select(agent => agent)));

            int[] allFronts = _nds.NonDominSort(allAgents, item => item.Objs);

            GenerateNextAgents(allAgents.Zip(allFronts, (agent, front) => new KeyValuePair<int, Agent>(front, agent)));

            EvalFunctionForCharges(Problem.TargetFunction);
        }

        protected override void Init(FWParams Parameters, int Dim, int DimObjs)
        {
            base.Init(Parameters, Dim, DimObjs);

            if(_idealPoint == null)
            {
                _idealPoint = new PointND(0.0, DimObjs);
            }
            else if(_idealPoint.Count != DimObjs)
            {
                _idealPoint = new PointND(0.0, DimObjs);
            }

            if (_nadirPoint == null)
            {
                _nadirPoint = new PointND(0.0, DimObjs);
            }
            else if (_nadirPoint.Count != DimObjs)
            {
                _nadirPoint = new PointND(0.0, DimObjs);
            }
        }

        private void FindIdealAndNadirPoint()
        {
            _idealPoint.SetAt(_chargePoints[0].Objs);
            _nadirPoint.SetAt(_chargePoints[0].Objs);

            for (int i = 1; i < _parameters.NP; i++)
            {
                for (int j = 0; j < _chargePoints[i].Objs.Count; j++)
                {
                    if(_chargePoints[i].Objs[j] < _idealPoint[j])
                    {
                        _idealPoint[j] = _chargePoints[i].Objs[j];
                    }
                    else if (_chargePoints[i].Objs[j] > _nadirPoint[j])
                    {
                        _nadirPoint[j] = _chargePoints[i].Objs[j];
                    }
                }
            }
        }

        public IEnumerable<Agent> ParetoFront
        {
            get => _nds.NonDominSort(_chargePoints, ag => ag.Objs).Zip(_chargePoints, (front, agent) => new KeyValuePair<int, Agent>(front, agent)).Where(kv => kv.Key == 0).Select(kv => kv.Value);

        }

        /// <summary>
        /// Create object which uses default implementation for random generators. 
        /// </summary>
        public MOFWOptimizer() : this(new ContUniformDist(), new NormalDist())
        {
        }

        /// <summary>
        /// Create object which uses custom implementation for random generators. 
        /// </summary>
        /// <param name="UniformGen">
        /// Object, which implements <see cref="IContUniformGen"/> interface.
        /// </param>
        /// <param name="NormalGen">  Object, which implements <see cref="INormalGen"/> interface. </param>
        /// <exception cref="ArgumentNullException">
        /// If <paramref name="NormalGen"/> or <paramref name="UniformGen"/> is null.
        /// </exception>
        public MOFWOptimizer(IContUniformGen UniformGen, INormalGen NormalGen) : base(UniformGen, NormalGen)
        {
            _nds = new Ndsort<double>(CmpDouble.DoubleCompare);
        }

        /// <summary>
        /// <see cref="IOOOptimizer{T}.Minimize(GeneralParams)"/> 
        /// </summary>
        /// <param name="GenParams"> General parameters. <see cref="GeneralParams"/>. </param>
        /// <exception cref="InvalidOperationException"> If parameters do not set. </exception>
        /// <exception cref="ArgumentNullException"> If <paramref name="GenParams"/> is null. </exception>
        /// <exception cref="ArithmeticException">
        /// If the function has value is NaN, PositiveInfinity or NegativeInfinity.
        /// </exception>
        public override void Minimize(FWParams Parameters, IMOOptProblem Problem)
        {
            Init(Parameters, Problem.LowerBounds.Count, Problem.CountObjs);

            FirstStep(Problem);

            for (int i = 1; i < _parameters.Imax; i++)
            {
                NextStep(Problem);
            }

            Clear();
        }

        /// <summary>
        /// <see cref="IOOOptimizer{T}.Minimize(GeneralParams, CancellationToken)"/> 
        /// </summary>
        /// <param name="GenParams">   General parameters. <see cref="GeneralParams"/>. </param>
        /// <param name="CancelToken"> <see cref="CancellationToken"/> </param>
        /// <exception cref="InvalidOperationException"> If parameters do not set. </exception>
        /// <exception cref="ArgumentNullException"> If <paramref name="GenParams"/> is null. </exception>
        /// <exception cref="ArithmeticException">
        /// If the function has value is NaN, PositiveInfinity or NegativeInfinity.
        /// </exception>
        /// <exception cref="OperationCanceledException"></exception>
        public override void Minimize(FWParams Parameters, IMOOptProblem Problem, CancellationToken CancelToken)
        {
            Init(Parameters, Problem.LowerBounds.Count, Problem.CountObjs);

            FirstStep(Problem);

            for (int i = 1; i < this._parameters.Imax; i++)
            {
                CancelToken.ThrowIfCancellationRequested();
                NextStep(Problem);
            }

            Clear();
        }

        /// <summary>
        /// <see cref="IOOOptimizer{T}.Minimize(GeneralParams, IProgress{Progress})"/> 
        /// </summary>
        /// <param name="GenParams"> General parameters. <see cref="GeneralParams"/>. </param>
        /// <param name="Reporter"> 
        /// Object which implement interface <see cref="IProgress{T}"/>, where T is
        /// <see cref="Progress"/>. <seealso cref="IOOOptimizer{T}.Minimize(GeneralParams, IProgress{Progress})"/>
        /// </param>
        /// <exception cref="InvalidOperationException"> If parameters do not set. </exception>
        /// <exception cref="ArgumentNullException">
        /// If <paramref name="GenParams"/> or <paramref name="Reporter"/> is null.
        /// </exception>
        /// <exception cref="ArithmeticException">
        /// If the function has value is NaN, PositiveInfinity or NegativeInfinity.
        /// </exception>
        public override void Minimize(FWParams Parameters, IMOOptProblem Problem, IProgress<Progress> Reporter)
        {
            if (Reporter == null)
            {
                throw new ArgumentNullException(nameof(Reporter));
            }

            Init(Parameters, Problem.LowerBounds.Count, Problem.CountObjs);

            FirstStep(Problem);

            Progress progress = new Progress(this, 0, this._parameters.Imax - 1, 0);

            Reporter.Report(progress);

            for (int i = 1; i < this._parameters.Imax; i++)
            {
                NextStep(Problem);
                progress.Current = i;
                Reporter.Report(progress);
            }

            Clear();
        }

        /// <summary>
        /// <see cref="IOOOptimizer{T}.Minimize(GeneralParams, IProgress{Progress})"/> 
        /// </summary>
        /// <param name="GenParams"> General parameters. <see cref="GeneralParams"/>. </param>
        /// <param name="Reporter"> 
        /// Object which implement interface <see cref="IProgress{T}"/>, where T is
        /// <see cref="Progress"/>.
        /// <seealso cref="IOOOptimizer{T}.Minimize(GeneralParams, IProgress{Progress})"/><param name="CancelToken"> <see cref="CancellationToken"/></param>
        /// </param>
        /// <exception cref="InvalidOperationException"> If parameters do not set. </exception>
        /// <exception cref="ArgumentNullException">
        /// If <paramref name="GenParams"/> or <paramref name="Reporter"/> is null.
        /// </exception>
        /// <exception cref="ArithmeticException">
        /// If the function has value is NaN, PositiveInfinity or NegativeInfinity.
        /// </exception>
        /// <exception cref="OperationCanceledException"></exception>
        public override void Minimize(FWParams Parameters, IMOOptProblem Problem, IProgress<Progress> Reporter, CancellationToken CancelToken)
        {
            if (Reporter == null)
            {
                throw new ArgumentNullException(nameof(Reporter));
            }

            Init(Parameters, Problem.LowerBounds.Count, Problem.CountObjs);

            FirstStep(Problem);

            Progress progress = new Progress(this, 0, this._parameters.Imax - 1, 0);

            Reporter.Report(progress);

            for (int i = 1; i < this._parameters.Imax; i++)
            {
                CancelToken.ThrowIfCancellationRequested();

                NextStep(Problem);
                progress.Current = i;
                Reporter.Report(progress);
            }

            Clear();
        }
    }
}