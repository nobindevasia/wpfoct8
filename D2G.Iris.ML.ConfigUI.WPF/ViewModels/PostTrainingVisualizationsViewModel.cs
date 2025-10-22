using System;
using System.Collections.Generic;
using D2G.Iris.ML.Core.Enums;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class PostTrainingVisualizationsViewModel : BaseViewModel
    {
        private int _selectedVisualizationIndex;
        private ModelType _currentModelType;
        private bool _hasTrainingResults;

        public PostTrainingVisualizationsViewModel()
        {
            ConfusionMatrix = new ConfusionMatrixViewModel();
            _currentModelType = ModelType.BinaryClassification;
        }

        #region Properties

        public ConfusionMatrixViewModel ConfusionMatrix { get; }

        public int SelectedVisualizationIndex
        {
            get => _selectedVisualizationIndex;
            set => SetProperty(ref _selectedVisualizationIndex, value);
        }

        public ModelType CurrentModelType
        {
            get => _currentModelType;
            set
            {
                if (SetProperty(ref _currentModelType, value))
                {
                    UpdateVisualizationAvailability();
                }
            }
        }

        public bool HasTrainingResults
        {
            get => _hasTrainingResults;
            set => SetProperty(ref _hasTrainingResults, value);
        }

        /// <summary>
        /// Determines if confusion matrix is available for current model type
        /// </summary>
        public bool IsConfusionMatrixAvailable =>
            CurrentModelType == ModelType.BinaryClassification ||
            CurrentModelType == ModelType.MultiClassClassification;

        #endregion

        #region Methods

        /// <summary>
        /// Updates the confusion matrix visualization
        /// </summary>
        /// <param name="confusionMatrix">2D array of confusion matrix values</param>
        /// <param name="classLabels">List of class labels</param>
        public void UpdateConfusionMatrix(double[,] confusionMatrix, List<string> classLabels)
        {
            if (IsConfusionMatrixAvailable)
            {
                ConfusionMatrix.UpdateConfusionMatrix(confusionMatrix, classLabels);
                HasTrainingResults = true;
            }
        }

        /// <summary>
        /// Updates the confusion matrix visualization with pre-calculated metrics
        /// </summary>
        public void UpdateConfusionMatrixWithMetrics(double[,] confusionMatrix, List<string> classLabels,
            double accuracy, double precision, double recall, double f1Score)
        {
            if (IsConfusionMatrixAvailable)
            {
                ConfusionMatrix.UpdateConfusionMatrixWithMetrics(confusionMatrix, classLabels, accuracy, precision, recall, f1Score);
                HasTrainingResults = true;
            }
        }

        /// <summary>
        /// Updates visualization availability based on model type
        /// </summary>
        private void UpdateVisualizationAvailability()
        {
            OnPropertyChanged(nameof(IsConfusionMatrixAvailable));

            // Clear visualizations if they're not available for current model type
            if (!IsConfusionMatrixAvailable)
            {
                ConfusionMatrix.Clear();
            }
        }

        /// <summary>
        /// Clears all visualization data
        /// </summary>
        public void ClearAll()
        {
            ConfusionMatrix.Clear();
            HasTrainingResults = false;
            SelectedVisualizationIndex = 0;
        }

        /// <summary>
        /// Sets the model type (used when training starts)
        /// </summary>
        public void SetModelType(ModelType modelType)
        {
            CurrentModelType = modelType;
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ConfusionMatrix?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
