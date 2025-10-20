using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using D2G.Iris.ML.ConfigUI.WPF.Services;
using D2G.Iris.ML.ConfigUI.WPF.ViewModels;
using D2G.Iris.ML.Core.Interfaces;
using D2G.Iris.ML.Configuration;
using D2G.Iris.ML.Data;
using D2G.Iris.ML.Training;
using Microsoft.ML;
using SciChart.Charting.Visuals;

namespace D2G.Iris.ML.ConfigUI.WPF
{
    public partial class App : Application
    {
        private ServiceProvider? _serviceProvider;

        protected override void OnStartup(StartupEventArgs e)
        {
            SciChartSurface.SetRuntimeLicenseKey("dkBLL7cFRf2kjrIDBv5UnMIOlzcrRi44w3/vQzJbsvsQ1s/F7BAmoG368pM381psDnVdB7vAyW2uH1Hs9A9gN/9nJIL9mQ4RHgln" +
                                                 "e+3ozSqZ+p7gF3tYGzMoDzG6noqUxpROjhpJh4gLxOt0CJxnp4ppf2EnxjIK70IuYIJHWUxvL91WPMjCoWYNAkN8V0JBNC86KRpj9G" +
                                                 "U6C7d1AQIp7YRC5pR2DhlxQILJ095mWDYiewPQ2GO1G26CMfMWxmevohKyGZkQgtzJTynusv7E7b/NGnnlKrGlvGujlDoMuRiRH9XkA" +
                                                 "fGCQm4bWnixsuHX3fIs7lS4IRKL1AWSPVheYnyPjrhnQPswbTVUBBMeXwgv22mVAfABvu1fxFWBO+WHjcC57GLWww/dp8KfI3Pa6oXy" +
                                                 "lI+KvgaXKysk/g3uc7TpwiNhNNMbHVy80/GIrYvhO3qmc0Uobdb0k9xENdLyL5gRHWgEoKmkban7f4QCsDrIoh2klh38eoE=");

            var services = new ServiceCollection();

            services.AddSingleton<IConfigurationService, ConfigurationService>();
            services.AddSingleton<IDatabaseSchemaLoader, DatabaseSchemaLoader>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IConfigManager, ConfigManager>();

            services.AddSingleton<ISqlHandler>(provider => new SqlHandler("DefaultTable"));
            services.AddSingleton<IDataLoader, DatabaseDataLoader>();
            services.AddSingleton<IDataProcessor, DataProcessor>();

            services.AddSingleton<MLContext>();
            services.AddSingleton<IModelTrainerFactory, ModelTrainerFactory>();

            services.AddSingleton<IDatabaseAnalyticsService, DatabaseAnalyticsService>();

            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<GeneralSettingsViewModel>();
            services.AddTransient<DatabaseSettingsViewModel>();
            services.AddTransient<InputFieldsViewModel>();
            services.AddTransient<TrainingParametersViewModel>();

            services.AddTransient<DataBalancingViewModel>();
            services.AddTransient<FeatureEngineeringViewModel>();
            services.AddTransient<AutoMLSettingsViewModel>();

            services.AddTransient<ExploratoryDataAnalysisViewModel>();
            services.AddTransient<VisualisationViewModel>();
            services.AddTransient<OutlierDetectionViewModel>();

            services.AddTransient<TrainingLogsViewModel>();

            _serviceProvider = services.BuildServiceProvider();

            var mainWindowViewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
            var mainWindow = new MainWindow(mainWindowViewModel);

            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _serviceProvider?.Dispose();
            base.OnExit(e);
        }
    }
}
