using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using D2G.Iris.ML.ConfigUI.WPF.ViewModels;
using Microsoft.Web.WebView2.Wpf;

namespace D2G.Iris.ML.ConfigUI.WPF.Views
{
    public partial class ViolinPlotView : UserControl
    {
        private ViolinPlotViewModel? _viewModel;
        private bool _webViewInitialized;
        private string? _pendingHtml;

        public ViolinPlotView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            DataContextChanged += OnDataContextChanged;
            IsVisibleChanged += OnIsVisibleChanged;
        }



        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await EnsureWebViewAsync();
            if (_pendingHtml != null)
            {
                await UpdatePlotHtmlAsync(_pendingHtml);
            }
            else
            {
                await UpdatePlotHtmlAsync(_viewModel?.PlotHtml);
            }
        }
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
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
                await UpdatePlotHtmlAsync(_viewModel?.PlotHtml);
            });
        }

        private async void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible && _pendingHtml != null)
            {
                await EnsureWebViewAsync();
                await UpdatePlotHtmlAsync(_pendingHtml);
                _pendingHtml = null;
            }
        }

        private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViolinPlotViewModel.PlotHtml))
            {
                var html = _viewModel?.PlotHtml;
                _pendingHtml = html;


                await Dispatcher.InvokeAsync(async () =>
                {
                    await EnsureWebViewAsync();
                    await UpdatePlotHtmlAsync(html);
                    if (IsVisible)
                    {
                        _pendingHtml = null;
                    }
                });
            }
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
                _webViewInitialized = false;
            }
        }

        private Task UpdatePlotHtmlAsync(string? htmlContent)
        {
            if (!_webViewInitialized || PlotWebView?.CoreWebView2 == null)
            {
                return Task.CompletedTask;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(htmlContent))
                {
                    PlotWebView.CoreWebView2.NavigateToString("<html><body style='font-family:Segoe UI, sans-serif;color:#666;padding:24px;'>Generate a violin plot to view results.</body></html>");
                }
                else
                {
                    PlotWebView.CoreWebView2.NavigateToString(htmlContent);
                }
            }
            catch
            {
            }

            return Task.CompletedTask;
        }
    }
}
