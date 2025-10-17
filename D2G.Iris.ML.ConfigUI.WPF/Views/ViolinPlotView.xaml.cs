using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using D2G.Iris.ML.ConfigUI.WPF.ViewModels;
using Microsoft.Web.WebView2.Wpf;

namespace D2G.Iris.ML.ConfigUI.WPF.Views
{
    /// <summary>
    /// Interaction logic for ViolinPlotView.xaml
    /// </summary>
    public partial class ViolinPlotView : UserControl
    {
        private ViolinPlotViewModel? _viewModel;
        private bool _webViewInitialized;

        public ViolinPlotView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            DataContextChanged += OnDataContextChanged;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                await EnsureWebViewAsync();
                UpdatePlotHtml(_viewModel?.PlotHtml);
            });
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _viewModel = null;
            }
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _viewModel = DataContext as ViolinPlotViewModel;

            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            }

            _ = Dispatcher.InvokeAsync(async () =>
            {
                await EnsureWebViewAsync();
                UpdatePlotHtml(_viewModel?.PlotHtml);
            });
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ViolinPlotViewModel.PlotHtml))
            {
                return;
            }

            var html = _viewModel?.PlotHtml;
            _ = Dispatcher.InvokeAsync(async () =>
            {
                await EnsureWebViewAsync();
                UpdatePlotHtml(html);
            });
        }

        private async Task EnsureWebViewAsync()
        {
            if (_webViewInitialized)
            {
                return;
            }

            await PlotWebView.EnsureCoreWebView2Async();
            _webViewInitialized = true;
        }

        private void UpdatePlotHtml(string? htmlContent)
        {
            if (!_webViewInitialized)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(htmlContent))
            {
                PlotWebView.NavigateToString("<html><body style='font-family:Segoe UI, sans-serif;color:#666;padding:24px;'>Generate a violin plot to view results.</body></html>");
            }
            else
            {
                PlotWebView.NavigateToString(htmlContent);
            }
        }
    }
}
