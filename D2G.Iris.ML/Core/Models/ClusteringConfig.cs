using System.Collections.Generic;

namespace D2G.Iris.ML.Core.Models
{
    /// <summary>
    /// Configuration for clustering algorithms
    /// </summary>
    public class ClusteringConfig
    {
        /// <summary>
        /// Number of clusters (K in K-Means)
        /// </summary>
        public int NumberOfClusters { get; set; } = 3;

        /// <summary>
        /// Maximum number of iterations for the clustering algorithm
        /// </summary>
        public int MaxIterations { get; set; } = 100;

        /// <summary>
        /// Algorithm to use for clustering (e.g., "kmeans")
        /// </summary>
        public string Algorithm { get; set; } = "kmeans";

        /// <summary>
        /// Whether to normalize features before clustering (recommended for K-Means)
        /// </summary>
        public bool UseNormalization { get; set; } = true;

        /// <summary>
        /// Algorithm-specific parameters
        /// </summary>
        public Dictionary<string, object> AlgorithmParameters { get; set; }

        public ClusteringConfig()
        {
            AlgorithmParameters = new Dictionary<string, object>();
        }
    }
}
