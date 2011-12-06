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
    using System.IO;
    using System.Linq;
    using System.Resources;
    using System.ServiceModel.Syndication;
    using System.Threading;
    using System.Xml;
    using Properties;
    using Toolkit.Console;
    using Toolkit.Engine;
    using Toolkit.Engine.Client;
    using Toolkit.Engine.Model.Atom;
    using Toolkit.Exceptions;
    using Toolkit.Extensions;
    using Toolkit.Logging;
    using Toolkit.Network;

    public class mkRepoMain : AsyncConsoleProgram {
        private bool _verbose = false;
        private string _output = "feed.atom.xml";
        private string _input;
        private Uri _baseUrl;
        private Uri _feedLocation;
        private IEnumerable<string> _packages;

        internal AtomFeed Feed;

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

            Verbose("# Connecting to Service...");
            _pm.ConnectAndWait("mkRepo tool", null, 5000);
            Verbose("# Connected to Service...");

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

                        case "output":
                            _output = last;
                            break;

                        case "input":
                            _input = last;
                            break;

                        case "feed-location":
                            try {
                                _feedLocation = new Uri(last);    
                            } catch {
                                throw new ConsoleException("Feed Location '{0}' is not a valid URI ", last);
                            }
                            
                            break;

                        case "base-url":
                            try {
                                _baseUrl = new Uri(last);
                            } catch {
                                throw new ConsoleException("Base Url Location '{0}' is not a valid URI ", last);
                            }
                            break;

                        default:
                            throw new ConsoleException(Resources.UnknownParameter, arg);
                    }
                }
                Logo();
                #endregion

                if (parameters.Count() < 1) {
                    throw new ConsoleException(Resources.MissingCommand);
                }

                _packages = parameters.Skip(1);

                switch( parameters.FirstOrDefault() ) {
                    case "create" :
                        Logger.Message("Creating Feed ");
                        Create();
                        break;

                    default:
                        throw new ConsoleException(Resources.UnknownCommand, parameters.FirstOrDefault());
                }


            } catch (ConsoleException failure) {
                Fail("{0}\r\n\r\n    {1}", failure.Message, Resources.ForCommandLineHelp);
                CancellationTokenSource.Cancel();
                _pm.Disconnect();
            }
            return 0;
        }

        private void Create() {
            Feed = new AtomFeed();
            AtomFeed originalFeed = null;

            if( !string.IsNullOrEmpty(_input) ) {
                Logger.Message("Loading existing feed.");
                if( _input.IsWebUrl()) {
                    var inputFilename = "feed.atom.xml".GenerateTemporaryFilename();

                    RemoteFile.GetRemoteFile(_input, inputFilename).Get(new RemoteFileMessages() {
                        Completed = (uri) => { },
                        Failed = (uri) => { inputFilename.TryHardToDelete(); },
                        Progress = (uri, progress) => { }
                    }).Wait();

                    if( !File.Exists(inputFilename) ) {
                        throw new ConsoleException("Failed to get input feed from '{0}' ", _input);
                    }
                    originalFeed = AtomFeed.LoadFile(inputFilename);
                }
                else {
                    originalFeed = AtomFeed.LoadFile(_input);
                }
            }

            if( originalFeed != null ) {
                Feed.Add(originalFeed.Items.Where(each => each is AtomItem).Select(each => each as AtomItem));
            }

            Logger.Message("Selecting local packages");
            var files = _packages.FindFilesSmarter();

            _pm.GetPackages(files, null, null, false, null, null, null, null, false, null, null, _messages).ContinueWith((antecedent) => {
                var packages = antecedent.Result;

                foreach (var pkg in packages) {
                    _pm.GetPackageDetails(pkg.CanonicalName, _messages).Wait();

                    if (!string.IsNullOrEmpty(pkg.PackageItemText)) {
                        var item = SyndicationItem.Load<AtomItem>(XmlReader.Create(new StringReader(pkg.PackageItemText)));
                        
                        var feedItem = Feed.Add(item);
                        
                        // first, make sure that the feeds contains the intended feed location.
                        if( feedItem.Model.Feeds == null ) {
                            feedItem.Model.Feeds  = new List<Uri>();
                        }

                        if( !feedItem.Model.Feeds.Contains(_feedLocation) ) {
                            feedItem.Model.Feeds.Insert(0, _feedLocation);
                        }

                        var location = new Uri(_baseUrl, Path.GetFileName(pkg.LocalPackagePath));

                        if (feedItem.Model.Locations== null) {
                            feedItem.Model.Locations = new List<Uri>();
                        }

                        if (!feedItem.Model.Locations.Contains(location)) {
                            feedItem.Model.Locations.Insert(0, location);
                        }

                    } else {
                        throw new ConsoleException("Missing ATOM data for '{0}'", pkg.Name);
                    }
                }
            }).Wait();

            Feed.Save(_output);

            // Feed.ToString()
            // PackageFeed.Add(PackageModel);
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
