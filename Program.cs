//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010 Trevor Dennis, Garrett Serack. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace CoApp.Scan
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Text;
	using Toolkit.Extensions;
	using Toolkit.Scan;
	using Toolkit.Scan.Types;

    /// <summary>
	/// Contains the program.
	/// </summary>
	internal class Program
	{
		private const string DEFAULT_OUTPUTFILE = "scanreport.xml";
		private const string HELP = @"
Usage:
-------

scan [options] <source-root-path>

    <source-root-path>          the root of the source tree to collect data from
        
    Options:
    --------
    --help                      this help
    --nologo                    don't display the logo
    --load-config=<file>        loads configuration from <file>
    --verbose                   prints verbose messages
    
    --output-file=<file>        dumps the scan output to the specified <file>
";

		private static int Main(string[] args)
		{
			return new Program().main(args);
		}

		private int main(string[] args)
		{
			string outputFile = DEFAULT_OUTPUTFILE;
			bool verbose = false;

			var options = args.Switches();
			var parameters = args.Parameters();

			#region Parse Options

			foreach (var arg in options.Keys)
			{
				var argumentParameters = options[arg];

				switch (arg)
				{
					/* options  */

					case "output-file":
						// 
						outputFile = argumentParameters.FirstOrDefault();
						break;

					case "verbose":
						verbose = true;
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

			if (parameters.Count() != 1)
			{
				return Fail("Missing source code root path. \r\n\r\n    Use --help for command line help.");
			}

			try
			{
				ProjectScanner scanner = new ProjectScanner();
				scanner.Verbose = verbose;

				ScanReport report = scanner.Scan(parameters.FirstOrDefault());

				string xml = report.Serialize();

				if (outputFile == "-")
				{
					Console.WriteLine(xml);
				}
				else
				{
					StreamWriter writer = new StreamWriter(outputFile, false, Encoding.Unicode);
					writer.WriteLine(xml);
					writer.Close();
				}
			}
			catch (Exception ex)
			{
				return Fail(string.Format("{0}\n{1}", ex.Message, ex.StackTrace));

			}

			return 0;
		}

		#region fail/HELP/logo

		public int Fail(string text, params object[] par)
		{
			Logo();
			using (new ConsoleColors(ConsoleColor.Red, ConsoleColor.Black))
				Console.WriteLine("Error:{0}", text.format(par));
			return 1;
		}

		private int Help()
		{
			Logo();
			using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black))
				HELP.Print();
			return 0;
		}

		private void Logo()
		{
			using (new ConsoleColors(ConsoleColor.Cyan, ConsoleColor.Black))
				this.Assembly().Logo().Print();
			this.Assembly().SetLogo("");
		}

		#endregion
	}
}