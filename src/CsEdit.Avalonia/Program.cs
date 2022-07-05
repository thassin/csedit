using System;
using System.IO;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace CsEdit.Avalonia
{
    class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args) {

            bool showUsageAndQuit = false;

            if ( args.Length > 1 ) showUsageAndQuit = true;

            if ( args.Length > 0 ) {
                string option = args[0];
                if ( option == "-?" ) showUsageAndQuit = true;
                if ( option == "-h" ) showUsageAndQuit = true;
                if ( option == "--help" ) showUsageAndQuit = true;
            }

            if ( showUsageAndQuit ) {
                Console.WriteLine();
                Console.WriteLine( "CsEdit USAGE:" );
                Console.WriteLine( "  => one optional commandline parameter: working directory." );
                Console.WriteLine( "  => defult working directory is the current directory." );
                Console.WriteLine();
                return;
            }

            string wrkdir = ".";
            if ( args.Length > 0 ) {
                wrkdir = args[0];
            }

            ProjectsProvider.Init( wrkdir );

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
    }
}
