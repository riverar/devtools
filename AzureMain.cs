using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoApp.Azure {
    using System.IO;
    using System.Reflection;
    using Microsoft.WindowsAzure;
    using Toolkit.Configuration;
    using Toolkit.Exceptions;
    using Toolkit.Extensions;

    public class AzureMain {
        static int Main(string[] args) {
            return new AzureMain().main(args);
        }

        public static string help = @"
Usage:
-------

Azure [options] action <parameters>
    
    Options:
    --------
    --help                      this help
    --nologo                    don't display the logo
    --load-config=<file>        loads configuration from <file>
    --verbose                   prints verbose messages

    --account-name=<account>    sets the account name
    --account-key=<accountKey>  sets the account key
    --remember                  remembers the credentials

    --uri                       prints out full URIs instead of names for remote files


    Actions:
    --------
    copy    <local-file-mask> <to-container>:[destination]
    copy    <from-container>:[filemask] [to-folder]

    dir       
    dir     <container>:[filemask]

    erase   <container>:<filemask>

    rmdir   <container>:
    mkdir   <container>:
";
        private string _accountName;
        private string _accountKey;
        private bool _remember;
        private bool _uri = false;
        private RegistryView _accountSettings = RegistryView.User["Azure\\Accounts"];
        private CloudFileSystem _cloudFileSystem = new CloudFileSystem();


        private string GetContainerName(string parameter) {
            if (string.IsNullOrEmpty(parameter)) {
                return null;
            }

            if( !parameter.Contains(":")) {
                return null;
            }

            var bits = parameter.Split(':');
            if( bits.Length > 0) {
                if(bits[0].Length > 3 ) {
                    return bits[0];
                }
            }
            return null;
        }

        private string GetRemoteFileMask(string parameter) {
            if (string.IsNullOrEmpty(parameter)) {
                return null;
            }
            var bits = parameter.Split(':');
            if( bits.Length > 1) {
                if(bits[0].Length > 3 ) {
                    return parameter.Substring(bits[0].Length + 1);
                }
            }
            return null;
        }

        protected int main(IEnumerable<string> args) {

            try {
                var options = args.Where(each => each.StartsWith("--")).Switches();
                var parameters = args.Where(each => !each.StartsWith("--")).Parameters();

                foreach (var arg in options.Keys) {
                    var argumentParameters = options[arg];
                    var last = argumentParameters.LastOrDefault();
                    var lastAsBool = string.IsNullOrEmpty(last) || last.IsTrue();

                    switch (arg) {
                            /* options  */

                            /* global switches */
                        case "load-config":
                            // all ready done, but don't get too picky.
                            break;

                        case "nologo":
                            this.Assembly().SetLogo(string.Empty);
                            break;

                        case "help":
                            return Help();

                        case "account-name":
                            _accountName = last;
                            break;

                        case "account-key":
                            _accountKey = last;
                            break;

                        case "remember" :
                            _remember = true;
                            break;

                        case "uri":
                        case "url":
                            _uri = true;
                            break;

                        default:
                            throw new ConsoleException("Unknown command:", arg);
                    }
                }

                Logo();

                if( string.IsNullOrEmpty(_accountName) ) {
                    // is there a default?
                    _accountName = _accountSettings["#default"].StringValue;
                }

                if( string.IsNullOrEmpty(_accountKey)) {
                    _accountKey = _accountSettings[_accountName, "key"].EncryptedStringValue;
                }

                if( _remember ) {
                    Console.WriteLine("Storing account information.");
                    _accountSettings["#default"].StringValue =_accountName;
                    if (!string.IsNullOrEmpty(_accountName)) {
                        _accountSettings[_accountName, "key"].EncryptedStringValue = _accountKey;
                    }
                }

                if (!parameters.Any()) {
                    throw new ConsoleException("missing command");
                }

                var command = parameters.FirstOrDefault().ToLower();
                parameters = parameters.Skip(1);

                switch( command ) {
                    case "copy":
                    case "cp":
                        if( parameters.Count() <1 ||  parameters.Count() > 2 ) {
                            throw new CoAppException("Command 'copy' has one or two parameters.");
                        }
                        
                        var from = parameters.FirstOrDefault();
                        var to = parameters.Skip(1).FirstOrDefault();
                        var fromContainer = GetContainerName(from);
                        var toContainer = GetContainerName(to);

                        if( string.IsNullOrEmpty(fromContainer) && string.IsNullOrEmpty(toContainer) ) {
                            throw new CoAppException("For command 'copy', at least one parameter must have a <container>:");
                        } 

                        if(!string.IsNullOrEmpty(fromContainer) && !string.IsNullOrEmpty(toContainer)  ) {
                            return CopyFromAzureToAzure(fromContainer, GetRemoteFileMask(from), toContainer, GetRemoteFileMask(to));
                        }

                        if(!string.IsNullOrEmpty(fromContainer) ) {
                            return CopyFromAzureToLocal(fromContainer, GetRemoteFileMask(from), to);
                        }

                        return CopyFromLocalToAzure(from, toContainer,GetRemoteFileMask(to) );

                    case "dir":
                    case "ls":
                        if(parameters.Count() > 1 ) {
                           throw new CoAppException("Command 'dir' takes zero or one parameters."); 
                        }

                        var dirPath = parameters.FirstOrDefault();
                        if( string.IsNullOrEmpty(dirPath)) {
                            return ListContainers();
                        }
                        var dirContainer = GetContainerName(dirPath);
                        if( string.IsNullOrEmpty(dirContainer)) {
                            throw new CoAppException("Command 'dir' parameter must specify a <container>:"); 
                        }
                        return ListFiles(dirContainer, GetRemoteFileMask(dirPath));
                        

                    case "erase":
                    case "del":
                        if(parameters.Count() != 1 ) {
                           throw new CoAppException("Command 'erase' requires one parameter."); 
                        }

                        var erasePath = parameters.FirstOrDefault();
                        var eraseContainer = GetContainerName(erasePath);
                        if( string.IsNullOrEmpty(eraseContainer)) {
                            throw new CoAppException("Command 'erase' parameter must specify <container>:"); 
                        }
                        var eraseMask = GetRemoteFileMask(erasePath);
                        if( string.IsNullOrEmpty(eraseMask)) {
                            throw new CoAppException("Command 'erase' parameter must specify a file mask."); 
                        }
                        return Erase(eraseContainer, eraseMask);
                        
                    case "rd":
                    case "rmdir":
                        if(parameters.Count() != 1 ) {
                           throw new CoAppException("Command 'rmdir' requires one parameter."); 
                        }

                        var rmdirPath = parameters.FirstOrDefault();
                        var rmdirContainer = GetContainerName(rmdirPath);
                        if( string.IsNullOrEmpty(rmdirContainer)) {
                            throw new CoAppException("Command 'rmdir' parameter must specify <container>:"); 
                        }
                        if( !string.IsNullOrEmpty( GetRemoteFileMask(rmdirPath))) {
                            throw new CoAppException("Command 'rmdir' parameter must not specify a file mask."); 
                        }
                        return RemoveContainer(rmdirContainer);

                    case "md":
                    case "mkdir":
                        
                        if(parameters.Count() != 1 ) {
                           throw new CoAppException("Command 'mkdir' requires one parameter."); 
                        }

                        var mkdirPath = parameters.FirstOrDefault();
                        var mkdirContainer = GetContainerName(mkdirPath);
                        if( string.IsNullOrEmpty(mkdirContainer)) {
                            throw new CoAppException("Command 'mkdir' parameter must specify <container>:"); 
                        }
                        if( !string.IsNullOrEmpty( GetRemoteFileMask(mkdirPath))) {
                            throw new CoAppException("Command 'mkdir' parameter must not specify a file mask."); 
                        }
                        return CreateContainer(mkdirContainer);
                }

            }catch (ConsoleException e) {
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

        private int CreateContainer(string container) {
            _cloudFileSystem.Connect(_accountName,_accountKey);
            if(_cloudFileSystem.ContainerExists(container)) {
                throw new ConsoleException("Container '{0}' already exists.", container);
            }
            _cloudFileSystem.AddContainer(container);
            return 0;
        }

        private int RemoveContainer(string container) {
            _cloudFileSystem.Connect(_accountName,_accountKey);
            if(!_cloudFileSystem.ContainerExists(container)) {
                throw new ConsoleException("Container '{0}' does not exist.", container);
            }

            _cloudFileSystem.RemoveContainer(container);
            return 0;
        }

        private int Erase(string container, string fileMask) {
            _cloudFileSystem.Connect(_accountName,_accountKey);

            if(!_cloudFileSystem.ContainerExists(container)) {
                throw new ConsoleException("Container '{0}' does not exist.", container);
            }

            var blobs = _cloudFileSystem.GetBlobNames(container, fileMask);
            if( !blobs.Any() ) {
                throw new ConsoleException("Container '{0}' does not contain files matching '{1}'.", container, fileMask);
            }

            foreach( var blob in blobs) {
                Console.WriteLine("Deleting :{0}", blob);
                _cloudFileSystem.EraseBlob(container, blob);
            }
            
            return 0;
        }

        private int ListFiles(string container, string fileMask) {
            _cloudFileSystem.Connect(_accountName,_accountKey);

            if(!_cloudFileSystem.ContainerExists(container)) {
                throw new ConsoleException("Container '{0}' does not exist.", container);
            }

            if (string.IsNullOrEmpty(fileMask)) {
                fileMask = "*";
            }

            var blobs = _cloudFileSystem.GetBlobs(container, fileMask);
            
            if( !blobs.Any() ) {
                throw new ConsoleException("Container '{0}' does not contain files matching '{1}'.", container, fileMask);
            }

            (from blob in blobs
                select new {
                    Date = ((DateTime)blob.date).ToShortDateString(),
                    Time = ((DateTime)blob.date).ToShortTimeString(),
                    Length = blob.length, 
                    File = _uri ? blob.uri.AbsoluteUri.ToString() :  blob.name,
                }).ToTable().ConsoleOut();
                
            return 0;
        }

        private int ListContainers() {
            _cloudFileSystem.Connect(_accountName,_accountKey);
            foreach( var container in _cloudFileSystem.ContainerNames ) {
                Console.WriteLine(container);
            }
            return 0;
        }

        private int CopyFromLocalToAzure(string @from, string container, string remoteFilePrefix) {
            _cloudFileSystem.Connect(_accountName,_accountKey);
            if(!_cloudFileSystem.ContainerExists(container)) {
                throw new ConsoleException("Container '{0}' does not exist.", container);
            }
            
            var localFiles = from.FindFilesSmarter().ToArray();
            var blobNames = localFiles.GetMinimalPaths().ToArray();
            for(var i = 0; i< localFiles.Length;i++) {
                _cloudFileSystem.WriteBlob(container, blobNames[i],localFiles[i], (progress) => {
                      ConsoleExtensions.PrintProgressBar("{0} => {1}:{2}".format(localFiles[i], container, blobNames[i]), progress);
                });   
                Console.WriteLine();
            }
            
            return 0;
        }

        private int CopyFromAzureToLocal(string container, string fileMask, string to) {
            _cloudFileSystem.Connect(_accountName,_accountKey);
            if(!_cloudFileSystem.ContainerExists(container)) {
                throw new ConsoleException("Container '{0}' does not exist.", container);
            }
             if (string.IsNullOrEmpty(fileMask)) {
                 fileMask = "*";
             }

            if( string.IsNullOrEmpty(to)) {
                to = Environment.CurrentDirectory;
            }

            if(!Directory.Exists(to) ) {
                throw new ConsoleException("Unknown directory '{0}'", to);
            } 

            var blobs = _cloudFileSystem.GetBlobs(container, fileMask);
                        
            if( !blobs.Any() ) {
                throw new ConsoleException("Container '{0}' does not contain files matching '{1}'.", container, fileMask);
            }

            foreach( var blob in blobs ) {
                string blobName = blob.name.ToString();
                string uri = blob.uri.ToString();
                var fullPath = Path.Combine(to, blobName).GetFullPath();
                var parentDir = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(parentDir)) {
                    Directory.CreateDirectory(parentDir);
                }

                _cloudFileSystem.ReadBlob(container, uri, fullPath,(progress) => {
                      ConsoleExtensions.PrintProgressBar("\r{0}:{1} => {2}".format(container, blobName, fullPath), progress);
                });
                Console.WriteLine();
            }
            return 0;
        }

        private int CopyFromAzureToAzure(string fromContainer, string getRemoteFileMask, string toContainer, string s) {

            return 0;
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
