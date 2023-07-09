using System.Net;
using System.Xml;

using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace FPM
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
        public string[] Depends { get; } = Array.Empty<string>();
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

            if (long.TryParse(GetAttribute(node, "date-modified", true), out long lastUpdated))
            {
                var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(lastUpdated).ToLocalTime();

                LastUpdated = dateTime.ToShortDateString() + " " + dateTime.ToLongTimeString();
            }
            else
            {
                Program.SendMessage("Component has an invalid timestamp; please alert Flashpoint staff", true);
            }

            // DownloadSize

            if (long.TryParse(GetAttribute(node, "download-size", true), out long downloadSize))
            {
                DownloadSize = downloadSize;
            }
            else
            {
                Program.SendMessage("Component has a download size with non-numeric characters; please alert Flashpoint staff", true);
            }

            // InstallSize

            if (long.TryParse(GetAttribute(node, "install-size", true), out long installSize))
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

            string infoPath = Path.Combine(Common.Path, "Components", ID);

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
        public static string Path { get; set; } = System.IO.Path.GetFullPath(System.IO.Path.Combine(Directory.GetCurrentDirectory(), ".."));
        public static string Source { get; set; } = "https://nexus-dev.unstable.life/repository/stable/components.xml";

        public static string Config { get => System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fpm.cfg"); }
        public static HttpClient Client { get; } = new();

        public static List<Component> Components { get; set; } = new();

        public static string[] Args { get; set; } = Array.Empty<string>();
    }

    public static partial class Program
    {
        public static void CheckConfig()
        {
            if (!File.Exists(Common.Config))
            {
                try
                {
                    File.Create(Common.Config).Close();
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
                File.WriteAllLines(Common.Config, new[] { Common.Path, Common.Source });
            }
            catch
            {
                SendMessage("Could not write to fpm.cfg (is it open in another program?)", true);
            }
        }

        public static void InitConfig()
        {
            CheckConfig();

            string[] cfg = File.ReadAllLines(Common.Config);

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
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            string xmlText = "";

            try
            {
                xmlText = await Common.Client.GetStringAsync(Common.Source);
            }
            catch
            {
                SendMessage("Component list could not be retrieved (do you have an internet connection?)", true);
            }

            XmlDocument xml = new();

            try
            {
                xml.LoadXml(xmlText);
            }
            catch
            {
                SendMessage("Component list could not be parsed; please alert Flashpoint staff", true);
            }

            var root = xml.GetElementsByTagName("list");

            if (root.Count == 0)
            {
                SendMessage("Component list does not contain a valid root element; please alert Flashpoint staff", true);
            }

            static void Iterate(XmlNode parent)
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

            Console.Write($"Downloading {component.ID}...");

            var stream = new MemoryStream(await Common.Client.GetByteArrayAsync(component.URL));

            if (stream == null)
            {
                SendMessage($"Component {component.ID} could not be retrieved (do you have an internet connection?)", true);
            }

            Console.Write(" done!\n");

            return stream;
        }

        public static void ExtractComponent(Stream stream, Component component)
        {
            Console.Write($" Extracting {component.ID}...");

            List<string> infoContents = new()
            {
                string.Join(" ", new[] { component.Hash, $"{component.InstallSize}" }.Concat(component.Depends).ToArray())
            };

            if (component.InstallSize == 0) return;

            using (var reader = ZipArchive.Open(stream).ExtractAllEntries())
            {
                while (reader.MoveToNextEntry())
                {
                    if (reader.Entry.IsDirectory) continue;

                    string destPath = Path.Combine(Common.Path, component.Directory.Replace('/', Path.DirectorySeparatorChar));

                    Directory.CreateDirectory(destPath);

                    reader.WriteEntryToDirectory(destPath, new ExtractionOptions
                    {
                        ExtractFullPath = true,
                        Overwrite = true,
                        PreserveFileTime = true
                    });

                    infoContents.Add(Path.Combine(component.Directory, reader.Entry.Key).Replace('/', Path.DirectorySeparatorChar));
                }
            }

            string infoDir = Path.Combine(Common.Path, "Components");

            try
            {
                Directory.CreateDirectory(infoDir);
                File.WriteAllLines(Path.Combine(infoDir, component.ID), infoContents);
            }
            catch
            {
                SendMessage("Component info file could not be created (is it open in another program?)", true);
            }

            Console.Write(" done!\n");
        }

        public static void RemoveComponent(Component component)
        {
            Console.Write($"   Removing {component.ID}...");

            string infoPath = Path.Combine(Common.Path, "Components", component.ID);
            string[] infoData = File.ReadAllLines(infoPath);

            for (int i = 1; i < infoData.Length; i++)
            {
                FullDelete(Path.Combine(Common.Path, infoData[i]));
            }

            FullDelete(infoPath);

            Console.Write(" done!\n");
        }

        public static void FullDelete(string file)
        {
            file = Path.GetFullPath(file);

            if (!Path.GetFullPath(file).StartsWith(Common.Path)) return;

            try
            {
                File.Delete(file);
            }
            catch (Exception e)
            {
                if (e is not DirectoryNotFoundException)
                {
                    SendMessage($"Could not delete {file} (is it open in another program?)");
                    return;
                }
            }

            string folder = Path.GetDirectoryName(file);

            while (folder != null && folder != Directory.GetParent(Common.Path).ToString())
            {
                if (Directory.Exists(folder) && !Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Any())
                {
                    try { Directory.Delete(folder, true); } catch { }
                }
                else break;

                folder = Directory.GetParent(folder).ToString();
            }
        }

        public static string FormatBytes(long bytes)
        {
            string[] units = new[] { "B", "KB", "MB", "GB" };
            int i = units.Length;

            while (--i >= 0)
            {
                double unitSize = Math.Pow(1024, i);
                if (Math.Abs(bytes) >= unitSize) return (Math.Round(bytes / unitSize * 10) / 10).ToString("N1") + units[i];
            }

            return "0.0B";
        }
    }
}
