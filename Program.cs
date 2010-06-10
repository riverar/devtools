//-----------------------------------------------------------------------
// <copyright company="Codeplex Foundation">
//     Copyright (c) 2010 Garrett Serack. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace CoApp.Scan {
    using System;
    using Toolkit.Extensions;

    internal class Program {
        private const string help = @"
Usage:
-------

CoApp-scan [options] <source-root-path>

    <source-root-path>          the root of the source tree to collect data from
        
    Options:
    --------
    --help                      this help
    --nologo                    don't display the logo
    --load-config=<file>        loads configuration from <file>
    --verbose                   prints verbose messages
    
    --output-file=<file>        dumps the scan output to the specified <file>
";

        private static int Main(string[] args) {
            return new Program().main(args);
        }

        private int main(string[] args) {
            
            var options = args.Switches();
            var parameters = args.Parameters();

            #region Parse Options 

            foreach(var arg in options.Keys) {
                var argumentParameters = options[arg];

                switch(arg) {
                        /* options  */
                        
                    case "output-file":
                        // 
                        break;
                    
                        /* global switches */
                    case "load-config":
                        // all ready done, but don't get too picky.
                        break;

                    case "nologo":
                        this.Assembly().SetLogo("");
                        break;

                    case "help":
                        return Help();

                    default:
                        return Fail("Unknown parameter [--{0}]", arg);
                }
            }
            Logo();

            #endregion

            if(parameters.Count != 1) {
                return Fail("Missign source code root path. \r\n\r\n    Use --help for command line help.");
            }

            return 0;
        }

        #region fail/help/logo

        public int Fail(string text, params object[] par) {
            Logo();
            using(new ConsoleColors(ConsoleColor.Red, ConsoleColor.Black))
                Console.WriteLine("Error:{0}", text.format(par));
            return 1;
        }

        private int Help() {
            Logo();
            using(new ConsoleColors(ConsoleColor.White, ConsoleColor.Black))
                help.Print();
            return 0;
        }

        private void Logo() {
            using(new ConsoleColors(ConsoleColor.Cyan, ConsoleColor.Black))
                this.Assembly().Logo().Print();
            this.Assembly().SetLogo("");
        }

        #endregion
    }
}