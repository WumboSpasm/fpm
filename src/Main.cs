using System;
using System.Linq;

namespace FlashpointManagerCLI
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

        [STAThread]
        static void Main(string[] args)
        {
            Common.Args = args;

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
                GetComponents().Wait();
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
                    DownloadHandler().Wait();
                    break;
                case "remove":
                    RemoveHandler();
                    break;
                case "update":
                    UpdateHandler().Wait();
                    break;
            }

            Environment.Exit(0);
        }
    }
}
