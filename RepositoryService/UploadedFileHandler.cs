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
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using System.Xml;
    using Ionic.Zlib;
    using Toolkit.Engine.Client;
    using Toolkit.Engine.Model.Atom;
    using Toolkit.Exceptions;
    using Toolkit.Extensions;
    using Toolkit.Logging;
    using Toolkit.Network;
    using Toolkit.Tasks;
    using System.ServiceModel.Syndication;

    public class UploadedFileHandler : RequestHandler {
        private readonly Tweeter _tweeter;
        private readonly string _localfeedLocation;
        private readonly string _packageStorageFolder;
        private readonly Uri _packagePrefixUrl;
        private readonly Uri _canonicalFeedUrl;
        private readonly CloudFileSystem _cloudFileSystem;

        public UploadedFileHandler(string localfeedLocation, string canonicalFeedUrl, string packageStoragePath, string packagePrefixUrl, string twitterHandle, CloudFileSystem cloudFileSystem) {
            _localfeedLocation = localfeedLocation.GetFullPath();
            _canonicalFeedUrl = new Uri(canonicalFeedUrl);
            _packageStorageFolder = packageStoragePath;
            _packagePrefixUrl = new Uri(packagePrefixUrl);
            _cloudFileSystem = cloudFileSystem;

            if (!string.IsNullOrEmpty(twitterHandle)) {
                _tweeter = new Tweeter(twitterHandle);
            }

            if( _cloudFileSystem == null ) {
                _packageStorageFolder = _packageStorageFolder.GetFullPath();
                if(!Directory.Exists(_packageStorageFolder)) {
                    Directory.CreateDirectory(_packageStorageFolder);
                }
            }
        }

        /*
        public override Task Post(HttpListenerResponse response, string relativePath, Toolkit.Pipes.UrlEncodedMessage message) {
            var fileData = message["file"].ToString();
            if( string.IsNullOrEmpty(fileData) ) {
                response.StatusCode = 500;
                response.Close();
                return "".AsResultTask();
            }
            
            var data = fileData.UrlDecode();
        }
        */

        public override Task Put(HttpListenerResponse response, string relativePath, byte[] data) {
            if( data.Length < 1 ) {
                response.StatusCode = 500;
                response.Close();
                return "".AsResultTask();
            }

            var result = Task.Factory.StartNew(
                () => {
                    var filename = "UploadedFile.bin".GenerateTemporaryFilename();
                    File.WriteAllBytes(filename, data);


                    PackageManager.Instance.ConnectAndWait("RepositoryService", null, 5000);

                    // verify that the file is actually a valid package
                    PackageManager.Instance.GetPackages(filename, messages: RepositoryServiceMain._messages).ContinueWith(
                        antecedent => {
                            if( antecedent.IsFaulted ) {
                                Console.WriteLine("Fault occurred after upload: {0}", filename);
                                filename.TryHardToDelete();
                                response.StatusCode = 400;
                                response.Close();
                                return;
                            }

                            if( antecedent.IsCanceled) {
                                Console.WriteLine("Request was cancelled");
                                filename.TryHardToDelete();
                                response.StatusCode = 400;
                                response.Close();
                                return;
                            }

                            var pkg = antecedent.Result.FirstOrDefault();
                            if( pkg == null ) {
                                Console.WriteLine("File uploaded is not recognized as a package: {0}", filename);
                                filename.TryHardToDelete();
                                response.StatusCode = 400;
                                response.Close();
                                return;
                            }

                            var targetFilename = (pkg.CanonicalName + ".msi").ToLower();
                            var location = new Uri(_packagePrefixUrl, targetFilename);
                            PackageManager.Instance.GetPackageDetails(pkg.CanonicalName, RepositoryServiceMain._messages).Wait();

                            //copy the package to the destination
                            if (_cloudFileSystem != null) {
                                // copy file to azure storage
                                _cloudFileSystem.WriteBlob(_packageStorageFolder, targetFilename, filename, false, (progress) => {
                                    ConsoleExtensions.PrintProgressBar("{0} => {1}".format(pkg.CanonicalName, _packageStorageFolder), progress);
                                });

                                if (pkg.Name.Equals("coapp.toolkit", StringComparison.CurrentCultureIgnoreCase) && pkg.PublicKeyToken.Equals("1e373a58e25250cb", StringComparison.CurrentCultureIgnoreCase)) {
                                    // update the default toolkit too
                                    _cloudFileSystem.WriteBlob(_packageStorageFolder, "coapp.toolkit.msi", filename, false, (progress) => {
                                        ConsoleExtensions.PrintProgressBar("{0} => {1}".format(_localfeedLocation, _packageStorageFolder), progress);
                                    });
                                    Console.WriteLine();
                                }

                                if (pkg.Name.Equals("coapp.devtools", StringComparison.CurrentCultureIgnoreCase) && pkg.PublicKeyToken.Equals("1e373a58e25250cb", StringComparison.CurrentCultureIgnoreCase)) {
                                    // update the default toolkit too
                                    _cloudFileSystem.WriteBlob(_packageStorageFolder, "coapp.devtools.msi", filename, false, (progress) => {
                                        ConsoleExtensions.PrintProgressBar("{0} => {1}".format(_localfeedLocation, _packageStorageFolder), progress);
                                    });
                                    Console.WriteLine();
                                }

                                // remove the local file, since we don't need it anymore.
                                filename.TryHardToDelete();

                                Console.WriteLine();
                            } else {
                                var targetLocation = Path.Combine(_packageStorageFolder, targetFilename);
                                if( File.Exists(targetLocation)) {
                                    targetLocation.TryHardToDelete();
                                }

                                File.Copy(filename, targetLocation);
                            }

                            lock(typeof(UploadedFileHandler)) {
                                // update the feed
                                var Feed = new AtomFeed();

                                if (!string.IsNullOrEmpty(_localfeedLocation) && File.Exists(_localfeedLocation)) {
                                    var originalFeed = AtomFeed.LoadFile(_localfeedLocation);
                                    Feed.Add(originalFeed.Items.Where(each => each is AtomItem).Select(each => each as AtomItem));
                                }

                                if (!string.IsNullOrEmpty(pkg.PackageItemText)) {
                                    var item = SyndicationItem.Load<AtomItem>(XmlReader.Create(new StringReader(pkg.PackageItemText)));

                                    var feedItem = Feed.Add(item);

                                    // first, make sure that the feeds contains the intended feed location.
                                    if (feedItem.Model.Feeds == null) {
                                        feedItem.Model.Feeds = new List<Uri>();
                                    }

                                    if (!feedItem.Model.Feeds.Contains(_canonicalFeedUrl)) {
                                        feedItem.Model.Feeds.Insert(0, _canonicalFeedUrl);
                                    }

                                    if (feedItem.Model.Locations == null) {
                                        feedItem.Model.Locations = new List<Uri>();
                                    }

                                    if (!feedItem.Model.Locations.Contains(location)) {
                                        feedItem.Model.Locations.Insert(0, location);
                                    }
                                }

                                Feed.Save(_localfeedLocation);
                                if (_cloudFileSystem != null) {
                                    _cloudFileSystem.WriteBlob(_packageStorageFolder, Path.GetFileName(_localfeedLocation).ToLower() , _localfeedLocation, false, (progress) => {
                                        ConsoleExtensions.PrintProgressBar("{0} => {1}".format(_localfeedLocation, _packageStorageFolder), progress);
                                    });
                                    Console.WriteLine();

                                    _cloudFileSystem.WriteBlob(_packageStorageFolder, Path.GetFileName(_localfeedLocation).ToLower()+".gz", _localfeedLocation, true, (progress) => {
                                        ConsoleExtensions.PrintProgressBar("{0} => {1}".format(_localfeedLocation+".gz", _packageStorageFolder), progress);
                                    });
                                    Console.WriteLine();
                                    
                                   
                                    
                                }
                            }

                            // Advertise the package on twitter
                            if (_tweeter != null) {
                                // pkg.Name
                                Bitly.Shorten(location.AbsoluteUri).ContinueWith(
                                    (x) => {
                                        var name = "[{0}-{1}-{2}]".format(pkg.Name, pkg.Version, pkg.Architecture);

                                        var summary = pkg.Summary;
                                        var l1 = 138 - (name.Length + x.Result.Length);
                                        if( summary.Length > l1 ) {
                                            summary = summary.Substring(0, l1 - 1) + "\u2026";
                                        }
                                        var text = "{0} {1} {2}".format(name, summary, x.Result);
                                        Console.WriteLine("Tweet: {0}",text);
                                        _tweeter.Tweet(text);
                                    });
                            }

                            response.StatusCode = 200;
                            response.Close();
                        }, TaskContinuationOptions.AttachedToParent);

                });

            result.ContinueWith(
                antecedent => {
                    if (result.IsFaulted) {
                        var e = antecedent.Exception.InnerException;
                        Console.WriteLine("Error handling uploaded package: {0} -- {1}\r\n{2}", e.GetType(), e.Message, e.StackTrace);
                        response.StatusCode = 500;
                        response.Close();
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);

            return result;
        }

        private void UpdateFeed(Package newPackage) {
          
        }
    }
}
