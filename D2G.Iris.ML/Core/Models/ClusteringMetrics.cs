using System;
using System.Collections.Generic;

namespace D2G.Iris.ML.Core.Models
{
    /// <summary>
    /// Metrics for evaluating clustering model quality
    /// </summary>
    public class ClusteringMetrics
    {
        /// <summary>
        /// Average distance of samples to their own cluster centroid (lower is better)
        /// </summary>
        public double AverageDistance { get; set; }

        /// <summary>
        /// Davies-Bouldin Index - measures cluster separation (lower is better)
        /// Range: [0, infinity), 0 indicates perfect clustering
        /// </summary>
        public double DaviesBouldinIndex { get; set; }

        /// <summary>
        /// Normalized Mutual Information (if ground truth labels available)
        /// Range: [0, 1], 1 indicates perfect clustering
        /// </summary>
        public double? NormalizedMutualInformation { get; set; }

        /// <summary>
        /// Number of clusters
        /// </summary>
        public int NumberOfClusters { get; set; }

        /// <summary>
        /// Distribution of samples across clusters
        /// </summary>
        public Dictionary<int, int> ClusterSizes { get; set; }

        /// <summary>
        /// Within-cluster sum of squares (inertia) - lower is better
        /// </summary>
        public double Inertia { get; set; }

        /// <summary>
        /// Silhouette score - measures how similar an object is to its own cluster
        /// Range: [-1, 1], 1 indicates well-clustered, -1 indicates misclustered
        /// </summary>
        public double SilhouetteScore { get; set; }

        /// <summary>
        /// Cluster centroids (feature vectors for each cluster center)
        /// </summary>
        public List<float[]> Centroids { get; set; }

        public ClusteringMetrics()
        {
            ClusterSizes = new Dictionary<int, int>();
            Centroids = new List<float[]>();
        }
    }
}
