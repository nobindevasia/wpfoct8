using System.Collections.Generic;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace D2G.Iris.ML.Core.Models
{
    public class TrainingResult
    {
        public ITransformer Model { get; set; }
        public object Metrics { get; set; } // Can be BinaryClassificationMetrics, MulticlassClassificationMetrics, or RegressionMetrics
        public string AlgorithmUsed { get; set; }

        // ROC Curve data (for binary classification)
        public List<double> RocCurveFpr { get; set; } // False Positive Rate
        public List<double> RocCurveTpr { get; set; } // True Positive Rate
        public List<double> RocCurveThresholds { get; set; } // Thresholds
        public double AucScore { get; set; } // Area Under Curve

        // Precision-Recall Curve data (for binary classification)
        public List<double> PrecisionRecallPrecision { get; set; } // Precision values
        public List<double> PrecisionRecallRecall { get; set; } // Recall values
        public List<double> PrecisionRecallThresholds { get; set; } // Thresholds
        public double AveragePrecision { get; set; } // Average Precision (AP)
    }
}
