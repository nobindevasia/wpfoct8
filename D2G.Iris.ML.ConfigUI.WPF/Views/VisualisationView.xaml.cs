using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using D2G.Iris.ML.ConfigUI.WPF.ViewModels;

namespace D2G.Iris.ML.ConfigUI.WPF.Views
{
    public partial class VisualisationView : UserControl
    {
        public VisualisationView()
        {
            InitializeComponent();
        }

        private void HistogramBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is HistogramViewModel histogram)
            {
                if (DataContext is VisualisationViewModel viewModel)
                {
                    viewModel.SelectHistogramCommand.Execute(histogram);
                }
            }
        }
    }
}
