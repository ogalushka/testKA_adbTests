﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Navigation;
using System.Xml;

namespace Keyapp
{
    public class SearchCommand
    {
        const string chromeIconId = "Chrome";
        const string searchBoxId = "com.android.chrome:id/search_box_text";
        const string acceptTermsButtonId = "com.android.chrome:id/terms_accept";
        const string noThanksButtonId = "com.android.chrome:id/negative_button";
        const string ipSearchString = "Your public IP address";

        const string searchFieldXPath = $"//*[@resource-id='{searchBoxId}']";
        const string acceptTearmsButtonXpath = $"//*[@resource-id='{acceptTermsButtonId}']";
        const string noThanksButtonXpath = $"//*[@resource-id='{noThanksButtonId}']";
        const string homeButtonXpath = "//*[@resource-id='com.android.chrome:id/home_button']";

        public CommandRunner commandRunner;

        public SearchCommand(CommandRunner commandRunner)
        {
            this.commandRunner = commandRunner;
        }

        public async Task<string> Execute(string searchQuerry)
        {
            // TODO check if adb installed and error;
            await CloseAll();
            await StartChrome();

            var rootNode = await WaitForAny(new[] {
                searchFieldXPath,
                acceptTearmsButtonXpath,
                noThanksButtonXpath,
                homeButtonXpath
                });

            var actionTaken = await CompleteChromeSetup(rootNode);
            if (actionTaken)
            {
                rootNode = await WaitForAny(new[] { searchFieldXPath, homeButtonXpath });
            }

            actionTaken = await OpenHomePage(rootNode);
            if (actionTaken)
            {
                rootNode = await WaitFor(searchFieldXPath);
            }

            await PerformSearch(rootNode, searchQuerry);
            rootNode = await WaitFor("//*[@class='android.webkit.WebView']");
            var searchResult = FindIpValue(rootNode);

            return searchResult;
        }

        public async Task CloseAll()
        {
            var sizeInfo = await commandRunner.RunCommand("adb shell wm size");
            var size = sizeInfo.Substring(sizeInfo.LastIndexOf(" ")).Split('x');
            var x = int.Parse(size[0]) / 2;
            var y1 = int.Parse(size[1]) / 3;
            var y0 = y1 * 2;

            await commandRunner.RunCommand("adb shell input keyevent KEYCODE_HOME");
            await commandRunner.RunCommand("adb shell input keyevent KEYCODE_APP_SWITCH");
            while ((await GetRoot()).SelectSingleNode("//*[@resource-id='com.google.android.apps.nexuslauncher:id/snapshot']") != null) 
            {
                await commandRunner.RunCommand($"adb shell input swipe {x} {y0} {x} {y1}");
            }
            await commandRunner.RunCommand("adb shell input keyevent KEYCODE_HOME");
        }

        private async Task StartChrome()
        {
            var xpath = $"//*[@content-desc='{chromeIconId}']";
            var rootNode = await WaitFor(xpath);

            var chromeIcon = rootNode.SelectSingleNode(xpath);
            if (chromeIcon != null)
            {
                await TapOnNode(chromeIcon);
            }
            else
            {
                throw new AppException("Can't find chrome icon on screen");
            }
        }

        private async Task<bool> CompleteChromeSetup(XmlElement rootNode)
        {
            var actionsTaken = false;

            var acceptTermsButton = rootNode.SelectSingleNode(acceptTearmsButtonXpath);
            if (acceptTermsButton != null)
            {
                //TODO replace with common one
                var boundsString = acceptTermsButton.Attributes!["bounds"]!.Value!;

                var values = boundsString.Replace("][", ",").Replace("[", "").Replace("]", "").Split(",").Select(s => int.Parse(s)).ToArray();
                await commandRunner.RunCommand($"adb shell input tap {values[0]} {values[1]}");

                rootNode = await WaitFor(noThanksButtonXpath);
                actionsTaken = true;
            }

            var noThanksNode = rootNode.SelectSingleNode(noThanksButtonXpath);
            if (noThanksNode != null)
            {
                await TapOnNode(rootNode, noThanksButtonXpath);
                actionsTaken = true;
            }
            return actionsTaken;
        }

        private async Task<bool> OpenHomePage(XmlElement rootNode)
        {
            var searchField = rootNode.SelectSingleNode(searchFieldXPath);
            if (searchField == null)
            {
                await TapOnNode(rootNode, homeButtonXpath);
                return true;
            }

            return false;
        }

        private async Task PerformSearch(XmlElement rootElement, string querryText)
        {
            var searchField = FindSearchField(rootElement);

            await TapOnNode(searchField);
            await commandRunner.RunCommand($"adb shell input text {querryText.Replace(" ", "%s")}");
            await commandRunner.RunCommand($"adb shell input keyevent KEYCODE_ENTER");
        }

        private Task<XmlElement> WaitFor(string xpath, int step = 500, int timeout = 10000)
        {
            return WaitForAny(new[] { xpath }, step, timeout);
        }

        private async Task<XmlElement> WaitForAny(string[] xpaths, int step = 500, int timeout = 10000)
        {
            for (int timePassed = 0; timePassed < timeout; timePassed += step)
            {
                await Task.Delay(step);
                var rootNode = await GetRoot();
                foreach (var xpath in xpaths) {
                    var targetNode = rootNode.SelectSingleNode(xpath);
                    if (targetNode != null)
                    {
                        return rootNode;
                    }
                }
            }

            throw new AppException($"Timeout while waiting for any of '{string.Join(",", xpaths)}' nodes");
        }

        private async Task<XmlElement> GetRoot()
        {
            // TODO parse location for pull
            await commandRunner.RunCommand("adb shell uiautomator dump && adb pull /sdcard/window_dump.xml");
            var file = await File.ReadAllTextAsync("./window_dump.xml");

            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(file);

            if (xmlDoc.DocumentElement == null)
            {
                throw new AppException($"Failed to parse uiautomatordump contents: {file}");
            }

            return xmlDoc.DocumentElement;
        }

        private XmlNode FindSearchField(XmlElement rootNode)
        {
            var namedSearch = rootNode.SelectSingleNode(searchFieldXPath);
            if (namedSearch != null)
            {
                return namedSearch;
            }

            throw new AppException("Can't find search text field");
        }

        private string FindIpValue(XmlElement rootNode)
        {
            var ipTitleNode = rootNode.SelectSingleNode($"//*[@text='{ipSearchString}']");
            if (ipTitleNode == null)
            {
                return "";
            }
            var ipNode = ipTitleNode?.PreviousSibling;
            if (ipNode == null)
            {
                return "";
            }

            var ip = ipNode.Attributes!["text"]?.Value;
            return ip ?? "";
        }

        private async Task TapOnNode(XmlElement rootNode, string xpath)
        {
            var node = rootNode.SelectSingleNode(xpath);
            if (node != null)
            {
                await TapOnNode(node);
            }
            else
            {
                throw new AppException($"Can't find node at {xpath} to click on");
            }
        }

        private async Task TapOnNode(XmlNode node)
        {
            var boundsString = node.Attributes!["bounds"]!.Value!;

            if (string.IsNullOrEmpty(boundsString))
            {
                throw new AppException($"Can't tap on node {node.Name} bounds are not defined");
            }

            if (boundsString == "[0,0][0,0]")
            {
                throw new AppException($"Clicking on nodes of screen is not supported");
            }

            var values = boundsString.Replace("][", ",").Replace("[", "").Replace("]", "").Split(",").Select(s => int.Parse(s)).ToArray();
            if (values.Length != 4)
            {
                throw new AppException($"Failed to parse node position expected [x0, y0][x1, y1] format received: {boundsString}");
            }

            var x0 = values[0];
            var y0 = values[1];
            var x1 = values[2];
            var y1 = values[3];
            var centerX = x0 + ((x1 - x0) / 2);
            var centerY = y0 + ((y1 - y0) / 2);

            await commandRunner.RunCommand($"adb shell input tap {centerX} {centerY}");
        }
    }
}
