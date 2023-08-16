using System;
using System.Windows;

namespace Keyapp
{
    public partial class MainWindow : Window
    {
        private readonly CommandRunner commandRunner;
        public MainWindow()
        {
            InitializeComponent();
            commandRunner = new CommandRunner();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            OutputText.Text = "Executing ...";
            ExecuteButton.IsEnabled = false;
            var querryText = Querry.Text;

            try
            {
                var result = await new SearchCommand(commandRunner).Execute(querryText);
                OutputText.Text = result;
            }
            catch (AppException ex)
            {
                OutputText.Text = "";
                MessageBox.Show($"Error: {ex.Message}", "Error");
            }
            finally
            {
                ExecuteButton.IsEnabled = true;
            }
        }


        protected override void OnClosed(EventArgs e)
        {
            commandRunner.Dispose();
            base.OnClosed(e);
        }
    }
}
