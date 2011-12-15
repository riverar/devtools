//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.RepositoryService {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Resources;
    using System.Threading;
    using Properties;
    using Toolkit.Configuration;
    using Toolkit.Console;
    using Toolkit.Engine;
    using Toolkit.Engine.Client;
    using Toolkit.Exceptions;
    using Toolkit.Extensions;
    using Toolkit.Logging;
    using Toolkit.Network;

    public class RepositoryServiceMain  : AsyncConsoleProgram {
        internal static PackageManagerMessages _messages;
        private static bool _verbose = false;
        internal static readonly RegistryView Settings = RegistryView.CoAppUser["RepositoryService"];

        protected override ResourceManager Res {
            get { return Resources.ResourceManager; }
        }

        private static int Main(string[] args) {
            return new RepositoryServiceMain().Startup(args);
        }

        protected override int Main(IEnumerable<string> args) {
            _messages = new PackageManagerMessages {
                UnexpectedFailure = UnexpectedFailure,
                NoPackagesFound = NoPackagesFound,
                PermissionRequired = OperationRequiresPermission,
                Error = MessageArgumentError,
                RequireRemoteFile =
                    (canonicalName, remoteLocations, localFolder, force) =>
                        Downloader.GetRemoteFile(canonicalName, remoteLocations, localFolder, force, new RemoteFileMessages {
                            Progress = (itemUri, percent) => { "Downloading {0}".format(itemUri.AbsoluteUri).PrintProgressBar(percent); },
                        }, _messages),
                OperationCancelled = CancellationRequested,
                PackageSatisfiedBy = (original, satisfiedBy) => { original.SatisfiedBy = satisfiedBy; },
                PackageBlocked = BlockedPackage,
                UnknownPackage = UnknownPackage,
            };

            var hosts = new string[] { "*" };
            var ports = new int[] { 80 };
            var commitMessage = "trigger";
            var packageUpload = "upload";
            string localfeedLocation = null;
            string packageStoragePath = null;
            string packagePrefixUrl = null;
            string canonicalFeedUrl = null;

            string tweetCommits = Settings["#tweet-commits"].StringValue;
            string tweetPackages = Settings["#tweet-packages"].StringValue;
            string azureAccount = Settings["#azure-account"].StringValue;

            string azureKey = null;

            var options = args.Where(each => each.StartsWith("--")).Switches();
            var parameters = args.Where(each => !each.StartsWith("--")).Parameters();


            foreach (var arg in options.Keys) {
                var argumentParameters = options[arg];
                var last = argumentParameters.LastOrDefault();
                var lastAsBool = string.IsNullOrEmpty(last) || last.IsTrue();

                switch (arg) {
                        /* options  */
                    case "verbose":
                        _verbose = lastAsBool;
                        Logger.Errors = true;
                        Logger.Messages = true;
                        Logger.Warnings = true;
                        break;

                        /* global switches */
                    case "load-config":
                        // all ready done, but don't get too picky.
                        break;

                    case "nologo":
                        this.Assembly().SetLogo(string.Empty);
                        break;

                    case "help":
                        return Help();

                    case "feed-path":
                        localfeedLocation = last;
                        break;

                    case "feed-url":
                        canonicalFeedUrl = last;
                        break;

                    case "package-path":
                        packageStoragePath = last;
                        break;

                    case "package-prefix":
                        packagePrefixUrl = last;
                        break;

                    case "host":
                        hosts = argumentParameters.ToArray();
                        break;

                    case "port":
                        ports = argumentParameters.Select(each => each.ToInt32()).ToArray();
                        break;
                    
                    case "package-upload":
                        packageUpload = last;
                        break;

                    case "commit-message":
                        commitMessage = last;
                        break;

                    case "tweet-commits":
                        Settings["#tweet-commits"].StringValue = tweetCommits = last;
                        break;

                    case "tweet-packages":
                        Settings["#tweet-commits"].StringValue = tweetPackages = last;
                        break;

                    case "azure-name":
                        Settings["#azure-account"].StringValue = azureAccount = last;
                        break;

                    case "azure-key":
                        azureKey = last;
                        break;
                }
            }

            Tweeter.Init(Settings, options);
            Bitly.Init(Settings, options);
            CloudFileSystem cfs = null; 

            if( !string.IsNullOrEmpty(azureAccount)) {
                cfs = new CloudFileSystem(Settings, azureAccount, azureKey);
            }

            try {
                var listener = new Listener();

                // get startup information.

                foreach( var host in hosts ) {
                    listener.AddHost(host);
                }

                foreach( var port in ports ) {
                    listener.AddPort(port);
                }

                
                listener.AddHandler(commitMessage, new CommitMessageHandler(tweetCommits));

                if( string.IsNullOrEmpty(packageStoragePath) || string.IsNullOrEmpty(localfeedLocation) || string.IsNullOrEmpty(packagePrefixUrl)  ) {
                    Console.WriteLine("[Package Uploader Disabled] specify must specify --package-path, --feed-path and  --package-prefix");
                }else {
                    listener.AddHandler(packageUpload, new UploadedFileHandler(localfeedLocation, canonicalFeedUrl, packageStoragePath, packagePrefixUrl, tweetPackages, cfs));
                }
                listener.Start();

                Console.WriteLine("Press ctrl-c to stop the listener.");

                while (true) {
                    Thread.Sleep(1000);
                }

                listener.Stop();
            } catch(Exception e) {
                Console.WriteLine("{0} -- {1}\r\n{2}", e.GetType(), e.Message, e.StackTrace);
                CancellationTokenSource.Cancel();
                PackageManager.Instance.Disconnect();
            }

            return 0;
        }

        private void UnknownPackage(string canonicalName) {
            Console.WriteLine("Unknown Package {0}", canonicalName);
        }

        private void BlockedPackage(string canonicalName) {
            Console.WriteLine("Package {0} is blocked", canonicalName);
        }

        private void CancellationRequested(string obj) {
            Console.WriteLine("Cancellation Requested.");
        }

        private void MessageArgumentError(string arg1, string arg2, string arg3) {
            Console.WriteLine("Message Argument Error {0}, {1}, {2}.", arg1, arg2, arg3);
        }

        private void OperationRequiresPermission(string policyName) {
            Console.WriteLine("Operation requires permission Policy:{0}", policyName);
        }

        private void NoPackagesFound() {
            Console.WriteLine("Did not find any packages.");
        }

        private void UnexpectedFailure(Exception obj) {
            throw new ConsoleException("SERVER EXCEPTION: {0}\r\n{1}", obj.Message, obj.StackTrace);
        }

        private void Verbose(string text, params object[] objs) {
            if (_verbose) {
                Console.WriteLine(text.format(objs));
            }
        }
    }
}

