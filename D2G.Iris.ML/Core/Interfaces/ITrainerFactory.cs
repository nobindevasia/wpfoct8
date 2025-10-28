using Microsoft.ML;
using D2G.Iris.ML.Core.Enums;
using D2G.Iris.ML.Core.Models;

namespace D2G.Iris.ML.Core.Interfaces
{
    public interface ITrainerFactory
    {
        IEstimator<ITransformer> GetTrainer(ModelType modelType, TrainingParameters parameters);
    }
}
