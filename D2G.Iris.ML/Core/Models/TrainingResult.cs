using System.Collections.Generic;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace D2G.Iris.ML.Core.Models
{
    public class TrainingResult
    {
        public ITransformer Model { get; set; }
        public object Metrics { get; set; }
        public string AlgorithmUsed { get; set; }


        public List<double> RocCurveFpr { get; set; }
        public List<double> RocCurveTpr { get; set; }
        public List<double> RocCurveThresholds { get; set; }
        public double AucScore { get; set; }


        public List<double> PrecisionRecallPrecision { get; set; }
        public List<double> PrecisionRecallRecall { get; set; }
        public List<double> PrecisionRecallThresholds { get; set; }
        public double AveragePrecision { get; set; }


        public List<string> FeatureNames { get; set; }
        public List<double> FeatureImportanceScores { get; set; }

        // Regression-specific data for residual plots
        public List<double> ActualValues { get; set; }
        public List<double> PredictedValues { get; set; }
        public List<double> Residuals { get; set; }

        // Clustering-specific data
        public List<uint> ClusterAssignments { get; set; }
        public List<float[]> ClusterCentroids { get; set; }
        public Dictionary<int, int> ClusterSizes { get; set; }
        public List<float[]> DataPoints { get; set; } // For visualization
    }
}
