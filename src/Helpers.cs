using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml;

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
        public string Directory { get; }
        public string LastUpdated { get; }
        public long DownloadSize { get; }
        public long InstallSize { get; }
        public string Hash { get; }
        public string[] Depends { get; } = new string[] { };
        public bool Downloaded { get; } = false;
        public bool Outdated { get; } = false;

        private long oldSize = 0;
        public long SizeDifference { get => InstallSize - oldSize; }

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

            // Path

            Directory = GetAttribute(node, "path", false);

            // LastUpdated

            long lastUpdated;

            if (long.TryParse(GetAttribute(node, "date-modified", true), out lastUpdated))
            {
                var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(lastUpdated).ToLocalTime();

                LastUpdated = dateTime.ToShortDateString() + " " + dateTime.ToLongTimeString();
            }
            else
            {
                Program.SendMessage("Component has an invalid timestamp; please alert Flashpoint staff", true);
            }

            // DownloadSize

            long downloadSize;

            if (long.TryParse(GetAttribute(node, "download-size", true), out downloadSize))
            {
                DownloadSize = downloadSize;
            }
            else
            {
                Program.SendMessage("Component has a download size with non-numeric characters; please alert Flashpoint staff", true);
            }

            // InstallSize

            long installSize;

            if (long.TryParse(GetAttribute(node, "install-size", true), out installSize))
            {
                InstallSize = installSize;
            }
            else
            {
                Program.SendMessage("Component has an install size with non-numeric characters; please alert Flashpoint staff", true);
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

                if (infoHeader.Length >= 2)
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
        public static string Path = Directory.GetParent(Directory.GetCurrentDirectory()).FullName;
        public static string Source = "https://nexus-dev.unstable.life/repository/stable/components.xml";

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
            var stream = new MemoryStream(await new WebClient().DownloadDataTaskAsync(Common.Source));

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
            if (component.InstallSize == 0) return Stream.Null;

            Console.Write($"Downloading {component.ID}... ");

            var stream = new MemoryStream(await new WebClient().DownloadDataTaskAsync(component.URL));

            if (stream == null)
            {
                SendMessage($"Component {component.ID} could not be retrieved (do you have an internet connection?)", true);
            }

            Console.Write("done!\n");

            return stream;
        }

        public static void ExtractComponent(Stream stream, Component component)
        {
            Console.Write($" Extracting {component.ID}... ");

            string infoDir = Path.Combine(Common.Path, "Components");
            string infoFile = Path.Combine(infoDir, $"{component.ID}.txt");

            Directory.CreateDirectory(infoDir);

            using (TextWriter writer = File.CreateText(infoFile))
            {
                string[] header = new[] { component.Hash, $"{component.InstallSize}" }.Concat(component.Depends).ToArray();

                writer.WriteLine(string.Join(" ", header));
            }

            if (component.InstallSize == 0) return;

            using (var archive = ZipArchive.Open(stream))
            {
                using (var reader = archive.ExtractAllEntries())
                {
                    int extractedFiles = 0;
                    int totalFiles = archive.Entries.Where(item => !item.IsDirectory).ToArray().Length;

                    while (reader.MoveToNextEntry())
                    {
                        if (reader.Entry.IsDirectory) continue;

                        string destPath = Path.Combine(Common.Path, component.Directory.Replace('/', '\\'));

                        Directory.CreateDirectory(destPath);

                        reader.WriteEntryToDirectory(destPath, new ExtractionOptions
                        {
                            ExtractFullPath = true,
                            Overwrite = true,
                            PreserveFileTime = true
                        });

                        using (TextWriter writer = File.AppendText(infoFile))
                        {
                            writer.WriteLine(Path.Combine(component.Directory, reader.Entry.Key).Replace('/', '\\'));
                        }

                        extractedFiles++;
                        string percentage = (Math.Round((double)extractedFiles / totalFiles * 1000) / 10).ToString("N1");
                    }
                }
            }

            Console.Write("done!\n");
        }

        public static void RemoveComponent(Component component)
        {
            Console.Write($"   Removing {component.ID}... ");

            string infoPath = Path.Combine(Common.Path, "Components", $"{component.ID}.txt");
            string[] infoData = File.ReadAllLines(infoPath);

            for (int i = 1; i < infoData.Length; i++)
            {
                FullDelete(Path.Combine(Common.Path, infoData[i]));
            }

            FullDelete(infoPath);

            Console.Write("done!\n");
        }

        public static void FullDelete(string file)
        {
            try
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
            catch { }
        }

        public static string FormatBytes(long bytes)
        {
            string[] units = new[] { "B", "KB", "MB", "GB" };
            int i = units.Length;

            while (--i >= 0)
            {
                double unitSize = Math.Pow(1024, i);
                if (Math.Abs(bytes) >= unitSize) return (Math.Floor(bytes / unitSize * 10) / 10).ToString("N1") + units[i];
            }

            return "0.0B";
        }
    }
}
