using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FlashpointManagerCLI
{
    public static partial class Program
    {
        public static void PathHandler()
        {
            CheckConfig();

            string[] cfg = File.ReadAllLines("fpm.cfg");

            Common.Path = Common.Args[1];
            if (cfg.Length >= 2) Common.Source = cfg[1];

            WriteConfig();
        }

        public static void SourceHandler()
        {
            CheckConfig();

            string[] cfg = File.ReadAllLines("fpm.cfg");

            if (cfg.Length >= 1) Common.Path = cfg[0];
            Common.Source = Common.Args[1];

            WriteConfig();
        }

        public static void ListHandler()
        {
            foreach (var component in Common.Components)
            {
                if (Common.Args.Length > 1 && (
                   (Common.Args[1] == "available"  &&  component.Downloaded)
                || (Common.Args[1] == "downloaded" && !component.Downloaded)
                || (Common.Args[1] == "updates"    && !component.Outdated)))
                {
                    continue;
                }

                string prefix = " ";

                if (component.Downloaded)
                {
                    if (component.Outdated)
                    {
                        prefix = "!";
                    }
                    else
                    {
                        prefix = "*";
                    }
                }

                Console.WriteLine($"{prefix} {component.ID}");
            }
        }

        public static void InfoHandler()
        {
            var component = Common.Components.FirstOrDefault(item => item.ID == Common.Args[1]);

            if (component == null)
            {
                SendMessage("Specified component does not exist", true);
            }

            Console.WriteLine($"ID:             {component.ID}");
            Console.WriteLine($"Title:          {component.Title}");
            Console.WriteLine($"Description:    {component.Description}");
            Console.WriteLine($"Download size:  {FormatBytes(component.DownloadSize)}");
            Console.WriteLine($"Install size:   {FormatBytes(component.InstallSize)}");
            Console.WriteLine($"Last updated:   {component.LastUpdated}");
            Console.WriteLine($"CRC32:          {component.Hash}\n");

            if (component.Depends.Length > 0)
            {
                Console.WriteLine($"Dependencies: \n  {string.Join($"\n  ", component.Depends)}\n");
            }

            Console.WriteLine($"Required?       {(component.ID.StartsWith("core") ? "Yes" : "No")}");
            Console.WriteLine($"Downloaded?     {(component.Downloaded ? "Yes" : "No")}");

            if (component.Downloaded)
            {
                Console.WriteLine($"Up-to-date?  {(component.Outdated ? "No" : "Yes")}");
            }
        }

        public static async Task DownloadHandler()
        {
            string[] args = Common.Args.Skip(1).ToArray();

            var toDownload = new List<Component>();
            long downloadSize = 0;
            long installSize  = 0;

            void UpdateQueues(string id, bool isDepend = false)
            {
                var component = Common.Components.FirstOrDefault(item => item.ID == id);

                if (component == null)
                {
                    SendMessage($"Component {id} does not exist and will be skipped");
                    return;
                }

                if (!component.Downloaded)
                {
                    toDownload.Add(component);
                    downloadSize += component.DownloadSize;
                    installSize  += component.InstallSize;

                    foreach (string depend in component.Depends)
                    {
                        UpdateQueues(depend, true);
                    }
                }
                else if (!isDepend)
                {
                    SendMessage($"Component {id} is already downloaded and will be skipped");
                }
            }

            if (args.Length > 0)
            {
                foreach (string id in args)
                {
                    UpdateQueues(id);
                }
            }
            else
            {
                foreach (var component in Common.Components)
                {
                    UpdateQueues(component.ID);
                }
            }

            toDownload = toDownload.Distinct().ToList();

            if (toDownload.Count > 0)
            {
                Console.WriteLine($"{toDownload.Count} component(s) will be downloaded:");

                foreach (var component in toDownload)
                {
                    Console.WriteLine($"  {component.ID}");
                }

                Console.WriteLine();
            }
            else
            {
                SendMessage($"No components to download");
                return;
            }

            Console.WriteLine($"Estimated download size: {FormatBytes(downloadSize)}");
            Console.WriteLine($"Estimated install size:  {FormatBytes(installSize)}\n");

            char answer;

            do
            {
                Console.Write("Is this OK? [y/n]: ");
                answer = Console.ReadKey().KeyChar;
            }
            while (answer != 'y' && answer != 'n');

            if (answer == 'n') return;

            Console.WriteLine('\n');
            Console.CursorVisible = false;

            foreach (var component in toDownload)
            {
                var stream = await DownloadComponent(component);
                ExtractComponent(stream, component);
            }

            Console.CursorVisible = true;
            Console.WriteLine();

            SendMessage($"Successfully downloaded {toDownload.Count} components");
        }

        public static void RemoveHandler()
        {
            string[] args = Common.Args.Skip(1).ToArray();

            var toRemove = new List<Component>();
            long removeSize = 0;

            foreach (string id in args)
            {
                var component = Common.Components.FirstOrDefault(item => item.ID == id);

                if (component == null)
                {
                    SendMessage($"Component {id} does not exist and will be skipped");
                    return;
                }

                if (!component.Downloaded)
                {
                    SendMessage($"Component {id} is not downloaded and will be skipped");
                }
                else
                {
                    toRemove.Add(component);
                    removeSize += component.InstallSize;
                }
            }

            toRemove = toRemove.Distinct().ToList();

            if (toRemove.Count > 0)
            {
                Console.WriteLine($"{toRemove.Count} component(s) will be removed:");

                foreach (var component in toRemove)
                {
                    Console.WriteLine($"  {component.ID}");
                }

                Console.WriteLine();
            }
            else
            {
                SendMessage($"No components to remove");
                return;
            }

            Console.WriteLine($"Estimated freed size: {FormatBytes(removeSize)}\n");

            char answer;

            do
            {
                Console.Write("Is this OK? [y/n]: ");
                answer = Console.ReadKey().KeyChar;
            }
            while (answer != 'y' && answer != 'n');

            if (answer == 'n') return;

            Console.WriteLine('\n');
            Console.CursorVisible = false;

            foreach (var component in toRemove)
            {
                RemoveComponent(component);
            }

            Console.CursorVisible = true;
            Console.WriteLine();

            SendMessage($"Successfully removed {toRemove.Count} components");
        }

        public static async Task UpdateHandler()
        {
            string[] args = Common.Args.Skip(1).ToArray();

            var toUpdate   = new List<Component>();
            var toDownload = new List<Component>();
            long downloadSize = 0;
            long installSize  = 0;

            if (args.Length > 0)
            {
                void UpdateQueues(string id, bool isDepend = false)
                {
                    var component = Common.Components.FirstOrDefault(item => item.ID == id);

                    if (component == null)
                    {
                        SendMessage($"Component {id} does not exist and will be skipped");
                        return;
                    }

                    if (!component.Downloaded)
                    {
                        if (isDepend)
                        {
                            toDownload.Add(component);
                        }
                        else
                        {
                            SendMessage($"Component {id} is not downloaded and will be skipped");
                        }
                    }
                    else if (component.Downloaded && !component.Outdated)
                    {
                        SendMessage($"Component {id} is already up-to-date and will be skipped");
                    }
                    else
                    {
                        toUpdate.Add(component);
                        foreach (string depend in component.Depends)
                        {
                            UpdateQueues(depend, true);
                        }
                    }
                }

                foreach (string id in args)
                {
                    UpdateQueues(id);
                }
            }
            else
            {
                toUpdate = Common.Components.Where(item => item.Downloaded && item.Outdated).ToList();
            }

            toUpdate   = toUpdate.Distinct().ToList();
            toDownload = toDownload.Distinct().ToList();
            downloadSize = toUpdate.Sum(item => item.DownloadSize) + toDownload.Sum(item => item.DownloadSize);
            installSize  = toUpdate.Sum(item => item.SizeDifference) + toDownload.Sum(item => item.InstallSize);

            if (toUpdate.Count > 0)
            {
                Console.WriteLine($"{toUpdate.Count} component(s) will be updated:");

                foreach (var component in toUpdate)
                {
                    Console.WriteLine($"  {component.ID}");
                }

                Console.WriteLine();

                if (toDownload.Count > 0)
                {
                    Console.WriteLine($"{toUpdate.Count} component(s) will be downloaded:");

                    foreach (var component in toDownload)
                    {
                        Console.WriteLine($"  {component.ID}");
                    }

                    Console.WriteLine();
                }
            }
            else
            {
                SendMessage($"No components to update");
                return;
            }

            Console.WriteLine($"Estimated download size: {FormatBytes(downloadSize)}");
            Console.WriteLine($"Estimated changed size:  {FormatBytes(installSize)}\n");

            char answer;

            do
            {
                Console.Write("Is this OK? [y/n]: ");
                answer = Console.ReadKey().KeyChar;
            }
            while (answer != 'y' && answer != 'n');

            if (answer == 'n') return;

            Console.WriteLine('\n');
            Console.CursorVisible = false;

            foreach (var component in toUpdate)
            {
                RemoveComponent(component);

                var stream = await DownloadComponent(component);
                ExtractComponent(stream, component);
            }

            Console.CursorVisible = true;
            Console.WriteLine();

            string message = $"Successfully updated {toUpdate.Count} components";
            if (toDownload.Count > 0) message += $" and downloaded {toDownload.Count} components";

            SendMessage(message);
        }
    }
}
