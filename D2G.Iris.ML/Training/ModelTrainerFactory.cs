using System;
using Microsoft.ML;
using D2G.Iris.ML.Core.Enums;
using D2G.Iris.ML.Core.Interfaces;

namespace D2G.Iris.ML.Training
{
    public class ModelTrainerFactory : IModelTrainerFactory
    {
        private readonly MLContext _mlContext;
        private readonly ITrainerFactory _trainerFactory;

        public ModelTrainerFactory(MLContext mlContext, ITrainerFactory trainerFactory)
        {
            _mlContext = mlContext;
            _trainerFactory = trainerFactory;
        }

        public IModelTrainer CreateTrainer(ModelType modelType)
        {
            return modelType switch
            {
                ModelType.BinaryClassification => new BinaryClassificationTrainer(_mlContext, _trainerFactory),
                ModelType.MultiClassClassification => new MultiClassClassificationTrainer(_mlContext, _trainerFactory),
                ModelType.Regression => new RegressionTrainer(_mlContext, _trainerFactory),
                _ => throw new ArgumentException($"Unsupported model type: {modelType}")
            };
        }
    }
}