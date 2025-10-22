using Microsoft.ML;
using Microsoft.ML.Data;

namespace D2G.Iris.ML.Core.Models
{
    public class TrainingResult
    {
        public ITransformer Model { get; set; }
        public object Metrics { get; set; } // Can be BinaryClassificationMetrics, MulticlassClassificationMetrics, or RegressionMetrics
        public string AlgorithmUsed { get; set; }
    }
}
