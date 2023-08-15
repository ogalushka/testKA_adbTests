using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Xml;
using System.Xml.Serialization;

namespace Keyapp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
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
