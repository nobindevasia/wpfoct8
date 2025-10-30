using System;
using System.Collections.Generic;

namespace D2G.Iris.ML.Core.Models
{
    public class AutoMLConfig
    {
        public bool Enabled { get; set; }
        public int MaxExperimentTimeInSeconds { get; set; }
        public int MaxModels { get; set; }
        public string OptimizingMetric { get; set; }
        public bool UseCrossValidation { get; set; }
        public int NumberOfFolds { get; set; } = 5;
        public uint? Seed { get; set; } = null;
    }
}