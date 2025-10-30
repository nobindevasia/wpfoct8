using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace D2G.Iris.ML.ConfigUI.WPF.Views
{
    public partial class FeatureEngineeringView : UserControl
    {
        private static readonly Regex DecimalRegex = new Regex(@"^[0-9]*\.?[0-9]*$");

        public FeatureEngineeringView()
        {
            InitializeComponent();
        }

        private void DecimalTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is TextBox textBox)
            {

                string newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);


                e.Handled = !DecimalRegex.IsMatch(newText);
            }
        }

        private void DecimalTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (!DecimalRegex.IsMatch(text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }
    }
}