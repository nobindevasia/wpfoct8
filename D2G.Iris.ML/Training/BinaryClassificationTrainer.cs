using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.AutoML;
using D2G.Iris.ML.Core.Interfaces;
using D2G.Iris.ML.Core.Models;
using D2G.Iris.ML.Utils;

namespace D2G.Iris.ML.Training
{
    public class BinaryClassificationTrainer : BaseModelTrainer
    {



        public BinaryClassificationTrainer(MLContext mlContext, ITrainerFactory trainerFactory)
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
            Console.WriteLine($"\nStarting binary classification using {(config.AutoML?.Enabled == true ? "AutoML" : config.TrainingParameters.Algorithm)}");
            try
            {

                IDataView labeledData;

                if (dataView.Schema.GetColumnOrNull("Label").HasValue)
                {

                    var labelType = dataView.Schema["Label"].Type;
                    if (labelType.RawType != typeof(bool))
                    {

                        var labelPipeline = mlContext.Transforms.Conversion.ConvertType(
                            outputColumnName: "Label", inputColumnName: "Label", outputKind: DataKind.Boolean);
                        labeledData = labelPipeline.Fit(dataView).Transform(dataView);
                    }
                    else
                    {
                        labeledData = dataView;
                    }
                }
                else
                {

                    var labelPipeline = mlContext.Transforms.CopyColumns(
                            outputColumnName: "RawLabel", inputColumnName: config.TargetField)
                        .Append(mlContext.Transforms.Conversion.ConvertType(
                            outputColumnName: "Label", inputColumnName: "RawLabel", outputKind: DataKind.Boolean));
                    labeledData = labelPipeline.Fit(dataView).Transform(dataView);
                }

                IDataView preparedData = PrepareData(labeledData, featureNames);

                return config.AutoML?.Enabled == true
                    ? await TrainWithAdvancedAutoML(mlContext, preparedData, featureNames, config, processedData)
                    : await TrainWithTraditionalApproach(mlContext, preparedData, featureNames, config, processedData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in training process: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }


        private async Task<TrainingResult> TrainWithAdvancedAutoML(
            MLContext mlContext,
            IDataView preparedData,
            string[] featureNames,
            ModelConfig config,
            ProcessedData processedData)
        {
            Console.WriteLine($"Maximum experiment time: {config.AutoML.MaxExperimentTimeInSeconds} seconds");
            Console.WriteLine($"Optimizing Metric: {config.AutoML.OptimizingMetric}");

            try
            {

                CreateCacheDirectory();


                if (!Enum.TryParse(config.AutoML.OptimizingMetric, out BinaryClassificationMetric metric))
                {
                    Console.WriteLine($"Warning: Unknown OptimizingMetric '{config.AutoML.OptimizingMetric}', defaulting to {nameof(BinaryClassificationMetric.Accuracy)}");
                    metric = BinaryClassificationMetric.Accuracy;
                }


                var experimentSettings = new BinaryExperimentSettings
                {
                    MaxExperimentTimeInSeconds = (uint)config.AutoML.MaxExperimentTimeInSeconds,
                    OptimizingMetric = metric
                };

                if (config.AutoML.MaxModels > 0)
                {
                    LimitTrainers(experimentSettings, config.AutoML.MaxModels);
                }

                Console.WriteLine("Creating experiment...");
                var experiment = mlContext.Auto()
                    .CreateBinaryClassificationExperiment(experimentSettings);

                var experimentStartTime = DateTime.Now;
                var experimentResult = experiment.Execute(
                    trainData: preparedData,
                    labelColumnName: "Label");
                var experimentDuration = DateTime.Now - experimentStartTime;


                Console.WriteLine($"AutoML experiment completed in {experimentDuration.TotalMinutes:F1} minutes");
                Console.WriteLine($"\n=== AutoML Experiment Summary ===");
                Console.WriteLine($"Models evaluated: {experimentResult.RunDetails.Count()}");

                PrintTopModels(experimentResult.RunDetails, metric);


                var bestRun = experimentResult.BestRun;
                var cleanTrainerName = CleanTrainerName(bestRun.TrainerName);
                Console.WriteLine($"\nBest model: {cleanTrainerName}");
                Console.WriteLine($"Training time: {bestRun.RuntimeInSeconds:F1} seconds");

                double bestMetricValue = GetMetricValue(bestRun.ValidationMetrics, metric);
                Console.WriteLine($"Best {metric} value: {bestMetricValue:F4}");


                PrintBinaryClassificationMetrics(bestRun.ValidationMetrics, cleanTrainerName);

                Console.WriteLine($"Confusion Matrix:\n{bestRun.ValidationMetrics.ConfusionMatrix.GetFormattedConfusionTable()}");


                await SaveModelInfo(
                    bestRun.ValidationMetrics,
                    preparedData,
                    featureNames,
                    config,
                    processedData);


                var safeName = SanitizeFileName(cleanTrainerName);
                var modelPath = $"BinaryClassification_AutoML_{safeName}_Model.zip";
                mlContext.Model.Save(bestRun.Model, preparedData.Schema, modelPath);
                Console.WriteLine($"\nModel saved to: {modelPath}");


                Console.WriteLine("\nCalculating ROC curve...");
                var rocData = CalculateRocCurve(mlContext, bestRun.Model, preparedData);


                Console.WriteLine("\nCalculating Precision-Recall curve...");
                var prData = CalculatePrecisionRecallCurve(mlContext, bestRun.Model, preparedData);

                // Calculate Feature Importance
                var (importanceFeatureNames, importanceScores) = CalculateBinaryFeatureImportance(
                    mlContext, bestRun.Model, preparedData, featureNames);

                return new TrainingResult
                {
                    Model = bestRun.Model,
                    Metrics = bestRun.ValidationMetrics,
                    AlgorithmUsed = cleanTrainerName,
                    RocCurveFpr = rocData.fpr,
                    RocCurveTpr = rocData.tpr,
                    RocCurveThresholds = rocData.thresholds,
                    AucScore = bestRun.ValidationMetrics.AreaUnderRocCurve,
                    PrecisionRecallPrecision = prData.precision,
                    PrecisionRecallRecall = prData.recall,
                    PrecisionRecallThresholds = prData.thresholds,
                    AveragePrecision = prData.averagePrecision,
                    FeatureNames = importanceFeatureNames,
                    FeatureImportanceScores = importanceScores
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in AutoML process: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }

                Console.WriteLine("Falling back to traditional approach...");
                return await TrainWithTraditionalApproach(mlContext, preparedData, featureNames, config, processedData);
            }
        }

        private void PrintTopModels(IEnumerable<RunDetail<BinaryClassificationMetrics>> runs, BinaryClassificationMetric metric)
        {
            Console.WriteLine($"\nTop 5 models evaluated (ranked by {metric}):");
            Console.WriteLine("Rank | Model Type                | AUC      | Accuracy | F1 Score | Runtime");
            Console.WriteLine("-----|---------------------------|----------|----------|----------|--------");

            int rank = 1;
            var orderedRuns = OrderRunsByMetric(runs, metric);

            foreach (var run in orderedRuns.Take(5))
            {
                var cleanName = CleanTrainerName(run.TrainerName);
                Console.WriteLine($"{rank,4} | {cleanName,-24} | {run.ValidationMetrics.AreaUnderRocCurve,8:F4} | " +
                                  $"{run.ValidationMetrics.Accuracy,8:F4} | {run.ValidationMetrics.F1Score,8:F4} | " +
                                  $"{run.RuntimeInSeconds,6:F1}s");
                rank++;
            }
        }

        private IEnumerable<RunDetail<BinaryClassificationMetrics>> OrderRunsByMetric(
            IEnumerable<RunDetail<BinaryClassificationMetrics>> runs,
            BinaryClassificationMetric metric)
        {
            return metric switch
            {
                BinaryClassificationMetric.Accuracy => runs.OrderByDescending(r => r.ValidationMetrics.Accuracy),
                BinaryClassificationMetric.AreaUnderRocCurve => runs.OrderByDescending(r => r.ValidationMetrics.AreaUnderRocCurve),
                BinaryClassificationMetric.AreaUnderPrecisionRecallCurve => runs.OrderByDescending(r => r.ValidationMetrics.AreaUnderPrecisionRecallCurve),
                BinaryClassificationMetric.F1Score => runs.OrderByDescending(r => r.ValidationMetrics.F1Score),
                BinaryClassificationMetric.NegativePrecision => runs.OrderByDescending(r => r.ValidationMetrics.NegativePrecision),
                BinaryClassificationMetric.NegativeRecall => runs.OrderByDescending(r => r.ValidationMetrics.NegativeRecall),
                BinaryClassificationMetric.PositivePrecision => runs.OrderByDescending(r => r.ValidationMetrics.PositivePrecision),
                BinaryClassificationMetric.PositiveRecall => runs.OrderByDescending(r => r.ValidationMetrics.PositiveRecall),
                _ => runs.OrderByDescending(r => r.ValidationMetrics.AreaUnderRocCurve)
            };
        }

        private double GetMetricValue(BinaryClassificationMetrics metrics, BinaryClassificationMetric metric)
        {
            return metric switch
            {
                BinaryClassificationMetric.Accuracy => metrics.Accuracy,
                BinaryClassificationMetric.AreaUnderRocCurve => metrics.AreaUnderRocCurve,
                BinaryClassificationMetric.AreaUnderPrecisionRecallCurve => metrics.AreaUnderPrecisionRecallCurve,
                BinaryClassificationMetric.F1Score => metrics.F1Score,
                BinaryClassificationMetric.NegativePrecision => metrics.NegativePrecision,
                BinaryClassificationMetric.NegativeRecall => metrics.NegativeRecall,
                BinaryClassificationMetric.PositivePrecision => metrics.PositivePrecision,
                BinaryClassificationMetric.PositiveRecall => metrics.PositiveRecall,
                _ => metrics.AreaUnderRocCurve
            };
        }

        private async Task<TrainingResult> TrainWithTraditionalApproach(
            MLContext mlContext,
            IDataView preparedData,
            string[] featureNames,
            ModelConfig config,
            ProcessedData processedData)
        {
            Console.WriteLine($"Using traditional approach with {config.TrainingParameters.Algorithm}");

            var split = SplitTrainTestData(
                mlContext,
                preparedData,
                config.TrainingParameters.TestFraction);

            var trainer = _trainerFactory.GetTrainer(
                config.ModelType,
                config.TrainingParameters);

            Console.WriteLine($"Training with algorithm: {config.TrainingParameters.Algorithm}");
            Console.WriteLine("Algorithm parameters:");
            if (config.TrainingParameters.AlgorithmParameters != null)
            {
                foreach (var param in config.TrainingParameters.AlgorithmParameters)
                {
                    Console.WriteLine($"  {param.Key}: {param.Value}");
                }
            }

            IEstimator<ITransformer> pipeline;
            if (split.TrainSet.Schema.GetColumnOrNull("Features").HasValue)
            {
                pipeline = trainer
                    .Append(mlContext.Transforms.CopyColumns("Probability", "Score"));
            }
            else
            {
                pipeline = GetBasePipeline(mlContext)
                    .Append(trainer)
                    .Append(mlContext.Transforms.CopyColumns("Probability", "Score"));
            }

            var trainingStartTime = DateTime.Now;
            var model = await TrainModelAsync(pipeline, split.TrainSet);
            var trainingDuration = DateTime.Now - trainingStartTime;
            Console.WriteLine($"Training completed in {trainingDuration.TotalSeconds:F1} seconds");

            var metrics = EvaluateBinaryClassification(
                mlContext,
                model,
                split.TestSet,
                config.TrainingParameters.Algorithm);

            Console.WriteLine($"Confusion Matrix:\n{metrics.ConfusionMatrix.GetFormattedConfusionTable()}");

            await SaveModelInfo(
                metrics,
                preparedData,
                featureNames,
                config,
                processedData);

            SaveModel(
                mlContext,
    model,
    preparedData,
    "BinaryClassification",
    config.TrainingParameters.Algorithm,
    featureNames,
    Core.Enums.ModelType.BinaryClassification);


            Console.WriteLine("\nCalculating ROC curve...");
            var rocData = CalculateRocCurve(mlContext, model, split.TestSet);


            Console.WriteLine("\nCalculating Precision-Recall curve...");
            var prData = CalculatePrecisionRecallCurve(mlContext, model, split.TestSet);

            // Calculate Feature Importance
            var (importanceFeatureNames, importanceScores) = CalculateBinaryFeatureImportance(
                mlContext, model, split.TestSet, featureNames);

            return new TrainingResult
            {
                Model = model,
                Metrics = metrics,
                AlgorithmUsed = config.TrainingParameters.Algorithm,
                RocCurveFpr = rocData.fpr,
                RocCurveTpr = rocData.tpr,
                RocCurveThresholds = rocData.thresholds,
                AucScore = metrics.AreaUnderRocCurve,
                PrecisionRecallPrecision = prData.precision,
                PrecisionRecallRecall = prData.recall,
                PrecisionRecallThresholds = prData.thresholds,
                AveragePrecision = prData.averagePrecision,
                FeatureNames = importanceFeatureNames,
                FeatureImportanceScores = importanceScores
            };
        }
        private class BinaryVector
        {
            [VectorType]
            public float[] Features { get; set; }
            public bool Label { get; set; }
        }

        private class BinaryPrediction
        {
            public bool PredictedLabel { get; set; }
            public float Score { get; set; }
            public float Probability { get; set; }
        }




        private (List<double> fpr, List<double> tpr, List<double> thresholds) CalculateRocCurve(
            MLContext mlContext,
            ITransformer model,
            IDataView testData)
        {
            try
            {

                var predictions = model.Transform(testData);
                var predictionData = mlContext.Data.CreateEnumerable<BinaryPredictionWithLabel>(predictions, reuseRowObject: false).ToList();

                if (predictionData.Count == 0)
                {
                    Console.WriteLine("Warning: No predictions available for ROC curve calculation");
                    return (new List<double>(), new List<double>(), new List<double>());
                }


                var sortedPredictions = predictionData.OrderByDescending(p => p.Probability).ToList();

                var fpr = new List<double>();
                var tpr = new List<double>();
                var thresholds = new List<double>();


                int totalPositives = sortedPredictions.Count(p => p.Label);
                int totalNegatives = sortedPredictions.Count(p => !p.Label);

                if (totalPositives == 0 || totalNegatives == 0)
                {
                    Console.WriteLine("Warning: Dataset contains only one class, cannot calculate ROC curve");
                    return (new List<double>(), new List<double>(), new List<double>());
                }


                fpr.Add(0.0);
                tpr.Add(0.0);
                thresholds.Add(1.0);

                int truePositives = 0;
                int falsePositives = 0;


                for (int i = 0; i < sortedPredictions.Count; i++)
                {
                    var pred = sortedPredictions[i];


                    if (pred.Label)
                        truePositives++;
                    else
                        falsePositives++;


                    double currentTpr = (double)truePositives / totalPositives;
                    double currentFpr = (double)falsePositives / totalNegatives;


                    if (i == sortedPredictions.Count - 1 ||
                        Math.Abs(pred.Probability - sortedPredictions[i + 1].Probability) > 1e-10)
                    {
                        tpr.Add(currentTpr);
                        fpr.Add(currentFpr);
                        thresholds.Add(pred.Probability);
                    }
                }


                if (fpr[fpr.Count - 1] != 1.0 || tpr[tpr.Count - 1] != 1.0)
                {
                    fpr.Add(1.0);
                    tpr.Add(1.0);
                    thresholds.Add(0.0);
                }

                Console.WriteLine($"ROC curve calculated with {fpr.Count} points");
                return (fpr, tpr, thresholds);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculating ROC curve: {ex.Message}");
                return (new List<double>(), new List<double>(), new List<double>());
            }
        }




        private (List<double> precision, List<double> recall, List<double> thresholds, double averagePrecision) CalculatePrecisionRecallCurve(
            MLContext mlContext,
            ITransformer model,
            IDataView testData)
        {
            try
            {

                var predictions = model.Transform(testData);
                var predictionData = mlContext.Data.CreateEnumerable<BinaryPredictionWithLabel>(predictions, reuseRowObject: false).ToList();

                if (predictionData.Count == 0)
                {
                    Console.WriteLine("Warning: No predictions available for PR curve calculation");
                    return (new List<double>(), new List<double>(), new List<double>(), 0.0);
                }


                var sortedPredictions = predictionData.OrderByDescending(p => p.Probability).ToList();

                var precision = new List<double>();
                var recall = new List<double>();
                var thresholds = new List<double>();


                int totalPositives = sortedPredictions.Count(p => p.Label);

                if (totalPositives == 0)
                {
                    Console.WriteLine("Warning: Dataset contains no positive samples, cannot calculate PR curve");
                    return (new List<double>(), new List<double>(), new List<double>(), 0.0);
                }





                precision.Add(1.0);
                recall.Add(0.0);
                thresholds.Add(1.0);

                int truePositives = 0;
                int falsePositives = 0;


                for (int i = 0; i < sortedPredictions.Count; i++)
                {
                    var pred = sortedPredictions[i];


                    if (pred.Label)
                        truePositives++;
                    else
                        falsePositives++;


                    double currentRecall = (double)truePositives / totalPositives;
                    double currentPrecision = (truePositives + falsePositives) > 0
                        ? (double)truePositives / (truePositives + falsePositives)
                        : 0.0;


                    if (i == sortedPredictions.Count - 1 ||
                        Math.Abs(pred.Probability - sortedPredictions[i + 1].Probability) > 1e-10)
                    {
                        precision.Add(currentPrecision);
                        recall.Add(currentRecall);
                        thresholds.Add(pred.Probability);
                    }
                }



                double ap = 0.0;
                for (int i = 1; i < recall.Count; i++)
                {
                    double recallDiff = recall[i] - recall[i - 1];

                    ap += recallDiff * precision[i];
                }

                Console.WriteLine($"Precision-Recall curve calculated with {precision.Count} points");
                Console.WriteLine($"Average Precision (AP): {ap:F4}");
                return (precision, recall, thresholds, ap);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculating Precision-Recall curve: {ex.Message}");
                return (new List<double>(), new List<double>(), new List<double>(), 0.0);
            }
        }

        private class BinaryPredictionWithLabel
        {
            public bool Label { get; set; }
            public bool PredictedLabel { get; set; }
            public float Score { get; set; }
            public float Probability { get; set; }
        }

        private IDataView PrepareData(IDataView labeledData, string[] featureNames)
        {
            if (labeledData.Schema.GetColumnOrNull("Features").HasValue)
            {
                return labeledData;
            }
            else
            {
                return _mlContext.Transforms.Concatenate("Features", featureNames)
                    .Fit(labeledData)
                    .Transform(labeledData);
            }
        }

        private void LimitTrainers(BinaryExperimentSettings experimentSettings, int maxModels)
        {
            try
            {
                var type = experimentSettings.GetType();
                var maxModelsField = type.GetField("MaxModels", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

                if (maxModelsField != null)
                {
                    if (maxModelsField.FieldType == typeof(uint))
                    {
                        maxModelsField.SetValue(experimentSettings, (uint)maxModels);
                    }
                    else if (maxModelsField.FieldType == typeof(int))
                    {
                        maxModelsField.SetValue(experimentSettings, maxModels);
                    }
                    else
                    {
                        maxModelsField.SetValue(experimentSettings, Convert.ChangeType(maxModels, maxModelsField.FieldType));
                    }
                    Console.WriteLine($"Set MaxModels to {maxModels}");
                }
                else
                {
                    Console.WriteLine($"Warning: MaxModels field not found");
                    Console.WriteLine($"Available fields on {type.Name}:");
                    foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
                    {
                        Console.WriteLine($"  - {field.Name} ({field.FieldType.Name}) DeclaringType={field.DeclaringType?.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting MaxModels: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}