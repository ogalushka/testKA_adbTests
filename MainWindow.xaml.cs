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

        //TODO Fail ??
        private async Task TapOnNode(XmlElement rootNode, string nodeId)
        {
            var node = rootNode.SelectSingleNode($"//*[@resource-id='{nodeId}']");
            if (node != null)
            {
                //TODO null checks?
                await TapOnNode(node);
            }
        }

        private async Task TapOnNode(XmlNode node)
        {
            var boundsString = node.Attributes!["bounds"]!.Value!;

            if (string.IsNullOrEmpty(boundsString))
            {
                throw new Exception($"Can't tap on node {node.Name} bounds are not defined");
            }

            if (boundsString == "[0,0][0,0]")
            {
                throw new Exception($"Clicking on nodes of screen is not supported");
            }

            var values = boundsString.Replace("][", ",").Replace("[", "").Replace("]", "").Split(",").Select(s => int.Parse(s)).ToArray();
            if (values.Length != 4)
            {
                throw new Exception($"Failed to parse node position expected [x0, y0][x1, y1] format received: {boundsString}");
            }

            var x0 = values[0];
            var y0 = values[1];
            var x1 = values[2];
            var y1 = values[3];
            var centerX = x0 + ((x1 - x0) / 2);
            var centerY = y0 + ((y1 - y0) / 2);

            await commandRunner.RunCommand($"adb shell input tap {centerX} {centerY}");
        }

        private async Task TapSearchField(XmlElement rootNode)
        {
            string searchBoxId = "com.android.chrome:id/search_box_text";
            string editTextClass = "android.widget.EditText";
            var namedSearch = rootNode.SelectSingleNode($"//*[@resource-id='{searchBoxId}']");

            if (namedSearch != null)
            {
                await TapOnNode(namedSearch);
                return;
            }

            var webPageSearch = rootNode.SelectSingleNode($"//*[@class='{editTextClass}']");
            if (webPageSearch != null)
            {
                await TapOnNode(webPageSearch);
                return;
            }
            // TODO error
        }

        private XmlNode? FindSearchFiled(XmlElement rootNode)
        {
            string searchBoxId = "com.android.chrome:id/search_box_text";
            string editTextClass = "android.widget.EditText";
            var namedSearch = rootNode.SelectSingleNode($"//*[@resource-id='{searchBoxId}']");

            if (namedSearch != null)
            {
                return namedSearch;
            }

            var webPageSearch = rootNode.SelectSingleNode($"//*[@class='{editTextClass}']");
            if (webPageSearch != null)
            {
                return webPageSearch;
            }

            return null;
        }

        private async Task<XmlElement> GetHierarchy()
        {
            var result = await commandRunner.RunCommand("adb exec-out uiautomator dump && adb pull /sdcard/window_dump.xml");
            var file = await File.ReadAllTextAsync("./window_dump.xml");

            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(file);

            if (xmlDoc.DocumentElement == null)
            {
                // TODO error
            }

            return xmlDoc.DocumentElement;
        }

        private async Task StartChrome()
        {
            string chromeIconId = "Chrome";

            var rootElement = await GetHierarchy();
            var chromeIcon = rootElement.SelectSingleNode($"//*[@content-desc='{chromeIconId}']");
            await TapOnNode(chromeIcon);
        }

        private async Task<string> PerformSearch(string querryText)
        {
            string searchBoxId = "com.android.chrome:id/search_box_text";
            string acceptTermsButtonId = "com.android.chrome:id/terms_accept";
            string noThanksButtonId = "com.android.chrome:id/negative_button";
            string ipSearchString = "Your public IP address";

            // TODO check if adb installed and error;
            // TODO parse location for pull
            // TODO delays??
            var rootElement = await GetHierarchy();

            // TODO replace with func and check
            // TODO refresh hierarchy after each action
            var acceptTermsButton = rootElement.SelectSingleNode("//*[@resource-id='com.android.chrome:id/terms_accept']");
            if (acceptTermsButton != null)
            {
                //TODO null checks?
                var boundsString = acceptTermsButton.Attributes!["bounds"]!.Value!;

                var values = boundsString.Replace("][", ",").Replace("[", "").Replace("]", "").Split(",").Select(s => int.Parse(s)).ToArray();
                await commandRunner.RunCommand($"adb shell input tap {values[0]} {values[1]}");
            }

            await TapOnNode(rootElement, noThanksButtonId);
            var searchField = FindSearchFiled(rootElement);
            if (searchField == null)
            {
                MessageBox.Show("Can't find search field");
                return "";
            }

            await TapOnNode(searchField);
            var searchText = searchField.Attributes!["text"]?.Value;
            // TODO when selecting homescreen searchfield 
            var textLength = string.IsNullOrEmpty(searchText) ? 0 : searchText.Length;
            for (var i = 0; i < textLength; i++)
            {
                // TODO optimize into a single call maybe
                await commandRunner.RunCommand($"adb shell input keyevent KEYCODE_DEL");
            }

            await commandRunner.RunCommand($"adb shell input text {querryText.Replace(" ", "%s")}");
            await commandRunner.RunCommand($"adb shell input keyevent KEYCODE_ENTER");

            await Task.Delay(2000);

            rootElement = await GetHierarchy();
            var ipTitleNode = rootElement.SelectSingleNode($"//*[@text='{ipSearchString}']");
            var ipNode = ipTitleNode?.PreviousSibling;
            var ip = ipNode?.Attributes!["text"]?.Value;

            return ip ?? "";
        }

        private async Task CloseAll()
        {
            var packagesListOutput = await commandRunner.RunCommand("adb shell pm list packages");
            var packagesList = packagesListOutput.Split("\r\n");
            foreach (var package in packagesList)
            {
                var name = package.Substring("package:".Length);
                await commandRunner.RunCommand($"adb shell am force-stop {name}");
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            OutputText.Text = "Executing ...";
            ExecuteButton.IsEnabled = false;
            var querryText = Querry.Text;


            try
            {
                // await PerformSearch(querryText);
                //await CloseAll();
                await StartChrome();
            }
            catch (Exception ex)
            {
                OutputText.Text = "";
                MessageBox.Show($"Error: {ex.Message}");
            }
            finally
            {
                ExecuteButton.IsEnabled = true;
            }
        }
    }
}
