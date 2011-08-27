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
    using Toolkit.Exceptions;
    using Toolkit.Extensions;
    using Toolkit.Scripting.Languages.PropertySheet;
    using Toolkit.Utility;

    internal class pTkMain {
        /// <summary>
        /// Help message for the user
        /// </summary>
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

        /// <summary>
        /// Wrapper for the Windows command line
        /// </summary>
        private ProcessUtility _cmdexe;
        /// <summary>
        /// Wrapper for git (source control)
        /// </summary>
        private ProcessUtility _gitexe;
        /// <summary>
        /// Wrapper for mercurial (source control)
        /// </summary>
        private ProcessUtility _hgexe;
        /// <summary>
        /// Wrapper for pTk (That's us!)
        /// </summary>
        private ProcessUtility _ptk;
        /// <summary>
        /// Wrapper for Trace. (Trace tells us what the build process does)
        /// </summary>
        private ProcessUtility _traceexe;
        
        
        /// <summary>
        /// Command line to git.cmd
        /// </summary>
        private string _gitcmd;
        /// <summary>
        /// Command line to setenv.cmd (prepare build environment)
        /// </summary>
        private string _setenvcmd;
        private bool _useGit;
        private bool _useHg;
        /// <summary>
        /// Does the user want us to print more?
        /// </summary>
        private bool _verbose;
        private readonly Dictionary<string, string> _originalEnvironment = GetEnvironment();
        /// <summary>
        /// Tell the user which tools we are using?
        /// </summary>
        private bool _showTools;
        /// <summary>
        /// A list of temporary files for bookkeeping
        /// </summary>
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

        /// <summary>
        /// Get the environment variables (key/value pairs)
        /// </summary>
        /// <remarks>
        /// Path variable may differ in output from actual path on some systems
        /// Run «reg query "hklm\system\currentcontrolset\control\Session manager\Environment" /v path» to verify
        /// Character limit for path on Vista is 1024 http://support.microsoft.com/kb/924032
        /// </remarks>
        /// <returns>A dictionary of path variables as strings</returns>
        private static Dictionary<string, string> GetEnvironment() {
            var env = Environment.GetEnvironmentVariables();
            return env.Keys.Cast<object>().ToDictionary(key => key.ToString(), key => env[key].ToString());
        }

        /// <summary>
        /// Resets application Environment 
        /// </summary>
        private void ResetEnvironment() {
            foreach( var key in Environment.GetEnvironmentVariables().Keys ) {
                Environment.SetEnvironmentVariable(key.ToString(),string.Empty);    
            }
            foreach (var key in _originalEnvironment.Keys) {
                Environment.SetEnvironmentVariable(key, _originalEnvironment[key]);    
            }
        }

        /// <summary>
        /// Set up environment and paths to use the Visual C compiler
        /// </summary>
        /// <param name="arch">A string indicating the target platform. Must be either "x64" or "x86"</param>
        private void SetVC10Compiler(string arch) {
            var targetCpu = Environment.GetEnvironmentVariable("TARGET_CPU");

            if (string.IsNullOrEmpty(targetCpu) || (targetCpu == "x64" && arch == "x86") || (targetCpu == "x86" && arch != "x86")) {

                if (string.IsNullOrEmpty(_setenvcmd))
                    throw new Exception("Cannot locate SDK SetEnv command. Please install the Windows SDK");

                Console.WriteLine(@"/c ""{0}"" /{1} & set ", _setenvcmd, arch == "x86" ? "x86" : "x64");
                _cmdexe.Exec(@"/c ""{0}"" /{1} & set ", _setenvcmd, arch == "x86" ? "x86" : "x64");

                
                foreach (var x in _cmdexe.StandardOut.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                    if (x.Contains("=")) {
                        var v = x.Split('=');
                        Environment.SetEnvironmentVariable(v[0], v[1]);
                        Console.WriteLine("Setting ENV: [{0}]=[{1}]", v[0], v[1]);
                    }

                targetCpu = Environment.GetEnvironmentVariable("TARGET_CPU");
                if (string.IsNullOrEmpty(targetCpu) || (targetCpu == "x64" && arch == "x86") || (targetCpu == "x86" && arch != "x86")) {
                    Console.WriteLine("Arch: {0}",arch);
                    Console.WriteLine("TargetCPI: {0}",targetCpu);
                    throw new Exception("Cannot set the SDK environment. Please install the Windows SDK and use the setenv.cmd command to set your environment");
                }
            }
        }

        /// <summary>
        /// Set up environment and paths to use mingw
        /// </summary>
        /// <param name="arch">A string indicating the target platform</param>
        private void SetMingwCompiler( string arch) {
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

        /// <summary>
        /// Change the designated compiler
        /// </summary>
        /// <param name="compiler">The compiler name and platform (supported strings only)</param>
        /// <remarks>Valid choices are "vc10-x86", "vc10-x64", "mingw-x86"</remarks>
        private void SwitchCompiler(string compiler) {
            ResetEnvironment();

            switch( compiler ) {
                case "vc10-x86":
                    SetVC10Compiler("x86");
                    break;
                case "vc10-x64":
                    SetVC10Compiler("x64");
                    break;
                case "mingw-x86":
                    SetMingwCompiler("x86");
                    break;
                default :
                    throw new ConsoleException("Unknown Compiler Selection: {0}", compiler);

            }
        }

        /// <summary>
        /// This is the main procedure
        /// </summary>
        /// <param name="args">Command line parameters</param>
        /// <returns>Error codes (0 for success, non-zero on Error)</returns>
        private int main(IEnumerable<string> args) {
            var options = args.Switches();
            var parameters = args.Parameters();
            var tempBuildinfo = (from a in @".\COPKG\".DirectoryEnumerateFilesSmarter("*.buildinfo", SearchOption.TopDirectoryOnly)
                                 orderby a.Length ascending
                                 select a.GetFullPath()).FirstOrDefault();
            // find PropertySheet location
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
                if( _ptk != null )
                    _ptk.Kill();
                if( _traceexe != null ) {
                    _traceexe.Kill();
                }
            };


            #region Parse Options 

            // set up options which were defined by the user
            foreach (string arg in options.Keys) {
                IEnumerable<string> argumentParameters = options[arg];

                switch (arg) {
                    case "nologo":
                        // disable logo (will print "\n" anyway)
                        this.Assembly().SetLogo("");
                        break;

                    case "verbose":
                        _verbose = true; 
                        break;

                    case "load":
                        // user specified a custom PropertySheet
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
            Environment.CurrentDirectory= Path.GetDirectoryName(Path.GetDirectoryName(buildinfo));

            // tell the user what we are
            Logo();

            // tell the user we can't work without instructions
            if (parameters.Count() < 1) {
                return Fail("Missing action . \r\n\r\n    Use --help for command line help.");
            }

            #endregion

            // set up several tools we need
            _cmdexe = new ProcessUtility("cmd.exe");
            _traceexe = new ProcessUtility(new ProgramFinder("").ScanForFile("trace.exe"));

            _ptk = new ProcessUtility( Assembly.GetEntryAssembly().Location );

            // if this package is tracked by git, we can use git
            _useGit = Directory.Exists(".git".GetFullPath());
            // if this package is tracked by mercurial, we can use mercurial
            _useHg = _useGit ? false : Directory.Exists(".hg".GetFullPath());

            // source control is mandatory! create a repository for this package
            if( !(_useGit||_useHg)) {
                return Fail("Source must be checked out using git or hg-git.");
            }
            
            // find git in the file system
            // - we prefer the CMD script over git.exe
            // git.exe may be located at "C:\Program Files\Git\bin"
            // git.cmd may be located at "C:\Program Files\Git\cmd"
            if( _useGit ) {
                if (_verbose) {
                    Console.WriteLine("Using git for verification");
                }
                // attemt to find git.cmd
                _gitcmd = ProgramFinder.ProgramFilesAndDotNet.ScanForFile("git.cmd");
                _gitexe = null;
                if (string.IsNullOrEmpty(_gitcmd)) {
                    // attemt to find git.exe
                    var f = ProgramFinder.ProgramFilesAndDotNet.ScanForFile("git.exe");
                    if( string.IsNullOrEmpty(f)) {
                         return Fail("Can not find git.cmd or git.exe (required to perform verification.)");
                    }
                    _gitexe = new ProcessUtility(ProgramFinder.ProgramFilesAndDotNet.ScanForFile("git.exe"));
                }
            }

            // find mercurial in the file system
            if( _useHg ) {
                var f = ProgramFinder.ProgramFilesAndDotNet.ScanForFile("hg.exe");
                if (string.IsNullOrEmpty(f)) {
                    return Fail("Can not find hg.exe (required to perform verification.)");
                }
                _hgexe = new ProcessUtility(f);
            }

            // figure out the path to the Windows SDK environment preparation command
            // usually found in "C:\Program Files\Microsoft SDKs\Windows\v7.1\Bin"
            _setenvcmd = ProgramFinder.ProgramFilesAndDotNetAndSdk.ScanForFile("setenv.cmd",filters:new [] {@"\Windows Azure SDK\**"});
            if( string.IsNullOrEmpty(_setenvcmd)) {
                return Fail("Can not find setenv.cmd (required to perform builds)");
            }

            // tell the user tool paths
            if (_showTools) {
                // print path to source control program
                if( _useGit) {
                    Console.Write("Git: {0}", _gitcmd ?? "");
                    if (_gitexe != null) {
                        Console.WriteLine(_gitexe.Executable ?? "");
                    }
                } 
                if( _useHg) {
                    Console.WriteLine("hg: {0}", _hgexe.Executable);
                }
                Console.WriteLine("SDK Setenv: {0}", _setenvcmd);
                Console.WriteLine("ptk: {0}", _ptk.Executable);
                Console.WriteLine("trace: {0}", _traceexe.Executable);
            }

            // load property sheet (that is the .buildinfo file by default)
            PropertySheet propertySheet = null;
            try {
                // load and parse. propertySheet will contain everything else we need for later
                propertySheet = PropertySheet.Load(buildinfo);
            }
            catch( EndUserParseException pspe) {
                using (new ConsoleColors(ConsoleColor.Yellow, ConsoleColor.Black)) {
                     Console.Write(pspe.Message);
                     Console.WriteLine("--found '{0}'", pspe.Token.Data);
                }
                
                return Fail("Error parsing .buildinfo file");
            }
            var builds = from rule in propertySheet.Rules where rule.Name != "*" select rule;
            if( parameters.Count() > 1 ) {
                var allbuilds = builds;
                builds = parameters.Skip(1).Aggregate(Enumerable.Empty<Rule>(), (current, p) => current.Union(from build in allbuilds where build.Name.IsWildcardMatch(p) select build));
            }
            
            // are there even builds present?
            if(builds.Count() == 0 ) {
                return Fail("No valid build configurations selected.");
            }

            // do the user's bidding
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
                            let targets = build["targets"].FirstOrDefault()
                            select new {
                                Configuration = build.Name,
                                Compiler = compiler != null ? compiler.LValue : "vc10-x86",
                                Number_of_Outputs = targets != null ? targets.Values.Count() : 0
                            }).ToTable().ConsoleOut();
                        break;
                    default:
                        return Fail("'{0}' is not a valid command. \r\n\r\n    Use --help for assistance.");
                }
            }
            catch (ConsoleException e) {
                // these exceptions are expected
                return Fail("   {0}", e.Message);
            }
            catch (Exception e) {
                // it's probably okay to crash within proper commands (because something else crashed)
                Console.WriteLine(e.StackTrace);
                return Fail("   {0}", e.Message);
            }
            
            return 0;
        }

        /// <summary>
        /// Traces the changes made by a specific script
        /// </summary>
        /// <param name="script">Script to trace</param>
        /// <param name="traceFile">An output file</param>
        private void TraceExec( string script, string traceFile ) {
            // multiline scripts need to be executed with a temporary script, 
            // everything else runs directly from the cmd prompt
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

        /// <summary>
        /// Create a temporary .cmd file
        /// </summary>
        /// <param name="text">The script to be written into the .cmd file</param>
        /// <returns>Full path to the temporary script</returns>
        private string WriteTempScript(string text) {
            var tmpFilename = Path.GetTempFileName();
            _tmpFiles.Add(tmpFilename);
            // append proper file extension
            tmpFilename += ".cmd";
            _tmpFiles.Add(tmpFilename);
            File.WriteAllText(tmpFilename, text);

            return tmpFilename;
        }

        /// <summary>
        /// Runs a command line script
        /// </summary>
        /// <param name="script">A command line script</param>
        private void Exec(string script) {
            
            // multiline scripts need prepration,
            // everything else can be run straight from cmd
            if (script.Contains("\r") || script.Contains("\n") ) {
                // set up environment for the script
                script =
@"@echo off
@setlocal 
{1}:
@cd ""{0}""


".format(Environment.CurrentDirectory, Environment.CurrentDirectory[0]) + script;
                // tell the user what we are about to run
                Console.WriteLine(script);
                // create temporary file
                var scriptpath = WriteTempScript(script);
                // run it
                _cmdexe.ExecNoRedirections(@"/c ""{0}""", scriptpath);
            }
            else {
                // run script
                _cmdexe.ExecNoRedirections(@"/c ""{0}""", script);
            }
            // handle error conditions
            if( _cmdexe.ExitCode != 0 ) {
                throw new ConsoleException("Command Exited with value {0}", _cmdexe.ExitCode);
            }
        }

       
        /// <summary>
        /// Deletes excess files according to clean command
        /// </summary>
        /// <param name="builds">A list of builds to clean</param>
        private void Clean(IEnumerable<Rule> builds) {
            foreach( var build in builds ) {
                var compiler = build["compiler"].FirstOrDefault();
                SwitchCompiler(compiler!= null ? compiler.LValue :  "vc10-x86");

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

        /// <summary>
        /// Builds all dependencies listed in a given build rule
        /// </summary>
        /// <param name="build">A build rule to which the dependencies should be built</param>
        private void BuildDependencies(Rule build) {
            // save current directory
            var pwd = Environment.CurrentDirectory;

            foreach (var use in build["uses"]) {
                var config = string.Empty;
                var folder = string.Empty;

                // set folder and configuration as needed
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

                // switch project directory
                Environment.CurrentDirectory = folder;
                // build dependency project
                _ptk.ExecNoRedirections("--nologo build {0}", config);
                if (_ptk.ExitCode != 0)
                    throw new ConsoleException("Dependency project failed to build [{0}] config={1}", depBuildinfo, string.IsNullOrEmpty(config) ? "all" : config);
                // reset directory to where we came from
                Environment.CurrentDirectory = pwd;
            }
        }

        /// <summary>
        /// Runs build rules
        /// </summary>
        /// <param name="builds">A list of build rules to build</param>
        private void Build(IEnumerable<Rule> builds) {
            foreach (var build in builds) {
                // build dependencies first
                BuildDependencies(build);

                // select a compiler or default to vc10-x86
                var compiler = build["compiler"].FirstOrDefault();
                SwitchCompiler(compiler != null ? compiler.LValue : "vc10-x86");

                // read the build command from PropertySheet
                var cmd = build["build-command"].FirstOrDefault();
                if (cmd == null)
                    throw new ConsoleException("missing build command in build {0}", build.Name);

                // tell the user which build rule we are processing right now
                using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                    Console.WriteLine("Built Configuration [{0}]", build.Name);
                }

                // run this build command
                Exec(cmd.LValue);
            }
        }

        /// <summary>
        /// Checks if the process chain clean/build/clean leaves excess or unaccounted files
        /// </summary>
        /// <remarks>
        /// Runs Clean, Build (and checks targets), Clean and Status (to check for excess files)
        /// </remarks>
        /// <param name="builds">A list of build rules to verify</param>
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

        /// <summary>
        /// Checks if excess files are present in the project directory
        /// </summary>
        /// <remarks>Throws ConsoleException if excess files are found</remarks>
        /// <param name="builds">A list of build rules to check</param>
        private void Status(IEnumerable<Rule> builds) {
            foreach (var build in builds) {
                IEnumerable<string> results = new string[] { };
                
                // this returns all new files created by the build process
                if (_useGit) {
                    results = Git("status -s");
                }
                else if (_useHg) {
                    results = Hg("status");
                }

                // Zero results means clean directory
                if (results.Count() > 0) {
                    Fail("Project directory is not clean:");
                    using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                        // list offending files
                        foreach (var result in results) {
                            Console.WriteLine("   {0}", result);
                        }
                    }
                    throw new ConsoleException("Failed.");
                }
            }
        }

        /// <summary>
        /// Trace a build process
        /// </summary>
        /// <param name="builds">The build rules to trace</param>
        private void Trace(IEnumerable<Rule> builds) {
            foreach (var build in builds) {
                // prepare dependencies. these are not part of the trace
                BuildDependencies(build);

                var compiler = build["compiler"].FirstOrDefault();
                SwitchCompiler(compiler != null ? compiler.LValue : "vc10-x86");

                // does this build rule contain a build command?
                var cmd = build["build-command"].FirstOrDefault();
                if (cmd == null)
                    throw new ConsoleException("missing build command in build {0}", build.Name);

                // run trace
                TraceExec(cmd.LValue, Path.Combine(Environment.CurrentDirectory, "trace[{0}].xml".format(build.Name)));
            }
        }

        /// <summary>
        /// Run a git command
        /// </summary>
        /// <param name="cmdLine">A command to run with git</param>
        /// <returns>Any line from git's output except for those containing "copkg"</returns>
        /// <example>
        /// Git ("status -s")
        /// </example>
        private IEnumerable<string> Git(string cmdLine) {
            if( !string.IsNullOrEmpty(_gitcmd) ) {
                _cmdexe.Exec(@"/c ""{0}"" {1}", _gitcmd, cmdLine);
                return from line in _cmdexe.StandardOut.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries) where !line.ToLower().Contains("copkg") select line;
            } else {
                _gitexe.Exec(cmdLine);
                return from line in _gitexe.StandardOut.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries) where !line.ToLower().Contains("copkg") select line;  
            }
        }

        /// <summary>
        /// Run an Hg command
        /// </summary>
        /// <param name="cmdLine">A command to run with hg</param>
        /// <returns>Any line from git's output except for those containing "copkg"</returns>
        private IEnumerable<string> Hg(string cmdLine) {
            _hgexe.Exec(cmdLine);
            return from line in _hgexe.StandardOut.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries) where !line.ToLower().Contains("copkg") select line;
        }

        #region fail/help/logo

        /// <summary>
        /// Print an error to the console
        /// </summary>
        /// <param name="text">An error message</param>
        /// <param name="par">A format string</param>
        /// <returns>Always returns 1</returns>
        /// <seealso cref="String.Format(string, object[])"/>
        /// <remarks>
        /// Format according to http://msdn.microsoft.com/en-us/library/b1csw23d.aspx
        /// </remarks>
        public static int Fail(string text, params object[] par) {
            Logo();
            using (new ConsoleColors(ConsoleColor.Red, ConsoleColor.Black)) {
                Console.WriteLine("Error:{0}", text.format(par));
            }
            return 1;
        }

        /// <summary>
        /// Print usage notes (help) and logo
        /// </summary>
        /// <returns>Always returns 0</returns>
        private static int Help() {
            Logo();
            using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                help.Print();
            }
            return 0;
        }

        /// <summary>
        /// Print program logo, information an copyright notice once.
        /// </summary>
        /// <remarks>
        /// Recurring calls to the function will not print "\n" (blank line) instead.
        /// </remarks>
        private static void Logo() {
            using (new ConsoleColors(ConsoleColor.Cyan, ConsoleColor.Black)) {
                Assembly.GetEntryAssembly().Logo().Print();
            }
            Assembly.GetEntryAssembly().SetLogo("");
        }

        #endregion
    }
}