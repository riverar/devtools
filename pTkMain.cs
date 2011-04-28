//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace CoApp.Ptk {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Toolkit.Extensions;
    using Toolkit.Scripting.Languages.PropertySheet;

    internal class pTkMain {
        private const string help =
            @"
Usage:
-------

pTK [options] action
    
    Options:
    --------
    --help                      this help
    --nologo                    don't display the logo
    --load-config=<file>        loads configuration from <file>
    --verbose                   prints verbose messages

    --load=<file>               loads the build ptk buildinfo
                                defaults to .\COPKG\.buildinfo 

    Actions:
        build                   builds the product
        clean                   removes all files that are not part of the 
                                project source
        verify                  ensures that the product source matches the 
                                built and cleaned
        trace                   performs a build using CoApp Trace to gather 
                                build data 
        
";

        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static int Main(string[] args) {
            return new pTkMain().main(args);
        }

        private int main(string[] args) {
            var options = args.Switches();
            var parameters = args.Parameters();
            var buildinfo = @".\COPKG\.buildinfo".GetFullPath();

            #region Parse Options 

            foreach (string arg in options.Keys) {
                var argumentParameters = options[arg];

                switch (arg) {
                    case "nologo":
                        this.Assembly().SetLogo("");
                        break;

                    case "load":
                        buildinfo = argumentParameters.LastOrDefault().GetFullPath();
                        break;

                    case "help":
                        return Help();
                }
            }

            if (!File.Exists(buildinfo)) {
                return Fail("Unable to find buildinfo file [{0}]. \r\n\r\n    Use --help for command line help.",buildinfo);
            }

            Logo();

            if (parameters.Count() < 1) {
                return Fail("Missing action . \r\n\r\n    Use --help for command line help.");
            }

            if (parameters.Count() > 1) {
                return Fail("Extra unknown parameters. \r\n\r\n    Use --help for command line help.");
            }

            #endregion

            var propertySheet = PropertySheet.Load(buildinfo);

            var package = propertySheet["package"].FirstOrDefault();
            var name = package["name"].FirstOrDefault();
            if( name != null && name.IsValue ) {
                Console.WriteLine("Package Name is :{0}", name.Value);
            }

            
            Console.WriteLine(propertySheet.ToString());

            // project.SpecFile = Path.GetFullPath(parameters.FirstOrDefault());));
            return 0;
        }

        #region fail/help/logo

        public int Fail(string text, params object[] par) {
            Logo();
            using (new ConsoleColors(ConsoleColor.Red, ConsoleColor.Black)) {
                Console.WriteLine("Error:{0}", text.format(par));
            }
            return 1;
        }

        private int Help() {
            Logo();
            using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                help.Print();
            }
            return 0;
        }

        private void Logo() {
            using (new ConsoleColors(ConsoleColor.Cyan, ConsoleColor.Black)) {
                this.Assembly().Logo().Print();
            }
            this.Assembly().SetLogo("");
        }

        #endregion
    }
}