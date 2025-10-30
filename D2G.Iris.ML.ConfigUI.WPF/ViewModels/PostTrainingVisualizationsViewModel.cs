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

        public PostTrainingVisualizationsViewModel(
            ConfusionMatrixViewModel confusionMatrixViewModel,
            RocCurveViewModel rocCurveViewModel,
            PrecisionRecallCurveViewModel precisionRecallCurveViewModel)
        {
            ConfusionMatrix = confusionMatrixViewModel;
            RocCurve = rocCurveViewModel;
            PrecisionRecallCurve = precisionRecallCurveViewModel;
            _currentModelType = ModelType.BinaryClassification;
        }

        #region Properties

        public ConfusionMatrixViewModel ConfusionMatrix { get; }

        public RocCurveViewModel RocCurve { get; }

        public PrecisionRecallCurveViewModel PrecisionRecallCurve { get; }

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




        public bool IsConfusionMatrixAvailable =>
            CurrentModelType == ModelType.BinaryClassification ||
            CurrentModelType == ModelType.MultiClassClassification;




        public bool IsRocCurveAvailable =>
            CurrentModelType == ModelType.BinaryClassification;




        public bool IsPrecisionRecallCurveAvailable =>
            CurrentModelType == ModelType.BinaryClassification;

        #endregion

        #region Methods






        public void UpdateConfusionMatrix(double[,] confusionMatrix, List<string> classLabels)
        {
            if (IsConfusionMatrixAvailable)
            {
                ConfusionMatrix.UpdateConfusionMatrix(confusionMatrix, classLabels);
                HasTrainingResults = true;
            }
        }




        public void UpdateConfusionMatrixWithMetrics(double[,] confusionMatrix, List<string> classLabels,
            double accuracy, double precision, double recall, double f1Score)
        {
            if (IsConfusionMatrixAvailable)
            {
                ConfusionMatrix.UpdateConfusionMatrixWithMetrics(confusionMatrix, classLabels, accuracy, precision, recall, f1Score);
                HasTrainingResults = true;
            }
        }








        public void UpdateRocCurve(List<double> fpr, List<double> tpr, List<double> thresholds, double aucScore)
        {
            if (IsRocCurveAvailable)
            {
                RocCurve.UpdateRocCurve(fpr, tpr, thresholds, aucScore);
                HasTrainingResults = true;
            }
        }








        public void UpdatePrecisionRecallCurve(List<double> precision, List<double> recall, List<double> thresholds, double averagePrecision)
        {
            if (IsPrecisionRecallCurveAvailable)
            {
                PrecisionRecallCurve.UpdatePrecisionRecallCurve(precision, recall, thresholds, averagePrecision);
                HasTrainingResults = true;
            }
        }




        private void UpdateVisualizationAvailability()
        {
            OnPropertyChanged(nameof(IsConfusionMatrixAvailable));
            OnPropertyChanged(nameof(IsRocCurveAvailable));
            OnPropertyChanged(nameof(IsPrecisionRecallCurveAvailable));


            if (!IsConfusionMatrixAvailable)
            {
                ConfusionMatrix.Clear();
            }

            if (!IsRocCurveAvailable)
            {
                RocCurve.Clear();
            }

            if (!IsPrecisionRecallCurveAvailable)
            {
                PrecisionRecallCurve.Clear();
            }
        }




        public void ClearAll()
        {
            ConfusionMatrix.Clear();
            RocCurve.Clear();
            PrecisionRecallCurve.Clear();
            HasTrainingResults = false;
            SelectedVisualizationIndex = 0;
        }




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
                RocCurve?.Dispose();
                PrecisionRecallCurve?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
