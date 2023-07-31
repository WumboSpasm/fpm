using System;
using System.Linq;
using System.Threading.Tasks;

namespace FPM
{
    public static partial class Program
    {
        static readonly string[] Commands =
        {
            "path",
            "source",
            "list",
            "info",
            "download",
            "remove",
            "update"
        };

        static async Task Main(string[] args)
        {
            Common.Args = args;
            Common.Client.Timeout = TimeSpan.FromSeconds(3);

            if (Common.Args.Length == 0 || Commands.All(cmd => cmd != Common.Args[0]))
            {
                Console.WriteLine(HelpText);
                Environment.Exit(0);
            }
            else if (Common.Args.Length == 1 && new[] { "info", "remove" }.Any(cmd => cmd == Common.Args[0]))
            {
                SendMessage("At least one argument is required", true);
            }

            if (Common.Args[0] != "path" && Common.Args[0] != "source")
            {
                InitConfig();
                await GetComponents();
            }

            switch (Common.Args[0])
            {
                case "path":
                    PathHandler();
                    break;
                case "source":
                    SourceHandler();
                    break;
                case "list":
                    ListHandler();
                    break;
                case "info":
                    InfoHandler();
                    break;
                case "download":
                    await DownloadHandler();
                    break;
                case "remove":
                    RemoveHandler();
                    break;
                case "update":
                    await UpdateHandler();
                    break;
            }

            Environment.Exit(0);
        }
    }
}
