//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace CoApp.ToolScanner {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using System.Xml.Linq;
    using CoApp.Toolkit.Extensions;
    using Toolkit.DynamicXml;
    using Toolkit.Utility;
    using Toolkit.Win32;


    internal class ToolScannerMain {
        private const string help =
            @"
Usage:
-------
ToolScanner [options] <directories...>

This tool scans recursively thru directories looking for things that look 
like compilers, assemblers, linkers, library tools, sdk tools etc, and 
records the  internal information of the EXEs in order to gather suitable data
for CoApp tools.

It looks for Microsoft, Cygwin, MinGW, Watcom, Borland, IBM intel compilers.


   Options:
    --------
    --help                      this help
    --nologo                    don't display the logo
    --load-config=<file>        loads configuration from <file>
    
    --clean                     do not preserve existing results.

    --send                      send the output file to Garrett

    --default=[driveletter]     Scans:
                                    \program files\
                                    \program files (x86)\
                                    \Ming*\
                                    \Msys*\
                                    \Cyg*\
                                    \*DDK*\

                                    On the given drive letter.

    
    --ouptut=<filename>         output the result to the file


    Easy: If you have your tools installed on C: you can just use:
        ToolScanner --default

    The output file is placed in ...

    

";
        private static readonly Lazy<ProgramFinder> _programFinder = new Lazy<ProgramFinder>(() => new ProgramFinder("", @"{0}\optional;%SystemDrive%\WinDDK;%ProgramFiles(x86)%;%ProgramFiles%;%ProgramW6432%".format(
            Path.GetDirectoryName(Assembly.GetEntryAssembly().Location))));
        private static readonly Lazy<ProcessUtility> StringsUtility = new Lazy<ProcessUtility>(() => new ProcessUtility(_programFinder.Value.ScanForFile("sysinternals_strings.exe")));

        private bool send;
        private string outputfilename;

        private static int Main(string[] args) {
            return new ToolScannerMain().main(args);
        }

        private int main(IEnumerable<string> args) {
            var options = args.Switches();
            var parameters = args.Parameters();


            Console.CancelKeyPress += (x, y) => {
                Console.WriteLine("Stopping ...");
                
            };

            foreach (string arg in options.Keys) {
                IEnumerable<string> argumentParameters = options[arg];

                switch (arg) {
                    case "nologo":
                        this.Assembly().SetLogo("");
                        break;

                    case "send":
                        send= true;
                        break;

                    case "rescan-tools":
                        ProgramFinder.IgnoreCache = true;
                        break;

                    case "output":
                        outputfilename = argumentParameters.Last().GetFullPath();
                        break;

                    case "default":
                        var drive = argumentParameters.Last();
                        var driveletter = string.IsNullOrEmpty(drive) ? 'c' : drive[0];
                        parameters = new[] {@"{0}:\program files*", @"{0}:\cyg*", @"{0}:\ming*", @"{0}:\msys*",@"{0}:\*ddk*"}.Select(d => d.format(driveletter)).Aggregate(parameters, (current, path) => current.Add(path));

                        break;

                    case "help":
                        return Help();
                }
            }
            // make sure that we're in the parent directory of the .buildinfo file.
            // Environment.CurrentDirectory = Path.GetDirectoryName(Path.GetDirectoryName(buildinfo));

            
            Logo();

            if (parameters.Count() < 1) {
                return Fail("Missing directory to scan. \r\n\r\n    Use --help for command line help.");
            }

            var dirList = new List<string>();
            foreach( var d in parameters) {
                var i = d.LastIndexOf("\\");

                if (i > -1) {
                    dirList.AddRange(Directory.EnumerateDirectories(d.Substring(0, i + 1), d.Substring(i + 1)));
                } else {
                    dirList.AddRange(Directory.EnumerateDirectories(Environment.CurrentDirectory,d));
                }
                
            }

            Console.WriteLine("Folders that will be searched");
            foreach (var dir in dirList) {
                Console.WriteLine("   {0}", dir.GetFullPath());
            }
            Console.WriteLine("---------------------------------------------------------\r\n");

            var EXEs = Enumerable.Empty<string>();
            // EXEs = dirList.Aggregate(EXEs, (current, dir) => current.Union(Directory.EnumerateFiles(dir.GetFullPath(), "*.exe", SearchOption.AllDirectories)));
            EXEs = dirList.Aggregate(EXEs, (current, dir) => current.Union(dir.GetFullPath().DirectoryEnumerateFilesSmarter("*.exe", SearchOption.AllDirectories) ));
            // "".DirectoryEnumerateFilesSmarter("*.exe", SearchOption.AllDirectories)
            

            var matchPatterns = new[] {
                "*cl*.exe",
                "*cc*.exe",
                "*++*.exe",
                "*cpp*.exe",
                "*plusplus*.exe",

                "*windres*.exe",
                "*rc*.exe",

                "*mc*.exe",

                "*idl*.exe",

                "*link*.exe",
                "*ld*.exe",
                "*lib*.exe",

                "*asm*.exe",

                "*ml*.exe",
                
                "*mt*.exe",
                
                "*make*.exe",
            };


            var filterPatterns = new[] {
                "*plink*",
                "**\\calibre**",
                "**\\git**",
                "**\\bazaar**",
                "**\\jetbrains**",
                "**sql**",
                "**\\mirc**",
                "**pantaray**",
                "**Windows Installer XML**",
                "**internet explorer**",
                "**live**",
                "**office**",
                "**help**",
                "**home server**",
                "**media**",
            };

            var candidates = from exe in EXEs where 
                matchPatterns.HasWildcardMatch(exe) && 
                !filterPatterns.HasWildcardMatch(exe) &&
                new FileInfo(exe).Length > 4096 
                             select exe;


            dynamic xmldoc = new DynamicNode("Tools");

            foreach (var exe in candidates) {
                try {
                    var Node = InterrogateBinary(exe);
                    xmldoc.Node.Add(Node);
                   
                    Console.WriteLine("   {0}", exe);
                } catch (Exception e)
                {
                    Console.WriteLine("{0} ===== {1}",exe, e.Message);
                    // Console.WriteLine(e.Message);
                    // Console.WriteLine(e.StackTrace);
                }
            }

            if (!string.IsNullOrEmpty(outputfilename)) {
                File.WriteAllText(outputfilename, xmldoc.Node.ToString());
            } else if (send) {
                // send it

                Console.WriteLine("Uploading...");

                var d = new Dictionary<string, string>();

                d.Add("data", xmldoc.Node.ToString());

                "http://static.withinwindows.com/sqm/upload.php".Post(d);
                Console.WriteLine("Done");
            }
            else {
                Console.WriteLine(xmldoc.Node.ToString());
            }

            

            return 0;
        }

        public XElement InterrogateBinary(string binaryPath) {
            binaryPath = binaryPath.GetFullPath();
            var peInfo = PEInfo.Scan(binaryPath);
            
            dynamic node = new DynamicNode("tool");
            node.FileName  = peInfo.VersionInfo.FileName ?? "*";

            node.Comments  = peInfo.VersionInfo.Comments ?? "*";
            node.CompanyName  = peInfo.VersionInfo.CompanyName ?? "*";
            node.FileDescription  = peInfo.VersionInfo.FileDescription ?? "*";
           
            node.FileVersion  = peInfo.VersionInfo.FileVersion ?? "*";
            node.InternalName  = peInfo.VersionInfo.InternalName ?? "*";
            node.Language  = peInfo.VersionInfo.Language ?? "*";
            node.LegalCopyright  = peInfo.VersionInfo.LegalCopyright ?? "*";
            node.LegalTrademarks  = peInfo.VersionInfo.LegalTrademarks ?? "*";
            node.OriginalFilename  = peInfo.VersionInfo.OriginalFilename ?? "*";
            node.PrivateBuild  = peInfo.VersionInfo.PrivateBuild ?? "*";
            node.ProductName  = peInfo.VersionInfo.ProductName ?? "*";
            node.ProductVersion  = peInfo.VersionInfo.ProductVersion ?? "*";
            node.SpecialBuild  = peInfo.VersionInfo.SpecialBuild ?? "*"        ;
            
            node.Attributes.ProductPrivatePart = peInfo.VersionInfo.ProductPrivatePart ;
            node.Attributes.ProductBuildPart = peInfo.VersionInfo.ProductBuildPart ;
            node.Attributes.ProductMajorPart = peInfo.VersionInfo.ProductMajorPart ;
            node.Attributes.ProductMinorPart = peInfo.VersionInfo.ProductMinorPart ;

            node.Attributes.FileBuildPart = peInfo.VersionInfo.FileBuildPart ;
            node.Attributes.FileMajorPart = peInfo.VersionInfo.FileMajorPart ;
            node.Attributes.FileMinorPart = peInfo.VersionInfo.FileMinorPart ;
            node.Attributes.FilePrivatePart = peInfo.VersionInfo.FilePrivatePart ;

            node.Attributes.IsDebug = peInfo.VersionInfo.IsDebug ;
            node.Attributes.IsPatched = peInfo.VersionInfo.IsPatched ;
            node.Attributes.IsPrivateBuild = peInfo.VersionInfo.IsPrivateBuild ;
            node.Attributes.IsPreRelease = peInfo.VersionInfo.IsPreRelease ;
            node.Attributes.IsSpecialBuild = peInfo.VersionInfo.IsSpecialBuild ;
            node.Dependencies = "";
            
            foreach (var d in peInfo.DependencyInformation.Where(each => !(each.Module.Contains("system32") || each.Module.Contains("syswow64") ||each.Module.Contains("winsxs") ) )) {
                dynamic depnode = new DynamicNode("dependency");

                depnode.Attributes.Filename = d.Filename;
                depnode.Attributes.Module = d.Module;
                
                depnode.Attributes.LinkTimeStamp = d.LinkTimeStamp;
                depnode.Attributes.FileSize = d.FileSize;
                depnode.Attributes.LinkChecksum = d.LinkChecksum;
                depnode.Attributes.RealChecksum = d.RealChecksum;
                depnode.Attributes.CPU = d.CPU;
                depnode.Attributes.Subsystem = d.Subsystem;
                depnode.Attributes.FileVer = d.FileVer;
                depnode.Attributes.ProductVer = d.ProductVer;
                depnode.Attributes.ImageVer = d.ImageVer;
                depnode.Attributes.LinkerVer = d.LinkerVer;
                depnode.Attributes.OSVer = d.OSVer;
                depnode.Attributes.SubsystemVer = d.SubsystemVer;
                 ((XElement)node.Dependencies.Node).Add((XElement)depnode.Node);
                
            }

            StringsUtility.Value.Exec(@"/accepteula ""{0}""",binaryPath);

            var rx = new Regex(@"\S*\.dll",RegexOptions.Multiline);

            var match = rx.Match(StringsUtility.Value.StandardOut);
            while (match.Success) {
                dynamic stringNode = new DynamicNode("string");
                stringNode.Attributes.dll = match.Value;
                ((XElement)node.Hints.Node).Add((XElement)stringNode.Node);
                match = match.NextMatch();
            }  
            return node.Node;
        }

        #region fail/help/logo

        public static int Fail(string text, params object[] par) {
            Logo();
            using (new ConsoleColors(ConsoleColor.Red, ConsoleColor.Black)) {
                Console.WriteLine("Error:{0}", text.format(par));
            }
            return 1;
        }

        private static int Help() {
            Logo();
            using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                help.Print();
            }
            return 0;
        }

        private static void Logo() {
            using (new ConsoleColors(ConsoleColor.Cyan, ConsoleColor.Black)) {
                Assembly.GetEntryAssembly().Logo().Print();
            }
            Assembly.GetEntryAssembly().SetLogo("");
        }

        #endregion
    }
}