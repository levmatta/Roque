using CLAP;
using System;
using System.Linq;
using System.ServiceProcess;

namespace Cinchcast.Roque.Service
{
    static class Program
    {

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            string[] args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            var mustBeConsole = args.Length > 0 && !(
                args[0].Equals("work", StringComparison.InvariantCultureIgnoreCase) ||
                args[0].Equals("w", StringComparison.InvariantCultureIgnoreCase)
                );

            if (Environment.UserInteractive || mustBeConsole)
            {
                Parser.RunConsole<RoqueApp>(args);
            }
            else
            {
                var servicesToRun = new ServiceBase[] { new RoqueService() };
                ServiceBase.Run(servicesToRun);
            }
        }

    }
}
