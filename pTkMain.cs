//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Ptk {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Toolkit.Configuration;
    using Toolkit.Exceptions;
    using Toolkit.Extensions;
    using Toolkit.Scripting.Languages.PropertySheet;
    using Toolkit.Utility;

    internal class pTkMain {
        private const string help =
            @"
Usage:
-------

pTK [options] action [buildconfiguration...]
    
    Options:
    --------
    --help                      this help
    --nologo                    don't display the logo
    --load-config=<file>        loads configuration from <file>
    --verbose                   prints verbose messages

    --rescan-tools              rescan for tool paths
    --show-tools                prints the path of the tools 

    --load=<file>               loads the build ptk buildinfo
                                defaults to .\COPKG\.buildinfo 

    --mingw-install=<path>      specifies the location of the mingw install
    --msys-install=<path>       specifies the location of the msys install

    Actions:
        build                   builds the product

        clean                   removes all files that are not part of the 
                                project source

        status                  shows any files present that should not be

        verify                  ensures that the product source matches the 
                                built and cleaned

        trace                   performs a build using CoApp Trace to gather 
                                build data 

        list                    lists availible builds from buildinfo

    [buildconfiguration]        optional; indicates the builds from the 
                                buildinfo file to act on. Defaults to all

";

        private ProcessUtility _cmdexe;
        private ProcessUtility _gitexe;
        private ProcessUtility _hgexe;
        private ProcessUtility _ptk;
        private ProcessUtility _traceexe;
        
        private string _gitcmd;
        // private string _setenvcmd;
        // private string _vcvars;

        // sdk batch file locations
        private string _setenvcmd71;
        private string _setenvcmd7;
        private string _setenvcmd6;
        private string _setenvcmdFeb2003;

        private string _wdksetenvcmd7600;

        // compiler batch file locations
        private string _vcvarsallbat10;
        private string _vcvarsallbat9;
        private string _vcvarsallbat8;
        private string _vcvarsallbat7;
        private string _vcvarsallbat71;
        private string _vcvars32bat;


        private bool _useGit;
        private bool _useHg;
        private bool _verbose;
        private readonly Dictionary<string, string> _originalEnvironment = GetEnvironment();
        private bool _showTools;
        private readonly List<string> _tmpFiles= new List<string>();
        private string _searchPaths = "";

        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static int Main(string[] args) {
            return new pTkMain().main(args);
        }

        private static Dictionary<string, string> GetEnvironment() {
            var env = Environment.GetEnvironmentVariables();
            return env.Keys.Cast<object>().ToDictionary(key => key.ToString(), key => env[key].ToString());
        }

        private void ResetEnvironment() {
            foreach( var key in Environment.GetEnvironmentVariables().Keys ) {
                Environment.SetEnvironmentVariable(key.ToString(),string.Empty);    
            }
            foreach (var key in _originalEnvironment.Keys) {
                Environment.SetEnvironmentVariable(key, _originalEnvironment[key]);    
            }
        }

        private void SetVCCompiler(string compilerName, string compilerBatchFile, string arch) {

            using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                Console.Write("Setting VC Compiler: ");
            }
            using (new ConsoleColors(ConsoleColor.Green, ConsoleColor.Black)) {
                Console.Write(compilerName);
            }
            using (new ConsoleColors(ConsoleColor.Yellow, ConsoleColor.Black)) {
                Console.WriteLine(" for [{0}]", arch);
            }


            if (string.IsNullOrEmpty(compilerBatchFile))
                throw new Exception("Cannot locate Visual C++ vcvars batch file command. Please install {0} (and use --rescan-tools). ".format(compilerName));

            _cmdexe.Exec(@"/c ""{0}"" /{1} & set ", compilerBatchFile, arch == "x86" ? "x86" : "x64");

            foreach (var x in _cmdexe.StandardOut.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)) {
                if (x.Contains("=")) {
                    var v = x.Split('=');
                    Environment.SetEnvironmentVariable(v[0], v[1]);
                    // Console.WriteLine("Setting ENV: [{0}]=[{1}]", v[0], v[1]);
                }
            }
        }

        private void SetSDK(string sdkName, string sdkBatchFile, string arch) {
            using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                Console.Write("Setting SDK: ");
            }
            using (new ConsoleColors(ConsoleColor.Green, ConsoleColor.Black)) {
                Console.Write(sdkName);
            }
            using (new ConsoleColors(ConsoleColor.Yellow, ConsoleColor.Black)) {
                Console.WriteLine(" for [{0}]", arch);
            }

            var targetCpu = Environment.GetEnvironmentVariable("TARGET_CPU");

            if (string.IsNullOrEmpty(targetCpu) || (targetCpu == "x64" && arch == "x86") || (targetCpu == "x86" && arch != "x86")) {

                if (string.IsNullOrEmpty(sdkBatchFile))
                    throw new Exception("Cannot locate SDK SetEnv command for SDK ({0}). Please install the Windows SDK {0}".format(sdkName));

                // Console.WriteLine(@"/c ""{0}"" /{1} & set ", _setenvcmd, arch == "x86" ? "x86" : "x64");

                _cmdexe.Exec(@"/c ""{0}"" /{1} & set ", sdkBatchFile, arch == "x86" ? "x86" : "x64");

                foreach (var x in _cmdexe.StandardOut.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                    if (x.Contains("=")) {
                        var v = x.Split('=');
                        Environment.SetEnvironmentVariable(v[0], v[1]);
                        // Console.WriteLine("Setting ENV: [{0}]=[{1}]", v[0], v[1]);
                    }

                /*
                targetCpu = Environment.GetEnvironmentVariable("TARGET_CPU");
                if (string.IsNullOrEmpty(targetCpu) || (targetCpu == "x64" && arch == "x86") || (targetCpu == "x86" && arch != "x86")) {
                    Console.WriteLine("Arch: {0}", arch);
                    Console.WriteLine("TargetCPU: {0}", targetCpu);
                    throw new Exception("Cannot set the SDK environment. Please install the Windows SDK ({0}) and use the setenv.cmd command to set your environment".format(sdkName));
                }
                 */
            }
        }

        private void SetMingwCompiler( string arch) {
            using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                Console.Write("Setting Compiler: ");
            }
            using (new ConsoleColors(ConsoleColor.Green, ConsoleColor.Black)) {
                Console.Write("mingw");
            }
            using (new ConsoleColors(ConsoleColor.Yellow, ConsoleColor.Black)) {
                Console.WriteLine(" for [{0}]", arch);
            }


            var mingwProgramFinder = new ProgramFinder("", Directory.GetDirectories(@"c:\\", "M*").Aggregate(_searchPaths+@"%ProgramFiles(x86)%;%ProgramFiles%;%ProgramW6432%", (current, dir) => dir + ";" + current));

            var gcc = mingwProgramFinder.ScanForFile("mingw32-gcc.exe");
            var msysmnt = mingwProgramFinder.ScanForFile("msysmnt.exe");

            if( string.IsNullOrEmpty(gcc)) {
                throw new ConsoleException("Unable to locate MinGW install location. Use --mingw-install=<path>\r\n   (it will remember after that.)");
            }

            if (string.IsNullOrEmpty(msysmnt)) {
                throw new ConsoleException("Unable to locate MSYS install location. Use --msys-install=<path>\r\n   (it will remember after that.)");
            }

            var msysBin = Path.GetDirectoryName(msysmnt);
            var msysPath = Path.GetDirectoryName(msysBin);

            var msysLocalBin = Path.Combine(msysPath, "local", "bin");
            var mingwBin = Path.GetDirectoryName(gcc);
            var mingwPath = Path.GetDirectoryName(mingwBin);
            var username = Environment.GetEnvironmentVariable("USERNAME") ?? "";

            var newPath = ".;" + mingwBin + ";" + msysBin + ";" + msysLocalBin + ";" + Environment.GetEnvironmentVariable("PATH");
            Environment.SetEnvironmentVariable("PATH", newPath);

            var tmpPath = Environment.GetEnvironmentVariable("TMP") ??
                Environment.GetEnvironmentVariable("TEMP") ??
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp").Replace("\\", "/");

            Environment.SetEnvironmentVariable("TMP", tmpPath);
            Environment.SetEnvironmentVariable("TEMP", tmpPath);

            Environment.SetEnvironmentVariable("WD", msysBin);
            Environment.SetEnvironmentVariable("TERM", "cygwin");

            var homedir = Environment.GetEnvironmentVariable("HOME");
            if( string.IsNullOrEmpty(homedir) ) {
                homedir = Path.Combine(Path.Combine(msysPath, "home"), username);
                if (!Directory.Exists(homedir)) {
                    homedir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                }
                Environment.SetEnvironmentVariable("HOME", homedir.Replace("\\", "/"));
            }
            
            Environment.SetEnvironmentVariable("HISTFILE", Path.Combine(homedir, ".bashhistory").Replace("\\", "/"));

            Environment.SetEnvironmentVariable("LOGNAME", username);
            Environment.SetEnvironmentVariable("MAKE_MODE", "unix");
            Environment.SetEnvironmentVariable("MSYSCON", "sh.exe");
            Environment.SetEnvironmentVariable("MSYSTEM", "MINGW32");
          
        }

        private void SwitchCompiler(string compiler, string platform) {
           
            switch( compiler ) {
                case "vc10":
                    SetVCCompiler("Visual Studio 2010", _vcvarsallbat10, platform);
                    break;

                case "vc9":
                    SetVCCompiler("Visual Studio 2008", _vcvarsallbat9, platform);
                    break;

                case "vc8":
                    SetVCCompiler("Visual Studio 2005", _vcvarsallbat8, platform);
                    break;

                case "vc7.1":
                    SetVCCompiler("Visual Studio 2003", _vcvarsallbat71, platform);
                    break;

                case "vc7":
                    SetVCCompiler("Visual Studio 2002", _vcvarsallbat7, platform);
                    break;

                case "vc6":
                    SetVCCompiler("Visual Studio 98 (vc6)", _vcvars32bat, platform);
                    break;

                case "sdk7.1":
                    SetSDK("Windows Sdk 7.1", _setenvcmd71, platform);
                    break;

                case "sdk7":
                    SetSDK("Windows Sdk 7", _setenvcmd7, platform);
                    break;

                case "sdk6":
                    SetSDK("Windows Sdk 6", _setenvcmd6, platform);
                    break;

                    /*
                case "wdk7600":
                    var wdkFolder = RegistryView.System[@"SOFTWARE\Wow6432Node\Microsoft\WDKDocumentation\7600.091201\Setup", "Build"].Value as string;
            
                    if (string.IsNullOrEmpty(wdkFolder)) {
                        wdkFolder = RegistryView.System[@"SOFTWARE\Microsoft\WDKDocumentation\7600.091201\Setup", "Build"].Value as string;
                    }
                    
                    // C:\WinDDK\7600.16385.1\ fre x86 WIN7
                    SetSDK("Windows WDK 7600", _wdksetenvcmd7600, platform);
                    break;
                    */
                case "mingw":
                    SetMingwCompiler(platform);
                    break;

                default :
                    throw new ConsoleException("Unknown Compiler Selection: {0}", compiler);
            }
        }

        private void SwitchSdk( string sdk, string platform ) {

            switch (sdk) {
                case "sdk7.1":
                    SetSDK("Windows Sdk 7.1", _setenvcmd71, platform);
                    break;

                case "sdk7":
                    SetSDK("Windows Sdk 7", _setenvcmd7, platform);
                    break;

                case "sdk6":
                    SetSDK("Windows Sdk 6", _setenvcmd6, platform);
                    break;

                case "feb2003":
                    SetSDK("Platform SDK Feb 2003", _setenvcmd6, platform);
                    break;

                case "wdk7600":
                    SetSDK("Windows WDK 7600", _wdksetenvcmd7600, platform);
                    break;

                case "none":
                    break;

                default:
                    throw new ConsoleException("Unknown Compiler Selection: {0}", sdk);

            }
        }

        private int main(IEnumerable<string> args) {
            var options = args.Switches();
            var parameters = args.Parameters();
            var tempBuildinfo = (from a in @".\COPKG\".DirectoryEnumerateFilesSmarter("*.buildinfo", SearchOption.TopDirectoryOnly)
                                 orderby a.Length ascending
                                 select a.GetFullPath()).FirstOrDefault();
            //we'll just use the default even though it won't work so I don't need to change the code much :)
            var buildinfo = tempBuildinfo ?? @".\COPKG\.buildinfo".GetFullPath();

            Console.CancelKeyPress += (x, y) => {
                Console.WriteLine("Stopping ptk.");
                if (_cmdexe != null)
                    _cmdexe.Kill();
                if (_gitexe != null)
                    _gitexe.Kill();
                if (_hgexe != null)
                    _hgexe.Kill();
                if (_ptk != null)
                    _ptk.Kill();
                if (_traceexe != null) {
                    _traceexe.Kill();
                }
            };


            #region Parse Options

            foreach (string arg in options.Keys) {
                IEnumerable<string> argumentParameters = options[arg];

                switch (arg) {
                    case "nologo":
                        this.Assembly().SetLogo("");
                        break;

                    case "verbose":
                        _verbose = true;
                        break;

                    case "load":
                        buildinfo = argumentParameters.LastOrDefault().GetFullPath();
                        break;

                    case "mingw-install":
                    case "msys-install":
                        _searchPaths += argumentParameters.LastOrDefault().GetFullPath() + ";";
                        break;

                    case "rescan-tools":
                        ProgramFinder.IgnoreCache = true;
                        break;

                    case "show-tools":
                        _showTools = true;
                        break;

                    case "help":
                        return Help();
                }
            }

            if (!File.Exists(buildinfo)) {
                return Fail("Unable to find buildinfo file [{0}]. \r\n\r\n    Use --help for command line help.", buildinfo);
            }

            // make sure that we're in the parent directory of the .buildinfo file.
            Environment.CurrentDirectory = Path.GetDirectoryName(Path.GetDirectoryName(buildinfo));

            Logo();

            if (parameters.Count() < 1) {
                return Fail("Missing action . \r\n\r\n    Use --help for command line help.");
            }

            #endregion

            _cmdexe = new ProcessUtility("cmd.exe");
            var f = new ProgramFinder("").ScanForFile("trace.exe");

            if(!string.IsNullOrEmpty(f)) {
                _traceexe = new ProcessUtility(new ProgramFinder("").ScanForFile("trace.exe"));
            }

            _ptk = new ProcessUtility(Assembly.GetEntryAssembly().Location);

            _useGit = Directory.Exists(".git".GetFullPath());
            _useHg = _useGit ? false : Directory.Exists(".hg".GetFullPath());

            if (!(_useGit || _useHg)) {
                return Fail("Source must be checked out using git or hg-git.");
            }

            if (_useGit) {
                if (_verbose) {
                    Console.WriteLine("Using git for verification");
                }
                _gitcmd = ProgramFinder.ProgramFilesAndDotNet.ScanForFile("git.cmd");
                _gitexe = null;
                if (string.IsNullOrEmpty(_gitcmd)) {
                    f = ProgramFinder.ProgramFilesAndDotNet.ScanForFile("git.exe");
                    if (string.IsNullOrEmpty(f)) {
                        return Fail("Can not find git.cmd or git.exe (required to perform verification.)");
                    }
                    _gitexe = new ProcessUtility(ProgramFinder.ProgramFilesAndDotNet.ScanForFile("git.exe"));
                }
            }

            if (_useHg) {
                 f = ProgramFinder.ProgramFilesAndDotNet.ScanForFile("hg.exe");
                if (string.IsNullOrEmpty(f)) {
                    return Fail("Can not find hg.exe (required to perform verification.)");
                }
                _hgexe = new ProcessUtility(f);
            }

            // find sdk batch files.

            _setenvcmd71 = ProgramFinder.ProgramFilesAndDotNetAndSdk.ScanForFile("setenv.cmd", excludeFilters: new[] { @"\Windows Azure SDK\**" , "winddk**" }, includeFilters: new [] {"sdk**", "v7.1**"}, rememberMissingFile:true, tagWithCosmeticVersion:"7.1");
            _setenvcmd7 = ProgramFinder.ProgramFilesAndDotNetAndSdk.ScanForFile("setenv.cmd", excludeFilters: new[] { @"\Windows Azure SDK\**", "7.1**", "winddk**" }, includeFilters: new[] { "sdk**", "v7**" }, rememberMissingFile: true, tagWithCosmeticVersion: "7.0");
            _setenvcmd6 = ProgramFinder.ProgramFilesAndDotNetAndSdk.ScanForFile("setenv.cmd", excludeFilters: new[] { @"\Windows Azure SDK\**", "winddk**" }, includeFilters: new[] { "sdk**", "6**" }, rememberMissingFile: true, tagWithCosmeticVersion: "6");

            _wdksetenvcmd7600 = ProgramFinder.ProgramFilesAndDotNetAndSdk.ScanForFile("setenv.bat", excludeFilters: new[] { @"\Windows Azure SDK\**"}, includeFilters: new[] { "winddk**"  }, rememberMissingFile: true, tagWithCosmeticVersion: "7600.16385.1");

            _vcvarsallbat10 = ProgramFinder.ProgramFilesAndDotNetAndSdk.ScanForFile("vcvarsall.bat", includeFilters: new[] { "vc**", "10.0**" }, rememberMissingFile: true, tagWithCosmeticVersion: "10.0");
            _vcvarsallbat9 = ProgramFinder.ProgramFilesAndDotNetAndSdk.ScanForFile("vcvarsall.bat", includeFilters: new[] { "vc**", "9.0**" }, rememberMissingFile: true, tagWithCosmeticVersion: "9.0");
            _vcvarsallbat8 = ProgramFinder.ProgramFilesAndDotNetAndSdk.ScanForFile("vcvarsall.bat", includeFilters: new[] { "vc**", "8.0**" }, rememberMissingFile: true, tagWithCosmeticVersion: "8.0");
            _vcvarsallbat7 = ProgramFinder.ProgramFilesAndDotNetAndSdk.ScanForFile("vcvarsall.bat", includeFilters: new[] { "vc**", "7.0**" }, rememberMissingFile: true, tagWithCosmeticVersion: "7.0");
            _vcvarsallbat71 = ProgramFinder.ProgramFilesAndDotNetAndSdk.ScanForFile("vcvarsall.bat", includeFilters: new[] { "vc**", "7.1**" }, rememberMissingFile: true, tagWithCosmeticVersion: "7.1");
            _vcvars32bat = ProgramFinder.ProgramFilesAndDotNetAndSdk.ScanForFile("vcvars32.bat", includeFilters: new[] { "vc98**" }, rememberMissingFile: true, tagWithCosmeticVersion: "6");

            if (_showTools) {
                if (_useGit) {
                    Console.Write("Git: {0}", _gitcmd ?? "");
                    if (_gitexe != null) {
                        Console.WriteLine(_gitexe.Executable ?? "");
                    }
                }
                if (_useHg) {
                    Console.WriteLine("hg: {0}", _hgexe.Executable);
                }
                Console.WriteLine("SDK Setenv (7.1): {0}", _vcvarsallbat10 ?? "Not-Found");
                Console.WriteLine("SDK Setenv (7.0): {0}", _setenvcmd7 ?? "Not-Found");
                Console.WriteLine("SDK Setenv (6): {0}", _setenvcmd6 ?? "Not-Found");

                Console.WriteLine("VC vcvarsall (10.0): {0}", _vcvarsallbat10 ?? "Not-Found");
                Console.WriteLine("VC vcvarsall (9.0): {0}", _vcvarsallbat9 ?? "Not-Found");
                Console.WriteLine("VC vcvarsall (8.0): {0}", _vcvarsallbat8 ?? "Not-Found");
                Console.WriteLine("VC vcvarsall (7.0): {0}", _vcvarsallbat7 ?? "Not-Found");
                Console.WriteLine("VC vcvarsall (7.1): {0}", _vcvarsallbat71 ?? "Not-Found");
                Console.WriteLine("VC vcvars32 (6): {0}", _vcvars32bat ?? "Not-Found");

                Console.WriteLine("ptk: {0}", _ptk.Executable);
                Console.WriteLine("trace: {0}", _traceexe.Executable);
            }

            PropertySheet propertySheet = null;
            try {
                propertySheet = PropertySheet.Load(buildinfo);
            }
            catch (EndUserParseException pspe) {
                using (new ConsoleColors(ConsoleColor.Yellow, ConsoleColor.Black)) {
                    Console.Write(pspe.Message);
                    Console.WriteLine("--found '{0}'", pspe.Token.Data);
                }

                return Fail("Error parsing .buildinfo file");
            }
            var builds = from rule in propertySheet.Rules where rule.Name != "*" select rule;
            if (parameters.Count() > 1) {
                var allbuilds = builds;
                builds = parameters.Skip(1).Aggregate(Enumerable.Empty<Rule>(), (current, p) => current.Union(from build in allbuilds where build.Name.IsWildcardMatch(p) select build));
            }

            if (builds.Count() == 0) {
                return Fail("No valid build configurations selected.");
            }

            try {
                switch (parameters.FirstOrDefault().ToLower()) {
                    case "build":
                        Build(builds);

                        break;
                    case "clean":
                        Clean(builds);
                        using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                            Console.WriteLine("Project Cleaned.");
                        }
                        break;
                    case "verify":
                        Clean(builds); // clean up other builds in the list first.
                        Verify(builds);
                        using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                            Console.WriteLine("Project Verified.");
                        }
                        break;
                    case "status":
                        Status(builds);
                        using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                            Console.WriteLine("Project is in clean state.");
                        }
                        break;
                    case "trace":
                        Trace(builds);
                        break;
                    case "list":
                        Console.WriteLine("Buildinfo from [{0}]", buildinfo);
                        (from build in builds
                            let compiler = build["compiler"].FirstOrDefault()
                            let sdk = build["sdk"].FirstOrDefault()
                            let platform = build["platform"].FirstOrDefault()
                            let targets = build["targets"].FirstOrDefault()
                            select new {
                                Configuration = build.Name,
                                Compiler = compiler != null ? compiler.LValue : "sdk7.1",
                                Sdk = sdk != null ? sdk.LValue : "sdk7.1",
                                Platform = platform != null ? platform.LValue : "x86",
                                Number_of_Outputs = targets != null ? targets.Values.Count() : 0
                            }).ToTable().ConsoleOut();
                        break;
                    default:
                        return Fail("'{0}' is not a valid command. \r\n\r\n    Use --help for assistance.");
                }
            }
            catch (ConsoleException e) {
                return Fail("   {0}", e.Message);
            }
            catch (Exception e) {
                Console.WriteLine(e.StackTrace);
                return Fail("   {0}", e.Message);
            }

            return 0;
        }

        private void TraceExec( string script, string traceFile ) {
            if (script.Contains("\r") || script.Contains("\n") ) {
                script =
@"@echo off
@setlocal 
{1}:
@cd ""{0}""


".format(Environment.CurrentDirectory, Environment.CurrentDirectory[0]) + script;
                var scriptpath = WriteTempScript(script);
                _traceexe.ExecNoRedirections(@"--nologo ""--output-file={1}"" cmd.exe /c ""{0}""", scriptpath, traceFile);
            }
            else {
                _traceexe.ExecNoRedirections(@"--nologo ""--output-file={1}"" cmd.exe /c ""{0}""", script, traceFile);
            }
        }

        private string WriteTempScript(string text) {
            var tmpFilename = Path.GetTempFileName();
            _tmpFiles.Add(tmpFilename);
            tmpFilename += ".cmd";
            _tmpFiles.Add(tmpFilename);
            File.WriteAllText(tmpFilename, text);

            return tmpFilename;
        }
        private void Exec(string script) {
            

            if (script.Contains("\r") || script.Contains("\n") ) {
                script =
@"@echo off
@setlocal 
{1}:
@cd ""{0}""


".format(Environment.CurrentDirectory, Environment.CurrentDirectory[0]) + script;
                //Console.WriteLine(script);
                var scriptpath = WriteTempScript(script);
                _cmdexe.ExecNoRedirections(@"/c ""{0}""", scriptpath);
            }
            else {
                _cmdexe.ExecNoRedirections(@"/c ""{0}""", script);
            }
            if( _cmdexe.ExitCode != 0 ) {
                throw new ConsoleException("Command Exited with value {0}", _cmdexe.ExitCode);
            }
        }

       
        private void SetCompilerSdkAndPlatform( Rule build ) {
            ResetEnvironment();

            var compilerProperty = build["compiler"].FirstOrDefault();
            var compiler = compilerProperty != null ? compilerProperty.LValue : "sdk7.1";

            var sdkProperty = build["sdk"].FirstOrDefault();
            var sdk = sdkProperty != null ? sdkProperty.LValue : "sdk7.1";

            var platformProperty = build["platform"].FirstOrDefault();
            var platform = platformProperty != null ? platformProperty.LValue : "x86";

            if (!compiler.Contains("sdk") && !compiler.Contains("wdk")) {
                SwitchSdk(sdk, platform);
            }

            SwitchCompiler(compiler,platform);
        }


        private void Clean(IEnumerable<Rule> builds) {
            foreach( var build in builds ) {
                SetCompilerSdkAndPlatform(build);


                var cmd = build["clean-command"].FirstOrDefault();
                if( cmd == null ) 
                    throw new ConsoleException("missing clean command in build {0}",build.Name);

                try {
                    Exec(cmd.LValue);
                } catch
                {
                    //ignoring any failures from clean command.
                }
                File.Delete(Path.Combine(Environment.CurrentDirectory, "trace[{0}].xml".format(build.Name)));
            }
        }
        private void BuildDependencies(Rule build) {
            var pwd = Environment.CurrentDirectory;

            foreach (var use in build["uses"]) {
                var config = string.Empty;
                var folder = string.Empty;
                if (use.IsCompoundProperty) {
                    config = use.LValue;
                    folder = use.RValue;
                }
                else {
                    folder = use.LValue;
                }
                folder = folder.GetFullPath();
                if (!Directory.Exists(folder)) {
                    throw new ConsoleException("Dependency project [{0}] does not exist.", folder);
                }
                var depBuildinfo = Path.Combine(folder, @"copkg\.buildinfo");
                if (!File.Exists(depBuildinfo)) {
                    throw new ConsoleException("Dependency project is missing buildinfo [{0}]", depBuildinfo);
                }

                Environment.CurrentDirectory = folder;
                _ptk.ExecNoRedirections("--nologo build {0}", config);
                if (_ptk.ExitCode != 0)
                    throw new ConsoleException("Dependency project failed to build [{0}] config={1}", depBuildinfo, string.IsNullOrEmpty(config) ? "all" : config);

                Environment.CurrentDirectory = pwd;
            }
        }

        private void Build(IEnumerable<Rule> builds) {
            foreach (var build in builds) {
                BuildDependencies(build);

                SetCompilerSdkAndPlatform(build);

                var cmd = build["build-command"].FirstOrDefault();
                if (cmd == null)
                    throw new ConsoleException("missing build command in build {0}", build.Name);

                using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                    Console.WriteLine("Built Configuration [{0}]", build.Name);
                }

                Exec(cmd.LValue);
            }
        }

        private void Verify(IEnumerable<Rule> builds) {
            foreach (var build in builds) {
                Clean( build.SingleItemAsEnumerable());
                Build(build.SingleItemAsEnumerable());
                
                foreach( var targ in build["targets"] ) {
                    if(targ.IsCollection) {
                        foreach( var file in targ.Values ) {
                            if( !File.Exists(file) ) {
                                throw new ConsoleException("Target [{0}] was not found.", file);
                            }
                        }
                    } else if( targ.IsValue ) {
                        if (!File.Exists(targ.LValue)) {
                            throw new ConsoleException("Target [{0}] was not found.", targ.LValue);
                        }
                    }
                }

                using (new ConsoleColors(ConsoleColor.Gray, ConsoleColor.Black)) {
                    Console.WriteLine("Targets Verified.");
                }
                

                Clean(build.SingleItemAsEnumerable());
                Status(build.SingleItemAsEnumerable());
            }
        }

        private void Status(IEnumerable<Rule> builds) {
            foreach (var build in builds) {
                IEnumerable<string> results = new string[] { };
                if (_useGit) {
                    results = Git("status -s");
                }
                else if (_useHg) {
                    results = Hg("status");
                }

                if (results.Count() > 0) {
                    Fail("Project directory is not clean:");
                    using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                        foreach (var result in results) {
                            Console.WriteLine("   {0}", result);
                        }
                    }
                    throw new ConsoleException("Failed.");
                }
            }
        }

        private void Trace(IEnumerable<Rule> builds) {
            foreach (var build in builds) {
                BuildDependencies(build);

                SetCompilerSdkAndPlatform(build);

                var cmd = build["build-command"].FirstOrDefault();
                if (cmd == null)
                    throw new ConsoleException("missing build command in build {0}", build.Name);

                TraceExec(cmd.LValue, Path.Combine(Environment.CurrentDirectory, "trace[{0}].xml".format(build.Name)));
            }
        }

        private IEnumerable<string> Git(string cmdLine) {
            if( !string.IsNullOrEmpty(_gitcmd) ) {
                _cmdexe.Exec(@"/c ""{0}"" {1}", _gitcmd, cmdLine);
                return from line in _cmdexe.StandardOut.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries) where !line.ToLower().Contains("copkg") select line;
            } else {
                _gitexe.Exec(cmdLine);
                return from line in _gitexe.StandardOut.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries) where !line.ToLower().Contains("copkg") select line;  
            }
        }

        private IEnumerable<string> Hg(string cmdLine) {
            _hgexe.Exec(cmdLine);
            return from line in _hgexe.StandardOut.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries) where !line.ToLower().Contains("copkg") select line;
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