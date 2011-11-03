//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.mkRepo {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Resources;
    using System.Threading;
    using Properties;
    using Toolkit.Console;
    using Toolkit.Engine;
    using Toolkit.Engine.Client;
    using Toolkit.Exceptions;
    using Toolkit.Extensions;
    using Toolkit.Logging;
    using Toolkit.Network;

    public class mkRepoMain : AsyncConsoleProgram {
        private bool _verbose = false;
        private PackageManagerMessages _messages;
        private readonly PackageManager _pm = PackageManager.Instance;

        private static int Main(string[] args) {
            return new mkRepoMain().Startup(args);
        }

        protected override ResourceManager Res {
            get { return Resources.ResourceManager; }
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

            try {
                #region command line parsing

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

                        default:
                            throw new ConsoleException("Unknown parameter '{0}'", arg);
                    }
                }

                Logo();

                if (parameters.Count() < 1) {
                    // throw new ConsoleException(Resources.MissingCommand);
                }

                #endregion

            } catch ( CoAppException e) {
                
            }
            return 0;
        }

        private void WaitForPackageManagerToComplete() {
            var trigger = new ManualResetEvent(!_pm.IsConnected || _pm.ActiveCalls == 0);
            Action whenTriggered = () => trigger.Set();

            _pm.Disconnected += whenTriggered;
            _pm.Completed += whenTriggered;

            WaitHandle.WaitAny(new[] { CancellationTokenSource.Token.WaitHandle, trigger });

            _pm.Disconnected -= whenTriggered;
            _pm.Completed -= whenTriggered;
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
            if (true == _verbose) {
                Console.WriteLine(text.format(objs));
            }
        }
    }
}
