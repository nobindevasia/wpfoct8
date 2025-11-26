using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using D2G.Iris.ML.Core.Enums;
using D2G.Iris.ML.Core.Models;

namespace D2G.Iris.ML.ConfigUI.WPF.Services
{
    public class ConfigurationService : IConfigurationService
    {
        private readonly JsonSerializerOptions _serializerOptions;

        public ConfigurationService()
        {
            _serializerOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        public ModelConfig LoadConfiguration(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Configuration file not found.", filePath);
            }

            try
            {
                string jsonText = File.ReadAllText(filePath);
                var jsonData = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, ModelConfig>>(jsonText, _serializerOptions);

                if (jsonData == null || !jsonData.ContainsKey("modelConfig"))
                {
                    throw new InvalidOperationException("Invalid configuration format.");
                }

                return jsonData["modelConfig"];
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Error parsing configuration file: {ex.Message}", ex);
            }
            catch (Exception ex) when (ex is not FileNotFoundException && ex is not InvalidOperationException)
            {
                throw new InvalidOperationException($"Error loading configuration: {ex.Message}", ex);
            }
        }

        public void SaveConfiguration(ModelConfig config, string filePath)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            try
            {
                var serializableConfig = new System.Collections.Generic.Dictionary<string, ModelConfig>
                {
                    { "modelConfig", config }
                };

                string jsonText = JsonSerializer.Serialize(serializableConfig, _serializerOptions);
                File.WriteAllText(filePath, jsonText);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error saving configuration: {ex.Message}", ex);
            }
        }

        public bool ValidateConfiguration(ModelConfig config)
        {
            if (config == null)
                return false;

            // Target field is only required for supervised learning (not clustering)
            if (config.ModelType != ModelType.Clustering)
            {
                if (string.IsNullOrWhiteSpace(config.TargetField))
                    return false;
            }

            if (config.Database == null)
                return false;

            if (string.IsNullOrWhiteSpace(config.Database.Server) ||
                string.IsNullOrWhiteSpace(config.Database.Database) ||
                string.IsNullOrWhiteSpace(config.Database.TableName))
                return false;

            if (config.TrainingParameters == null ||
                string.IsNullOrWhiteSpace(config.TrainingParameters.Algorithm))
                return false;

            return true;
        }
    }
}