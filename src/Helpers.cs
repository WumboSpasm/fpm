using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

using Downloader;
using Microsoft.WindowsAPICodePack.Dialogs;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace FlashpointManagerCLI
{
    public class Component
    {
        public string Title { get; }
        public string Description { get; }
        public string ID { get; }
        public string URL { get; }
        public long Size { get; }
        public string Hash { get; }
        public string[] Depends { get; } = new string[] { };
        public bool Downloaded { get; } = false;
        public bool Outdated { get; } = false;

        private long oldSize = 0;
        public long SizeDifference { get => Size - oldSize; }

        public Component(XmlNode node)
        {
            // Title

            Title = GetAttribute(node, "title", true);

            // Description

            Description = GetAttribute(node, "description", true);

            // ID

            XmlNode workingNode = node.ParentNode;
            string id = GetAttribute(node, "id", true);

            while (workingNode != null && workingNode.Name != "list")
            {
                if (workingNode.Attributes != null && workingNode.Name != "list")
                {
                    id = $"{GetAttribute(workingNode, "id", true)}-{id}";
                }

                workingNode = workingNode.ParentNode;
            }

            ID = id;

            // URL

            var urlAttribute = node.OwnerDocument.GetElementsByTagName("list")[0].Attributes["url"];

            if (urlAttribute == null)
            {
                Program.SendMessage("Component list does not have a repository URL; please alert Flashpoint staff", true);
            }

            string url = urlAttribute.Value;
            if (!url.EndsWith("/")) url += "/";

            URL = url + $"{ID}.zip";

            // Size

            long size;

            if (long.TryParse(GetAttribute(node, "size", true), out size))
            {
                Size = size;
            }
            else
            {
                Program.SendMessage("Component has a size with non-numeric characters; please alert Flashpoint staff", true);
            }

            // Hash

            string hash = GetAttribute(node, "hash", true);

            if (hash.Length == 8)
            {
                Hash = hash;
            }
            else
            {
                Program.SendMessage("Component has a hash with an invalid length; please alert Flashpoint staff", true);
            }

            // Depends

            string depends = GetAttribute(node, "depends", false);

            if (depends.Length > 0) Depends = depends.Split(' ');

            // Downloaded, Outdated, SizeDifference

            string infoPath = Path.Combine(Common.Path, "Components", $"{ID}.txt");

            if (File.Exists(infoPath))
            {
                Downloaded = true;

                string[] infoHeader = File.ReadLines(infoPath).First().Split(' ');

                if (infoHeader.Length == 2)
                {
                    if (infoHeader[0] != Hash)
                    {
                        Outdated = true;

                        if (!long.TryParse(infoHeader[1], out oldSize))
                        {
                            Program.SendMessage($"Component {ID} has an invalid size in its info file header; some commands may display incorrect information until it is redownloaded");
                        }
                    }
                }
                else
                {
                    Program.SendMessage($"Component {ID} has an incorrectly-formatted info file header; some commands may not function correctly until it is redownloaded");
                }
            }
        }

        protected static string GetAttribute(XmlNode node, string attribute, bool throwError)
        {
            if (node.Attributes != null && node.Attributes[attribute] != null)
            {
                return node.Attributes[attribute].Value;
            }
            else if (throwError)
            {
                Program.SendMessage($"Component is missing required {attribute} attribute; please alert Flashpoint staff", true);
            }

            return "";
        }
    }

    public static class Common
    {
        public static string Path = Directory.GetCurrentDirectory();
        public static string Source = "http://localhost/components.xml";

        public static List<Component> Components = new List<Component>();

        public static string[] Args;
    }

    public static partial class Program
    {
        public static void CheckConfig()
        {
            if (!File.Exists("fpm.cfg"))
            {
                try
                {
                    File.Create("fpm.cfg").Close();
                }
                catch
                {
                    SendMessage("Could not create fpm.cfg (missing permissions?)", true);
                }
            }
        }

        public static void WriteConfig()
        {
            try
            {
                File.WriteAllLines("fpm.cfg", new[] { Common.Path, Common.Source });
            }
            catch
            {
                SendMessage("Could not write to fpm.cfg (is it open in another program?)", true);
            }
        }

        public static void InitConfig()
        {
            CheckConfig();

            string[] cfg = File.ReadAllLines("fpm.cfg");

            if (cfg.Length == 0)
            {
                SendMessage("Please select your Flashpoint folder");

                var dialog = new CommonOpenFileDialog() { IsFolderPicker = true };

                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    Common.Path = dialog.FileName;
                }
                else
                {
                    SendMessage("No folder was selected, defaulting to working directory");
                }

                WriteConfig();
            }
            else
            {
                Common.Path = cfg[0];
                if (cfg.Length >= 2) Common.Source = cfg[1];
            }
        }

        public static void SendMessage(string msg, bool abort = false)
        {
            Console.WriteLine($"{Common.Args[0]}: {msg}");
            if (abort) Environment.Exit(1);
        }

        public static async Task GetComponents()
        {
            var stream = await new DownloadService().DownloadFileTaskAsync(Common.Source);

            if (stream == null)
            {
                SendMessage("Component list could not be retrieved (do you have an internet connection?)", true);
            }

            stream.Position = 0;

            var xml = new XmlDocument();
            xml.Load(stream);

            var root = xml.GetElementsByTagName("list");

            if (root.Count == 0)
            {
                SendMessage("Component list does not contain a valid root element; please alert Flashpoint staff", true);
            }

            void Iterate(XmlNode parent)
            {
                foreach (XmlNode child in parent.ChildNodes)
                {
                    if (child.Name == "component")
                    {
                        Common.Components.Add(new Component(child));
                    }

                    Iterate(child);
                }
            }
            Iterate(xml.GetElementsByTagName("list")[0]);
        }

        public static async Task<Stream> DownloadComponent(Component component)
        {
            DownloadService downloader = new DownloadService();

            downloader.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) =>
            {
                string percentage = (Math.Round(e.ProgressPercentage * 10) / 10).ToString("N1").PadLeft(5);

                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write($"[{percentage}%] Downloading {component.ID}... {FormatBytes(e.ReceivedBytesSize)} of {FormatBytes(e.TotalBytesToReceive)}");
            };

            downloader.DownloadFileCompleted += (object sender, System.ComponentModel.AsyncCompletedEventArgs e) =>
            {
                Console.WriteLine();
            };

            return await downloader.DownloadFileTaskAsync(component.URL);
        }

        public static void ExtractComponent(Stream stream, Component component)
        {
            using (var archive = ZipArchive.Open(stream))
            {
                using (var reader = archive.ExtractAllEntries())
                {
                    string infoDir  = Path.Combine(Common.Path, "Components");
                    string infoFile = Path.Combine(infoDir, $"{component.ID}.txt");

                    Directory.CreateDirectory(infoDir);

                    using (TextWriter writer = File.CreateText(infoFile))
                    {
                        string[] header = new[] { component.Hash, component.Size.ToString() }.ToArray();

                        writer.WriteLine(string.Join(" ", header));
                    }

                    int extractedFiles = 0;
                    int totalFiles = archive.Entries.Where(item => !item.IsDirectory).ToArray().Length;

                    while (reader.MoveToNextEntry())
                    {
                        if (reader.Entry.IsDirectory) continue;

                        reader.WriteEntryToDirectory(Common.Path, new ExtractionOptions
                        {
                            ExtractFullPath = true,
                            Overwrite = true,
                            PreserveFileTime = true
                        });

                        using (TextWriter writer = File.AppendText(infoFile))
                        {
                            writer.WriteLine(reader.Entry.Key.Replace("/", @"\"));
                        }

                        extractedFiles++;
                        string percentage = (Math.Round((double)extractedFiles / totalFiles * 1000) / 10).ToString("N1").PadLeft(5);

                        Console.SetCursorPosition(0, Console.CursorTop);
                        Console.Write($"[{percentage}%] Extracting {component.ID}... {extractedFiles} of {totalFiles} files");
                    }

                    Console.WriteLine();
                }
            }
        }

        public static void RemoveComponent(Component component)
        {
            string infoPath = Path.Combine(Common.Path, "Components", $"{component.ID}.txt");
            string[] infoData = File.ReadAllLines(infoPath);

            for (int i = 1; i < infoData.Length; i++)
            {
                FullDelete(Path.Combine(Common.Path, infoData[i]));

                string percentage = (Math.Round((double)i / (infoData.Length - 1) * 1000) / 10).ToString("N1").PadLeft(5);

                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write($"[{percentage}%] Removing {component.ID}... {i} of {infoData.Length - 1} files");
            }

            FullDelete(infoPath);

            Console.WriteLine();
        }

        public static void FullDelete(string file)
        {
            if (File.Exists(file)) File.Delete(file);

            string folder = Path.GetDirectoryName(file);

            while (folder != Common.Source)
            {
                if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                {
                    Directory.Delete(folder, false);
                }
                else break;

                folder = Directory.GetParent(folder).ToString();
            }
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes >= 1000000000)
            {
                return (Math.Truncate((double)bytes / 100000000) / 10).ToString("N1") + "GB";
            }
            else if (bytes >= 1000000)
            {
                return (Math.Truncate((double)bytes / 100000) / 10).ToString("N1") + "MB";
            }
            else
            {
                return (Math.Truncate((double)bytes / 100) / 10).ToString("N1") + "KB";
            }
        }
    }
}
