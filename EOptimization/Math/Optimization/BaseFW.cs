﻿// This is an open source non-commercial project. Dear PVS-Studio, please check it. PVS-Studio Static
// Code Analyzer for C, C++ and C#: http://www.viva64.com
namespace EOpt.Math.Optimization
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using EOpt.Help;
    using EOpt.Math.Random;

    using Math.LA;

    using Priority_Queue;

    /// <summary>
    /// Base class for the FW method.
    /// </summary>
    /// <typeparam name="TProblem">Type of problem</typeparam>
    /// <typeparam name="TObj">Type of objective</typeparam>
    public abstract class BaseFW<TObj, TProblem> : IBaseOptimizer<FWParams, TProblem> where TProblem : IConstrOptProblem<double, TObj>
    {
        /// <summary>
        /// Charges.
        /// </summary>
        protected List<Agent> _chargePoints;

        // Need in the method 'GenerateIndexesOfAxes'.
        protected int[] _coordNumbers;

        /// <summary>
        /// Debris for charges.
        /// </summary>
        protected LinkedList<Agent>[] _debris;

        protected DynSymmetricMatrix _matrixOfDistances;

        protected int _minDebrisCount, _maxDebrisCount;

        protected INormalGen _normalRand;

        protected FWParams _parameters;

        protected IContUniformGen _uniformRand;

        protected List<WeightOfAgent> _weightedAgents;

        protected KahanSum _distKahanSum, _denumForProbKahanSum;

        protected AgentPool _pool;

        /// <summary>
        /// The structure is storing an agent and its probability of choosing.
        /// </summary>
        protected class WeightOfAgent
        {
            public Agent Agent { get; private set; }

            public double Weight { get; set; }

            public bool IsTake { get; set; }

            public WeightOfAgent(Agent Agent, double Dist)
            {
                this.Agent = Agent;
                this.Weight = Dist;
                IsTake = false;
            }

            public void Reset()
            {
                Weight = 0.0;
                IsTake = false;
            }
        }

        /// <summary>
        /// Helper class for using  FastPriorityQueue
        /// </summary>
        protected class AgentNode : FastPriorityQueueNode
        {
            public int AgentIndex { get; private set; }

            public AgentNode(int Index)
            {
                AgentIndex = Index;
            }
        }

        protected void CalculateDistances(Func<Agent, Agent, double> Distance)
        {
            _denumForProbKahanSum.SumResest();

            // Calculate distance between all points.
            for (int i = 0; i < _matrixOfDistances.RowCount; i++)
            {
                for (int j = i + 1; j < _matrixOfDistances.ColumnCount; j++)
                {
                    _matrixOfDistances[i, j] = Distance(_weightedAgents[i].Agent, _weightedAgents[j].Agent);
                }
            }

            for (int ii = 0; ii < _matrixOfDistances.RowCount; ii++)
            {
                _distKahanSum.SumResest();

                for (int j = 0; j < _matrixOfDistances.ColumnCount; j++)
                {
                    _distKahanSum.Add(_matrixOfDistances[ii, j]);
                }

                _weightedAgents[ii].Weight = _distKahanSum.Sum;

                _denumForProbKahanSum.Add(_distKahanSum.Sum);
            }

            // Probability of explosion.
            for (int jj = 0; jj < _weightedAgents.Count; jj++)
            {
                _weightedAgents[jj].Weight /= _denumForProbKahanSum.Sum;

                if (CheckDouble.GetTypeValue(_weightedAgents[jj].Weight) != DoubleTypeValue.Valid)
                {
                    for (int k = 0; k < _weightedAgents.Count; k++)
                    {
                        _weightedAgents[jj].Weight = 1.0 / _matrixOfDistances.ColumnCount;
                    }

                    break;
                }
            }
        }

        protected virtual void Clear()
        {
            _chargePoints.Clear();

            for (int i = 0; i < _debris.Length; i++)
            {
                _debris[i].Clear();
            }
        }

        protected void FindAmountDebrisForCharge(double S, int WhichCharge)
        {
            int countDebris = (int)Math.Truncate(S);

            if (countDebris < _minDebrisCount)
            {
                countDebris = _minDebrisCount;
            }
            else if (countDebris > _maxDebrisCount)
            {
                countDebris = _maxDebrisCount;
            }

            if (_debris[WhichCharge].Count > countDebris)
            {
                int del = _debris[WhichCharge].Count - countDebris;

                while (del > 0)
                {
                    _pool.AddAgent(_debris[WhichCharge].Last.Value);
                    _debris[WhichCharge].RemoveLast();
                    del--;
                }
            }
            else if (_debris[WhichCharge].Count < countDebris)
            {
                int total = countDebris - _debris[WhichCharge].Count;

                while (total > 0)
                {
                    _debris[WhichCharge].AddLast(_pool.GetAgent());
                    total--;
                }
            }
        }

        /// <summary>
        /// First method for determination of position of the debris.
        /// </summary>
        /// <param name="Splinter">        </param>
        /// <param name="CountOfDimension"></param>
        /// <param name="Amplitude">       </param>
        /// <param name="LowerBounds">     </param>
        /// <param name="UpperBounds">     </param>
        protected void FirstMethodDeterminationOfPosition(Agent Splinter, int CountOfDimension, double Amplitude, IReadOnlyList<double> LowerBounds, IReadOnlyList<double> UpperBounds)
        {
            // The indices are choosing randomly.
            GenerateIndicesOfAxes(CountOfDimension);

            double h = 0;

            // Calculate position of debris.
            for (int i = 0; i < CountOfDimension; i++)
            {
                int axisIndex = _coordNumbers[i];

                h = Amplitude * _uniformRand.URandVal(-1, 1);

                Splinter.Point[axisIndex] += h;

                if (Splinter.Point[axisIndex] < LowerBounds[axisIndex])
                {
                    Splinter.Point[axisIndex] = _uniformRand.URandVal(LowerBounds[axisIndex], 0.5 * (LowerBounds[axisIndex] + UpperBounds[axisIndex]));
                }
                else if (Splinter.Point[axisIndex] > UpperBounds[axisIndex])
                {
                    //Splinter.Point[axisIndex] = _uniformRand.URandVal(0.5 * (LowerBounds[axisIndex] + UpperBounds[axisIndex]), UpperBounds[axisIndex]);
                    Splinter.Point[axisIndex] =   UpperBounds[axisIndex];
                }
            }
        }

        protected abstract void FirstStep(TProblem Problem);

        protected void GenerateDebrisForCharge(IReadOnlyList<double> LowerBounds, IReadOnlyList<double> UpperBounds, double Amplitude, int WhichCharge)
        {
            double ksi = 0;

            // For each debris.
            foreach (Agent splinter in _debris[WhichCharge])
            {
                // The position of debris sets to the position of charge.
                splinter.Point.SetAt(_chargePoints[WhichCharge].Point);

                ksi = _uniformRand.URandVal(0, 1);

                int CountOfDimension = (int)Math.Ceiling(LowerBounds.Count * ksi);

                if (ksi < 0.5)
                {
                    FirstMethodDeterminationOfPosition(splinter, CountOfDimension, Amplitude, LowerBounds, UpperBounds);
                }
                else
                {
                    SecondMethodDeterminationOfPosition(splinter, CountOfDimension, LowerBounds, UpperBounds);
                }
            }
        }

        /// <summary>
        /// Generate randomly indices of axes.
        /// </summary>
        /// <returns></returns>
        protected void GenerateIndicesOfAxes(int TotalTake)
        {
            int i = 0;

            Random rand = SyncRandom.Get();

            foreach (int coordIndex in Enumerable.Range(0, _coordNumbers.Length))
            {
                if (coordIndex < TotalTake)
                {
                    _coordNumbers[i++] = coordIndex;
                }
                else
                {
                    int swapIndex = rand.Next(i);

                    if (swapIndex < TotalTake)
                    {
                        _coordNumbers[swapIndex] = coordIndex;
                    }
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="Parameters"></param>
        /// <param name="Dim"></param>
        /// <param name="DimObjs"></param>
        /// <exception cref="ArgumentException">If <paramref name="Parameters"/> is not initialized.</exception>
        protected virtual void Init(FWParams Parameters, int Dim, int DimObjs)
        {
            if (!Parameters.IsParamsInit)
            {
                throw new ArgumentException("The parameters were created by the default constructor and have invalid values. You need to create parameters with a custom constructor.", nameof(Parameters));
            }

            _parameters = Parameters;

            _minDebrisCount = _parameters.Smin;
            _maxDebrisCount = _parameters.Smax;

            if (_coordNumbers == null)
            {
                _coordNumbers = new int[Dim];
            }
            else if (_coordNumbers.Length != Dim)
            {
                _coordNumbers = new int[Dim];
            }

            if (_chargePoints == null)
            {
                _chargePoints = new List<Agent>(_parameters.NP);
            }
            else
            {
                _chargePoints.Clear();
                _chargePoints.Capacity = _parameters.NP;
            }

            if (_debris == null)
            {
                InitDebris();
            }
            else if (_debris.Length != this.Parameters.NP)
            {
                InitDebris();
            }

            int newSizeMatrix = checked(_parameters.NP - 1 + _parameters.NP * _minDebrisCount);

            _weightedAgents = new List<WeightOfAgent>(newSizeMatrix);

            _pool = new AgentPool(_parameters.NP * _maxDebrisCount / 2, new AgenCreator(Dim, DimObjs));

            if (_matrixOfDistances == null)
            {
                _matrixOfDistances = new DynSymmetricMatrix(newSizeMatrix);
            }
        }

        protected virtual void InitAgents(IReadOnlyList<double> LowerBounds, IReadOnlyList<double> UpperBounds, int DimObjs)
        {
            int dimension = LowerBounds.Count;

            // Create points of explosion.
            for (int i = 0; i < _parameters.NP; i++)
            {
                Agent agent = _pool.GetAgent();

                //PointND point = new PointND(0.0, dimension);

                for (int j = 0; j < dimension; j++)
                {
                    agent.Point[j] = _uniformRand.URandVal(LowerBounds[j], UpperBounds[j]);
                }

                _chargePoints.Add(agent);
            }
        }

        protected void InitDebris()
        {
            _debris = new LinkedList<Agent>[_parameters.NP];

            for (int i = 0; i < _debris.Length; i++)
            {
                _debris[i] = new LinkedList<Agent>();
            }
        }

        protected abstract void NextStep(TProblem Problem);

        protected void ResetMatrixAndTrimWeights(int NewSize)
        {
            _matrixOfDistances.ColumnCount = NewSize;
            _matrixOfDistances.Fill(0.0);

            if (NewSize > _weightedAgents.Count)
            {
                int countAdd = NewSize - _weightedAgents.Count;

                _weightedAgents.Capacity += countAdd;

                while (countAdd > 0)
                {
                    _weightedAgents.Add(new WeightOfAgent(_pool.GetAgent(), 0.0));
                    countAdd--;
                }
            }
            else if (_weightedAgents.Count > NewSize)
            {
                for (int i = NewSize; i < _weightedAgents.Count; i++)
                {
                    _pool.AddAgent(_weightedAgents[i].Agent);
                }

                _weightedAgents.RemoveRange(NewSize, _weightedAgents.Count - NewSize);
            }

            for (int i = 0; i < _weightedAgents.Count; i++)
            {
                _weightedAgents[i].Reset();
            }
        }

        /// <summary>
        /// Second method for determination of position of the debris.
        /// </summary>
        /// <param name="Splinter">        </param>
        /// <param name="CountOfDimension"></param>
        /// <param name="LowerBounds">     </param>
        /// <param name="UpperBounds">     </param>
        protected void SecondMethodDeterminationOfPosition(Agent Splinter, int CountOfDimension, IReadOnlyList<double> LowerBounds, IReadOnlyList<double> UpperBounds)
        {
            GenerateIndicesOfAxes(CountOfDimension);

            double g = 0;

            int axisIndex = 0;

            // Calculate position of debris.
            for (int i = 0; i < CountOfDimension; i++)
            {
                axisIndex = _coordNumbers[i];

                g = _normalRand.NRandVal(1, 1);

                Splinter.Point[axisIndex] *= g;

                if (Splinter.Point[axisIndex] < LowerBounds[axisIndex])
                {
                    Splinter.Point[axisIndex] = _uniformRand.URandVal(LowerBounds[axisIndex], 0.5 * (LowerBounds[axisIndex] + UpperBounds[axisIndex]));
                }
                else if (Splinter.Point[axisIndex] > UpperBounds[axisIndex])
                {
                    Splinter.Point[axisIndex] = _uniformRand.URandVal(0.5 * (LowerBounds[axisIndex] + UpperBounds[axisIndex]), UpperBounds[axisIndex]);
                }
            }
        }

        /// <summary>
        /// <para>
        /// Weighted Random Sampling without replacement.
        /// </para>
        /// </summary>
        /// <remarks>
        /// The original algorithm described in the Efraimidis, P.S., & Spirakis, P.G. (2007). Weighted Random Sampling (2005; Efraimidis, Spirakis).
        /// </remarks>
        /// <param name="TotalTake"></param>
        protected void TakeAgents(int TotalTake)
        {
            var _priority = new SimplePriorityQueue<int, double>();

            double weight = 0.0;

            for (int i = 0; i < _weightedAgents.Count; i++)
            {
                weight = Math.Pow(_uniformRand.URandVal(0, 1), 1 / _weightedAgents[i].Weight);

                if (_priority.Count < TotalTake)
                {
                    _priority.Enqueue(i, weight);
                }
                else
                {
                    if (weight >= _priority.GetPriority(_priority.First))
                    {
                        _priority.Dequeue();
                        _priority.Enqueue(i, weight);
                    }
                }
            }

            while (_priority.Count != 0)
            {
                _weightedAgents[_priority.Dequeue()].IsTake = true;
            }
        }

        /// <summary>
        /// Parameters for method.
        /// </summary>
        public FWParams Parameters => _parameters;

        /// <summary>
        /// Create the object which uses default implementation for random generators.
        /// </summary>
        public BaseFW() : this(new ContUniformDist(), new NormalDist())
        {
        }

        /// <summary>
        /// Create the object which uses custom implementation for random generators.
        /// </summary>
        /// <param name="UniformGen">
        /// Object, which implements <see cref="IContUniformGen"/> interface.
        /// </param>
        /// <param name="NormalGen">  Object, which implements <see cref="INormalGen"/> interface. </param>
        /// <exception cref="ArgumentNullException">
        /// If <paramref name="NormalGen"/> or <paramref name="UniformGen"/> is null.
        /// </exception>
        public BaseFW(IContUniformGen UniformGen, INormalGen NormalGen)
        {
            if (UniformGen == null)
            {
                throw new ArgumentNullException(nameof(UniformGen));
            }

            if (NormalGen == null)
            {
                throw new ArgumentNullException(nameof(NormalGen));
            }

            _uniformRand = UniformGen;

            _normalRand = NormalGen;

            _distKahanSum = new KahanSum();
            _denumForProbKahanSum = new KahanSum();
        }

        /// <summary>
        /// <see cref="IBaseOptimizer{TParams, TProblem}.Minimize(TParams, TProblem)"/>.
        /// </summary>
        /// <param name="Parameters"></param>
        /// <param name="Problem">   </param>
        public abstract void Minimize(FWParams Parameters, TProblem Problem);

        /// <summary>
        /// <see cref="IBaseOptimizer{TParams, TProblem}.Minimize(TParams, TProblem)"/>.
        /// </summary>
        /// <param name="Parameters"> </param>
        /// <param name="Problem">    </param>
        /// <param name="CancelToken"></param>
        public abstract void Minimize(FWParams Parameters, TProblem Problem, CancellationToken CancelToken);

        /// <summary>
        /// <see cref="IBaseOptimizer{TParams, TProblem}.Minimize(TParams, TProblem)"/>.
        /// </summary>
        /// <param name="Parameters"></param>
        /// <param name="Problem">   </param>
        /// <param name="Reporter">  </param>
        public abstract void Minimize(FWParams Parameters, TProblem Problem, IProgress<Progress> Reporter);

        /// <summary>
        /// <see cref="IBaseOptimizer{TParams, TProblem}.Minimize(TParams, TProblem)"/>.
        /// </summary>
        /// <param name="Parameters"> </param>
        /// <param name="Problem">    </param>
        /// <param name="Reporter">   </param>
        /// <param name="CancelToken"></param>
        public abstract void Minimize(FWParams Parameters, TProblem Problem, IProgress<Progress> Reporter, CancellationToken CancelToken);
    }
}