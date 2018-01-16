﻿namespace EOpt.Math.Optimization.Tests
{
    using System;
    using Help;

    class TestReporter : IProgress<Progress>
    {
        public bool Error { get; private set; } = false;

        private int _iterMin, _iterMax;

        private Type _optimizerType;

        public TestReporter(Type OptimizerType, int IterMin, int IterMax)
        {
            _iterMin = IterMin;
            _iterMax = IterMax;
            _optimizerType = OptimizerType;
        }

        public void Report(Progress Prog)
        {
            if (_iterMin != Prog.Start || _iterMax != Prog.End)
                Error = true;
            if (Prog.Current < _iterMin || Prog.Current > _iterMax)
                Error = true;             
        }

    }
}
