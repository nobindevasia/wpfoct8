using System;
using System.Collections.Generic;
using System.Linq;
using D2G.Iris.ML.Core.Enums;
using D2G.Iris.ML.Utils;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class ParameterDialogViewModel : BaseViewModel
    {
        private string _parameterName = "";
        private string _parameterValueString = "";
        private object? _parameterValue;
        private string _valueHint = "";
        private string _errorMessage = "";
        private bool _hasError;
        private readonly string _algorithmName;
        private readonly ModelType _modelType;
        private Type? _selectedParameterType;
        private bool _isBooleanParameter;
        private List<string> _booleanOptions = new List<string> { "true", "false" };

        public ParameterDialogViewModel(string algorithmName, ModelType modelType)
        {
            _algorithmName = algorithmName;
            _modelType = modelType;
            LoadAvailableParameters();
        }

        #region Properties

        public string ParameterName
        {
            get => _parameterName;
            set
            {
                if (SetProperty(ref _parameterName, value))
                {
                    OnParameterNameChanged();
                    OnPropertyChanged(nameof(IsValid));
                }
            }
        }

        public string ParameterValueString
        {
            get => _parameterValueString;
            set
            {
                if (SetProperty(ref _parameterValueString, value))
                {
                    ValidateAndSetValue();
                    OnPropertyChanged(nameof(IsValid));
                }
            }
        }

        public object? ParameterValue
        {
            get => _parameterValue;
            private set => SetProperty(ref _parameterValue, value);
        }

        public string ValueHint
        {
            get => _valueHint;
            private set => SetProperty(ref _valueHint, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        public bool HasError
        {
            get => _hasError;
            private set => SetProperty(ref _hasError, value);
        }

        public List<string> AvailableParameters { get; private set; } = new List<string>();

        public string AlgorithmTooltip { get; private set; } = "";

        public bool IsValid => !HasError && !string.IsNullOrWhiteSpace(ParameterName) && !string.IsNullOrWhiteSpace(ParameterValueString);

        public bool IsBooleanParameter
        {
            get => _isBooleanParameter;
            private set => SetProperty(ref _isBooleanParameter, value);
        }

        public List<string> BooleanOptions
        {
            get => _booleanOptions;
            private set => SetProperty(ref _booleanOptions, value);
        }

        #endregion

        private void LoadAvailableParameters()
        {
            try
            {
                var optionsType = AlgorithmRegistry.GetOptionsType(_algorithmName, _modelType);
                if (optionsType != null)
                {
                    AvailableParameters = ParameterHelper.GetParameterDisplayList(optionsType);
                    AlgorithmTooltip = ParameterHelper.CreateParameterTooltip(optionsType);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading parameters: {ex.Message}");
            }
        }

        private void OnParameterNameChanged()
        {
            ClearError();

            try
            {
                var optionsType = AlgorithmRegistry.GetOptionsType(_algorithmName, _modelType);
                if (optionsType == null) return;

                var actualParameterName = ExtractParameterName(_parameterName);


                var property = optionsType.GetProperty(actualParameterName);
                var field = optionsType.GetField(actualParameterName);

                if (property != null)
                {
                    _selectedParameterType = property.PropertyType;
                    var underlyingType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                    IsBooleanParameter = underlyingType == typeof(bool);
                    ValueHint = ParameterHelper.GetValueHint(property.PropertyType);


                    if (string.IsNullOrEmpty(ParameterValueString) && IsBooleanParameter)
                    {
                        ParameterValueString = "false";
                    }
                    else if (!IsBooleanParameter)
                    {
                        ParameterValueString = "";
                    }
                }
                else if (field != null)
                {
                    _selectedParameterType = field.FieldType;
                    var underlyingType = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;
                    IsBooleanParameter = underlyingType == typeof(bool);
                    ValueHint = ParameterHelper.GetValueHint(field.FieldType);


                    if (string.IsNullOrEmpty(ParameterValueString) && IsBooleanParameter)
                    {
                        ParameterValueString = "false";
                    }
                    else if (!IsBooleanParameter)
                    {
                        ParameterValueString = "";
                    }
                }
                else
                {
                    _selectedParameterType = null;
                    IsBooleanParameter = false;
                    ValueHint = "Enter a value";
                }
            }
            catch (Exception ex)
            {
                SetError($"Error getting parameter info: {ex.Message}");
            }
        }

        private void ValidateAndSetValue()
        {
            ClearError();

            if (string.IsNullOrWhiteSpace(ParameterValueString))
            {
                ParameterValue = null;
                return;
            }

            if (_selectedParameterType == null)
            {

                TryBasicParsing();
                return;
            }

            try
            {
                ParameterValue = ParameterHelper.ConvertParameterValue(ParameterValueString, _selectedParameterType);
            }
            catch (Exception ex)
            {
                SetError($"Invalid value for {ParameterHelper.GetFriendlyTypeName(_selectedParameterType)}: {ex.Message}");

                TryBasicParsing();
            }
        }

        private void TryBasicParsing()
        {
            try
            {

                if (int.TryParse(ParameterValueString, out int intValue))
                    ParameterValue = intValue;
                else if (double.TryParse(ParameterValueString, out double doubleValue))
                    ParameterValue = doubleValue;
                else if (bool.TryParse(ParameterValueString, out bool boolValue))
                    ParameterValue = boolValue;
                else
                    ParameterValue = ParameterValueString;
            }
            catch
            {
                ParameterValue = ParameterValueString;
            }
        }

        private void SetError(string message)
        {
            ErrorMessage = message;
            HasError = true;
            OnPropertyChanged(nameof(IsValid));
        }

        private void ClearError()
        {
            ErrorMessage = "";
            HasError = false;
            OnPropertyChanged(nameof(IsValid));
        }

        private string ExtractParameterName(string displayText)
        {

            if (string.IsNullOrEmpty(displayText))
                return displayText;

            var parenIndex = displayText.IndexOf(" (");
            return parenIndex > 0 ? displayText.Substring(0, parenIndex) : displayText;
        }
    }
}