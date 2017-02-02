﻿namespace EOpt.Math.Optimization
{
    using System;


    /// <summary>
    /// Parameters for Fireworks method. <see cref="FireworksOptimizer"/>
    /// </summary>
    public class FireWorksParams
    {
        private int np, m, nmax;

        private double alpha, beta, amax;

        private Func<PointND, PointND, double> distFunc;

        /// <summary>
        /// Number of charges on each iteration.
        /// </summary>
        public int NP
        {
            get
            {
                return this.np;
            }
        }

        /// <summary>
        /// Number of debris for each charge.
        /// </summary>
        public int M
        {
            get
            {
                return this.m;
            }
        }

        /// <summary>
        /// Max iteration.
        /// </summary>
        public int Imax
        {
            get
            {
                return this.nmax;
            }
        }

        /// <summary>
        /// Parameter, which restricts the number of debris  from below.
        /// </summary>
        public double Alpha
        {
            get
            {
                return this.alpha;
            }
        }

        /// <summary>
        /// Parameter, which restricts the number of debris  from above.
        /// </summary>
        public double Beta
        {

            get
            {
                return this.beta;
            }
        }

        /// <summary>
        /// Maximum amplitude of explosion.
        /// </summary>
        public double Amax
        {
            get
            {
                return this.amax;
            }
        }

        /// <summary>
        /// Function for measurement distance between points.
        /// </summary>
        public Func<PointND, PointND, double> DistanceFunction
        {
            get
            {
                return distFunc;
            }
        }

        /// <summary>
        /// Parameters for Fireworks method.
        /// </summary>
        /// <param name="NP">Number of charges on each iteration.</param>
        /// <param name="Imax">Max iteration.</param>
        /// <param name="distanceFunction">Function for measurement distance between points.</param>
        /// <param name="m">Number of debris for each charge.</param>
        /// <param name="alpha">Parameter, which restricts the number of debris  from below. <paramref name="alpha"/> int (0;1),  <paramref name="alpha"/> &lt; <paramref name="beta"/>.</param>
        /// <param name="beta">Parameter, which restricts the number of debris  from above. <paramref name="beta"/> in (0;1), <paramref name="beta"/> &gt <paramref name="alpha"/>.</param>
        /// <param name="Amax">Maximum amplitude of explosion.</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public FireWorksParams(int NP, int Imax, Func<PointND, PointND, double> distanceFunction, int m, double alpha = 0.1, double beta = 0.9, double Amax = 40)
        {
            if (NP < 1 || m < 1 || Imax < 1)
                throw new ArgumentException($"{nameof(Imax)}, {nameof(m)}, {nameof(Imax)} must be > 0.");
            if (alpha <= 0 || beta >= 1 || alpha >= beta)
                throw new ArgumentException($"{nameof(alpha)} and {nameof(beta)} must be in (0;1), {nameof(alpha)} < {nameof(beta)}.");
            if (Amax <= 0)
                throw new ArgumentException($"{nameof(Amax)} must be > 0.", nameof(Amax));
            if (distanceFunction == null)
                throw new ArgumentNullException(nameof(distanceFunction));

            this.np = NP;
            this.nmax = Imax;
            this.m = m;
            this.amax = Amax;
            this.alpha = alpha;
            this.beta = beta;
            distFunc = distanceFunction;
        }
    }

}