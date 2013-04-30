﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Xml.Serialization;
using CoCEd.Common;
using CoCEd.Model;
using CoCEd.ViewModel;

namespace CoCEd
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
#if !DEBUG
            DispatcherUnhandledException += OnDispatcherUnhandledException;
#endif
            Initialize();
        }

        void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            DispatcherUnhandledException -= OnDispatcherUnhandledException;

            MessageBox.Show(
                "An error occured and the application is going to exit.\n\n The error below will be saved as CoCEd.log. Please report it on CoC's forums (you can also use Ctrl+C):\n" + e.Exception.ToString(),
                "Unexpected error.", MessageBoxButton.OK, MessageBoxImage.Error);

            Logger.Error(e.Exception);
            Shutdown();
        }

        void Initialize()
        {
            var xmlResult = XmlData.LoadXml();
            switch (xmlResult)
            {
                case XmlLoadingResult.Success:
                    break;

                case XmlLoadingResult.MissingFile:
                    MessageBox.Show("Could not find the CoCEd.xml file.", "Fatal error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                    return;

                case XmlLoadingResult.NoPermission:
                    MessageBox.Show("The CoCEd.xml file was already in use or the application does not have the permission to access its own program folder.", "Fatal error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                    return;

                default:
                    throw new NotImplementedException();
            }

            VM.Create();

            FileManager.BuildPaths();
            var set = FileManager.CreateSet();
            if (FileManager.MoreThanOneFolderPath != null)
            {
                MessageBox.Show("There should be only one folder in:\n" + FileManager.MoreThanOneFolderPath, "Unexpected folder streucture", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            if (FileManager.MissingPermissionPath != null)
            {
                MessageBox.Show("Missing permission for IO on a folder or a file:\n" + FileManager.MissingPermissionPath, "IO Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

#if DEBUG
            var file = AutoLoad(set);
            //DebugStatuses(file);
            //RunTest(set);
            //ParsePerks();
#endif
        }


#if DEBUG
        static AmfFile AutoLoad(FileGroupSetVM set)
        {
            var file = set.StandardOfflineFiles.Files[0];
            VM.Instance.Load(file.Path);
            return file.Source;
        }

        static void PrintStatuses(AmfFile file)
        {
            foreach (AmfPair pair in file["statusAffects"])
            {
                int key = Int32.Parse(pair.Key);
                var name = pair.Value["statusAffectName"] as string;
                Debug.WriteLine(key.ToString("000") + " - " + name);
            }
        }

        static void RunTest(FileGroupSetVM set)
        {
            foreach (var first in set.StandardOfflineFiles.Files)
            {
                var outPath = "e:\\" + first.Source.Name + ".sol";
                first.Source.Test();
                first.Source.Save(outPath);

                var input = File.ReadAllBytes(first.Source.FilePath);
                var output = File.ReadAllBytes(outPath);
                if (input.Length != output.Length) throw new InvalidOperationException();
                for (int i = 0; i < input.Length; i++)
                {
                    if (input[i] != output[i]) throw new InvalidOperationException();
                }
            }
            MessageBox.Show("Success!");
        }

        void ParsePerks()
        {
            // player.createPerk("Bro Body",0,0,0,0,"You have the body of a muscled, sex-addicted hunk.  Your cock cannot be shorter than 10\", you're much lustier....
            var regexStr = @"player.createPerk\([\s]*QUOTE(?<name>[^QUOTE]*)QUOTE[\s]*,(?<value1>[\s\d\.]*),(?<value2>[\s\d\.]*),(?<value3>[\s\d\.]*),(?<value4>[\s\d\.]*),[\s]*QUOTE(?<desc>[^QUOTE]*)QUOTE";
            Regex regex = new Regex(regexStr.Replace("QUOTE", "\""), RegexOptions.IgnoreCase | RegexOptions.Compiled);

            List<XmlPerk> perks = new List<XmlPerk>();
            const string sourceDir = @"e:\downloads\coc\source\";
            foreach (var file in Directory.EnumerateFiles(sourceDir, "*.as", SearchOption.AllDirectories))
            {
                const string quoteSubst = "__$___@QUOTE@$___!__";
                var text = File.ReadAllText(file).Replace("\\\"", quoteSubst);
                foreach (Match match in regex.Matches(text))
                {
                    var name = match.Groups["name"].Value.Replace(quoteSubst, "\"");
                    if (perks.Any(x => x.Name == name)) continue;

                    var value1 = match.Groups["value1"].Value;
                    var value2 = match.Groups["value2"].Value;
                    var value3 = match.Groups["value3"].Value;
                    var value4 = match.Groups["value4"].Value;
                    var desc = match.Groups["desc"].Value.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t").Replace(quoteSubst, "\""); 

                    var perk = new XmlPerk 
                    { 
                        Name = name, 
                        Value1 = Double.Parse(value1.Trim(), CultureInfo.InvariantCulture),
                        Value2 = Double.Parse(value2.Trim(), CultureInfo.InvariantCulture),
                        Value3 = Double.Parse(value3.Trim(), CultureInfo.InvariantCulture),
                        Value4 = Double.Parse(value4.Trim(), CultureInfo.InvariantCulture),
                        Description = desc, 
                    };
                    perks.Add(perk);
                }
            }

            using (var s = File.OpenWrite(@"e:\perks.xml"))
            {
                XmlSerializer x = new XmlSerializer(typeof(XmlPerk[]));
                x.Serialize(s, perks.OrderBy(p => p.Name).ToArray());
            }
        }

        void ImportWiki()
        {
            // 1) Copy the item codes page from the wiki in OOo calc.
            // 2) Select all and save as e:\\CocItems.txt

            var builder = new StringBuilder();
            var lines = File.ReadAllLines("e:\\CocItems.txt");
            const string separator = "\t<!--============================================================================================================================================-->";
            const string itemFormat = "\t\t<Item ID=\"{0}\" Name=\"{1}\"/>";
            const string groupBeginFormat = "\t<{0}>";
            const string groupEndFormat = "\t</{0}>";
            string currentGroup = null;
            string currentItem = null;

            foreach (var line in lines)
            {
                if (line == "")
                {
                    if (currentGroup != null) builder.AppendFormat(groupEndFormat, currentGroup).AppendLine();
                    currentGroup = null;
                }
                else if (line.StartsWith("\t"))
                {
                    builder.AppendFormat(itemFormat, line.Trim('\t', ' '), currentItem).AppendLine();
                    currentItem = null;
                }
                else if (currentGroup == null)
                {
                    currentGroup = line.Replace(" ", "");
                    builder.AppendLine(separator).AppendFormat(groupBeginFormat, currentGroup).AppendLine();
                }
                else
                {
                    currentItem = line;
                }
            }

            File.WriteAllText("e:\\CocItems.xml", builder.ToString());
        }
#endif
    }
}
