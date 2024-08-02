using System;
using System.Windows;

namespace SteakCreateTool
{
    public partial class EditCountDialog : Window
    {
        public int NewCount { get; private set; }

        public EditCountDialog(DateTime date, int count)
        {
            InitializeComponent();
            DateTextBox.Text = date.ToShortDateString();
            CountTextBox.Text = count.ToString();
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(CountTextBox.Text, out var count))
            {
                NewCount = count;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Invalid count value.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}