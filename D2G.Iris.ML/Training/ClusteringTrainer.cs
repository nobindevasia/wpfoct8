using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;
using D2G.Iris.ML.Core.Interfaces;
using D2G.Iris.ML.Core.Models;
using D2G.Iris.ML.Core.Enums;

namespace D2G.Iris.ML.Training
{
    public class ClusteringTrainer : BaseModelTrainer
    {
        public ClusteringTrainer(MLContext mlContext, ITrainerFactory trainerFactory)
            : base(mlContext, trainerFactory)
        {
        }

        public override async Task<TrainingResult> TrainModel(
            MLContext mlContext,
            IDataView dataView,
            string[] featureNames,
            ModelConfig config,
            ProcessedData processedData)
        {
            Console.WriteLine("\n=============== Training K-Means Clustering Model ===============");
            Console.WriteLine($"Number of Clusters: {config.Clustering.NumberOfClusters}");
            Console.WriteLine($"Max Iterations: {config.Clustering.MaxIterations}");
            Console.WriteLine($"Features: {string.Join(", ", featureNames)}");

            // Build the training pipeline
            var pipeline = BuildClusteringPipeline(mlContext, config);

            // Train the model
            Console.WriteLine("\nTraining K-Means model...");
            var model = await TrainModelAsync(pipeline, dataView);
            Console.WriteLine("Training completed!");

            // Extract centroids from trained model
            try
            {
                if (model is TransformerChain<ClusteringPredictionTransformer<KMeansModelParameters>> chain)
                {
                    var kmeansParams = chain.LastTransformer.Model;

                    // Pass null - the method will allocate the array internally
                    VBuffer<float>[] modelCentroids = null;
                    kmeansParams.GetClusterCentroids(ref modelCentroids, out int k);

                    Console.WriteLine($"\nK-Means Model: Extracted {k} centroids:");
                    for (int i = 0; i < k; i++)
                    {
                        // Copy to a new dense array to avoid VBuffer issues
                        var buffer = modelCentroids[i];
                        var values = new float[buffer.Length];
                        buffer.CopyTo(values);
                        Console.WriteLine($"  Centroid {i + 1}: [{string.Join(", ", values.Select(v => v.ToString("F4")))}]");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not extract model centroids: {ex.Message}");
            }

            // Make predictions on the entire dataset
            var predictions = model.Transform(dataView);

            // Calculate clustering metrics
            var metrics = EvaluateClustering(mlContext, predictions, config.Clustering.NumberOfClusters);

            // Extract cluster assignments and data points for visualization
            var (clusterAssignments, dataPoints, centroids) = ExtractClusteringData(
                mlContext,
                predictions,
                featureNames.Length);

            // Print metrics
            PrintClusteringMetrics(metrics);

            // Save the model
            string modelPath = SaveModel(
                mlContext,
                model,
                dataView,
                "Clustering",
                config.Clustering.Algorithm,
                featureNames,
                ModelType.Clustering);

            return new TrainingResult
            {
                Model = model,
                Metrics = metrics,
                AlgorithmUsed = config.Clustering.Algorithm,
                FeatureNames = featureNames.ToList(),
                ClusterAssignments = clusterAssignments,
                ClusterCentroids = centroids,
                ClusterSizes = metrics.ClusterSizes,
                DataPoints = dataPoints
            };
        }

        private IEstimator<ITransformer> BuildClusteringPipeline(
            MLContext mlContext,
            ModelConfig config)
        {
            IEstimator<ITransformer> pipeline;

            // Add normalization if enabled (highly recommended for K-Means)
            if (config.Clustering.UseNormalization)
            {
                Console.WriteLine("Applying MinMax normalization to features...");
                pipeline = mlContext.Transforms.NormalizeMinMax("Features");
            }
            else
            {
                // Create a no-op pipeline if normalization is disabled
                pipeline = mlContext.Transforms.CopyColumns("Features", "Features");
            }

            // Add K-Means trainer
            var kmeansTrainer = mlContext.Clustering.Trainers.KMeans(
                featureColumnName: "Features",
                numberOfClusters: config.Clustering.NumberOfClusters);

            pipeline = pipeline.Append(kmeansTrainer);

            return pipeline;
        }

        private Core.Models.ClusteringMetrics EvaluateClustering(
            MLContext mlContext,
            IDataView predictions,
            int numberOfClusters)
        {
            Console.WriteLine("\n=============== Evaluating Clustering Model ===============");

            // Get ML.NET clustering metrics
            var mlMetrics = mlContext.Clustering.Evaluate(predictions);

            // Calculate cluster sizes
            var clusterSizes = CalculateClusterSizes(predictions);

            // Calculate additional metrics
            var silhouetteScore = CalculateSilhouetteScore(predictions, numberOfClusters);
            var daviesBouldinIndex = CalculateDaviesBouldinIndex(predictions, numberOfClusters);

            // Extract centroids
            var centroids = ExtractCentroids(predictions);

            var metrics = new Core.Models.ClusteringMetrics
            {
                AverageDistance = mlMetrics.AverageDistance,
                DaviesBouldinIndex = daviesBouldinIndex,
                NumberOfClusters = numberOfClusters,
                ClusterSizes = clusterSizes,
                Inertia = CalculateInertia(predictions),
                SilhouetteScore = silhouetteScore,
                Centroids = centroids
            };

            return metrics;
        }

        private Dictionary<int, int> CalculateClusterSizes(IDataView predictions)
        {
            var clusterSizes = new Dictionary<int, int>();

            var clusterColumn = predictions.GetColumn<uint>("PredictedLabel").ToList();

            foreach (var clusterId in clusterColumn)
            {
                if (!clusterSizes.ContainsKey((int)clusterId))
                {
                    clusterSizes[(int)clusterId] = 0;
                }
                clusterSizes[(int)clusterId]++;
            }

            return clusterSizes;
        }

        private double CalculateSilhouetteScore(IDataView predictions, int numberOfClusters)
        {
            // Optimized silhouette score calculation with sampling for large datasets
            try
            {
                var features = predictions.GetColumn<float[]>("Features").ToList();
                var clusters = predictions.GetColumn<uint>("PredictedLabel").ToList();

                if (features.Count == 0 || numberOfClusters <= 1)
                    return 0.0;

                // For large datasets, use sampling to avoid O(n²) complexity
                const int maxSampleSize = 1000;
                List<int> sampleIndices;

                if (features.Count > maxSampleSize)
                {
                    Console.WriteLine($"  Sampling {maxSampleSize} points for Silhouette Score calculation...");
                    var random = new Random(42);
                    sampleIndices = Enumerable.Range(0, features.Count)
                        .OrderBy(_ => random.Next())
                        .Take(maxSampleSize)
                        .ToList();
                }
                else
                {
                    sampleIndices = Enumerable.Range(0, features.Count).ToList();
                }

                // Pre-group points by cluster for efficiency
                var clusterPoints = new Dictionary<uint, List<(int index, float[] feature)>>();
                for (int i = 0; i < features.Count; i++)
                {
                    var c = clusters[i];
                    if (!clusterPoints.ContainsKey(c))
                        clusterPoints[c] = new List<(int, float[])>();
                    clusterPoints[c].Add((i, features[i]));
                }

                double totalScore = 0.0;
                int validPoints = 0;

                foreach (var i in sampleIndices)
                {
                    var point = features[i];
                    var cluster = clusters[i];

                    // Calculate average distance to points in same cluster (a)
                    var sameCluster = clusterPoints[cluster];
                    if (sameCluster.Count <= 1) continue;

                    double a = sameCluster
                        .Where(p => p.index != i)
                        .Average(p => EuclideanDistance(point, p.feature));

                    // Calculate minimum average distance to points in other clusters (b)
                    double b = double.MaxValue;
                    foreach (var kvp in clusterPoints)
                    {
                        if (kvp.Key == cluster || kvp.Value.Count == 0) continue;

                        double avgDist = kvp.Value.Average(p => EuclideanDistance(point, p.feature));
                        b = Math.Min(b, avgDist);
                    }

                    if (b != double.MaxValue)
                    {
                        double s = (b - a) / Math.Max(a, b);
                        totalScore += s;
                        validPoints++;
                    }
                }

                return validPoints > 0 ? totalScore / validPoints : 0.0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not calculate Silhouette Score: {ex.Message}");
                return 0.0;
            }
        }

        private double CalculateDaviesBouldinIndex(IDataView predictions, int numberOfClusters)
        {
            try
            {
                var features = predictions.GetColumn<float[]>("Features").ToList();
                var clusters = predictions.GetColumn<uint>("PredictedLabel").ToList();

                if (features.Count == 0 || numberOfClusters <= 1)
                    return 0.0;

                // Pre-group points by cluster for efficiency
                var clusterPoints = new Dictionary<uint, List<float[]>>();
                for (int i = 0; i < features.Count; i++)
                {
                    var c = clusters[i];
                    if (!clusterPoints.ContainsKey(c))
                        clusterPoints[c] = new List<float[]>();
                    clusterPoints[c].Add(features[i]);
                }

                // Calculate centroids
                var centroids = new Dictionary<uint, float[]>();
                foreach (var kvp in clusterPoints)
                {
                    centroids[kvp.Key] = CalculateCentroid(kvp.Value);
                }

                // Pre-calculate average distances from each cluster to its centroid
                var avgDistances = new Dictionary<uint, double>();
                foreach (var kvp in clusterPoints)
                {
                    avgDistances[kvp.Key] = kvp.Value.Average(p => EuclideanDistance(p, centroids[kvp.Key]));
                }

                // Calculate Davies-Bouldin Index
                double dbIndex = 0.0;
                foreach (var ci in centroids.Keys)
                {
                    double maxRatio = 0.0;
                    double si = avgDistances[ci];

                    foreach (var cj in centroids.Keys)
                    {
                        if (ci == cj) continue;

                        double sj = avgDistances[cj];
                        double mij = EuclideanDistance(centroids[ci], centroids[cj]);
                        double ratio = (si + sj) / mij;

                        maxRatio = Math.Max(maxRatio, ratio);
                    }
                    dbIndex += maxRatio;
                }

                return centroids.Count > 0 ? dbIndex / centroids.Count : 0.0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not calculate Davies-Bouldin Index: {ex.Message}");
                return 0.0;
            }
        }

        private double CalculateInertia(IDataView predictions)
        {
            try
            {
                var features = predictions.GetColumn<float[]>("Features").ToList();
                var clusters = predictions.GetColumn<uint>("PredictedLabel").ToList();
                var distances = predictions.GetColumn<float[]>("Score").ToList();

                double inertia = 0.0;
                for (int i = 0; i < distances.Count; i++)
                {
                    // Score contains distances to all centroids, take the minimum (assigned cluster)
                    inertia += distances[i].Min();
                }

                return inertia;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not calculate Inertia: {ex.Message}");
                return 0.0;
            }
        }

        private List<float[]> ExtractCentroids(IDataView predictions)
        {
            // Note: ML.NET doesn't directly expose centroids from K-Means
            // We'll calculate them from the assigned clusters
            try
            {
                var features = predictions.GetColumn<float[]>("Features").ToList();
                var clusters = predictions.GetColumn<uint>("PredictedLabel").ToList();

                var maxCluster = clusters.Max();
                var centroids = new List<float[]>();

                for (uint c = 0; c <= maxCluster; c++)
                {
                    var clusterPoints = features.Where((f, idx) => clusters[idx] == c).ToList();
                    if (clusterPoints.Count > 0)
                    {
                        centroids.Add(CalculateCentroid(clusterPoints));
                    }
                }

                return centroids;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not extract centroids: {ex.Message}");
                return new List<float[]>();
            }
        }

        private float[] CalculateCentroid(List<float[]> points)
        {
            if (points.Count == 0) return new float[0];

            int dimensions = points[0].Length;
            var centroid = new float[dimensions];

            for (int d = 0; d < dimensions; d++)
            {
                centroid[d] = points.Average(p => p[d]);
            }

            return centroid;
        }

        private (List<uint> clusterAssignments, List<float[]> dataPoints, List<float[]> centroids)
            ExtractClusteringData(MLContext mlContext, IDataView predictions, int featureCount)
        {
            var clusterAssignments = predictions.GetColumn<uint>("PredictedLabel").ToList();
            var dataPoints = predictions.GetColumn<float[]>("Features").ToList();
            var centroids = ExtractCentroids(predictions);

            return (clusterAssignments, dataPoints, centroids);
        }

        private double EuclideanDistance(float[] a, float[] b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException("Vectors must have the same length");

            double sum = 0.0;
            for (int i = 0; i < a.Length; i++)
            {
                double diff = a[i] - b[i];
                sum += diff * diff;
            }
            return Math.Sqrt(sum);
        }

        private void PrintClusteringMetrics(Core.Models.ClusteringMetrics metrics)
        {
            Console.WriteLine($"\nClustering Evaluation Metrics:");
            Console.WriteLine($"  Number of Clusters:        {metrics.NumberOfClusters}");
            Console.WriteLine($"  Average Distance:          {metrics.AverageDistance:F4}");
            Console.WriteLine($"  Silhouette Score:          {metrics.SilhouetteScore:F4} (closer to 1 is better)");
            Console.WriteLine($"  Davies-Bouldin Index:      {metrics.DaviesBouldinIndex:F4} (lower is better)");
            Console.WriteLine($"  Inertia (WCSS):            {metrics.Inertia:F4}");

            Console.WriteLine("\n  Cluster Sizes:");
            foreach (var kvp in metrics.ClusterSizes.OrderBy(x => x.Key))
            {
                double percentage = (kvp.Value * 100.0) / metrics.ClusterSizes.Values.Sum();
                Console.WriteLine($"    Cluster {kvp.Key}: {kvp.Value} samples ({percentage:F1}%)");
            }
        }
    }
}
