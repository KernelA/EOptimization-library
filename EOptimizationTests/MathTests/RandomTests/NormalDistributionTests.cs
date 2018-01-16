﻿namespace EOpt.Math.Random.Tests
{
    using Xunit;
    using EOpt.Math.Random;
    using System;


    public class NormalDistributionTests
    {
        [Fact]
        public void NormalDistributionConstrTest()
        {
            NormalDistribution dist = new NormalDistribution();

            Assert.True(dist.Mean == 0 && dist.StdDev == 1);
        }

        [Fact]
        public void NormalDistributionConstrTest1()
        {

            Assert.Throws<ArgumentException>(() => new NormalDistribution(1, -2));            
        }

        [Fact]
        public void NormalDistributionConstrTest3()
        {
            NormalDistribution dist = new NormalDistribution(2, 5);

            Assert.True(dist.Mean == 2 && dist.StdDev == 5);
        }

        [Fact]
        public void NRandValTest()
        {
            NormalDistribution dist = new NormalDistribution(0, 2);

            Assert.Throws<ArgumentException>(() => dist.NRandVal(1, -1));
        }
    }
}