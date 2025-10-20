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
            await EnsureWebViewAsync();
            UpdatePlotHtml(_viewModel?.PlotHtml);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }
            // Don't set _viewModel to null or reset _webViewInitialized
            // Keep the state so it works when returning to this view
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

            // Use BeginInvoke with high priority to ensure immediate update
            Dispatcher.BeginInvoke(async () =>
            {
                await EnsureWebViewAsync();
                UpdatePlotHtml(html);
            }, System.Windows.Threading.DispatcherPriority.Render);
        }

        private async Task EnsureWebViewAsync()
        {
            if (_webViewInitialized && PlotWebView?.CoreWebView2 != null)
            {
                return;
            }

            try
            {
                await PlotWebView.EnsureCoreWebView2Async();
                _webViewInitialized = true;
            }
            catch
            {
                // Initialization failed, will retry next time
                _webViewInitialized = false;
            }
        }

        private void UpdatePlotHtml(string? htmlContent)
        {
            if (!_webViewInitialized || PlotWebView?.CoreWebView2 == null)
            {
                return;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(htmlContent))
                {
                    PlotWebView.NavigateToString("<html><body style='font-family:Segoe UI, sans-serif;color:#666;padding:24px;'>Generate a violin plot to view results.</body></html>");
                }
                else
                {
                    PlotWebView.NavigateToString(htmlContent);
                }
            }
            catch
            {
                // WebView2 might not be ready yet, ignore
            }
        }
    }
}
