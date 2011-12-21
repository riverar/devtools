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
    using System.Net;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using Toolkit.Pipes;
    using Toolkit.Tasks;
    using Toolkit.Utility;

    public class CommitMessageHandler : RequestHandler {
        private Tweeter _tweeter;
        private ProcessUtility _cmdexe = new ProcessUtility("cmd.exe");

        public CommitMessageHandler(string twitterHandle) {
            if( !string.IsNullOrEmpty(twitterHandle) ) {
                _tweeter = new Tweeter(twitterHandle);
            }

            Environment.SetEnvironmentVariable("PATH", @"c:\git\cmd;c:\git\bin;c:\tools\bin;c:\tools" + Environment.GetEnvironmentVariable("PATH"));
        }

        public override Task Get(HttpListenerResponse response, string relativePath, UrlEncodedMessage message) {
            response.WriteString("<html><body>Relative Path: {0}<br>GET : <br>", relativePath);

            foreach( var key in message ) {
                response.WriteString("&nbsp;&nbsp;&nbsp;{0} = {1}<br>", key, message[key]);
            }

            response.WriteString("</body></html>");

            return "".AsResultTask();
        }

        
        public override Task Post(HttpListenerResponse response, string relativePath, UrlEncodedMessage message) {
            var payload = (string)message["payload"];
            if( payload == null ) {
                response.StatusCode = 500;
                response.Close();
                return "".AsResultTask();
            }

            var result = Task.Factory.StartNew( () => {
                try {
                    dynamic json = JObject.Parse(payload);
                    Console.WriteLine("MSG Process begin {0}", json.commits.Count);
                    
                    var count = json.commits.Count;
                    for (int i = 0; i < count; i++) {
                        var username = json.commits[i].author.username.Value;
                        var commitMessage = json.commits[i].message.Value;
                        var repository = json.repository.name.Value;
                        var handle = username;
                        var url = (string)json.commits[i].url.Value;
                        if (repository == "coapp.org" || repository == "new_coapp.org") {
                            Task.Factory.StartNew(() => {
                                Console.WriteLine("Rebuilding website.");

                                Console.WriteLine("(1) Pulling from github");
                                Environment.CurrentDirectory = @"c:\tools\new_coapp.org";
                                if (_cmdexe.Exec(@"/c git.cmd pull") != 0 ) {
                                    Console.WriteLine("Git Pull Failure:\r\n{0}", _cmdexe.StandardOut);
                                    return;
                                }

                                Console.WriteLine("(2) Running DocPad Generate");
                                var node = new ProcessUtility(@"tools\node.exe");
                                if( node.Exec(@"node_modules\coffee-script\bin\coffee node_modules\docpad\bin\docpad generate") != 0 ) {
                                    Console.WriteLine("DocPad Failure:\r\n{0}", node.StandardOut);
                                    return;
                                }

                                Console.WriteLine("Rebuilt Website.");
                            });
                        }

                        Bitly.Shorten(url).ContinueWith((bitlyAntecedent) => {
                            var commitUrl = bitlyAntecedent.Result;

                            // var handle = aliases.ContainsKey(username) ? aliases[username] : username;
                            var sz = repository.Length + handle.Length + commitUrl.Length + commitMessage.Length + 10;
                            var n = 140 - sz;

                            if (n < 0) {
                                commitMessage = commitMessage.Substring(0, (commitMessage.Length + n) - 3) + "...";
                            }
                            _tweeter.Tweet("[{0}]=>{1} via {2} {3}", repository, commitMessage, handle, commitUrl);
                            Console.WriteLine("[{0}]=>{1} via {2} {3}", repository, commitMessage, handle, commitUrl);
                        });

                        

                    } 
                } catch(Exception e) {
                    Console.WriteLine("Error handling uploaded package: {0} -- {1}\r\n{2}", e.GetType(), e.Message, e.StackTrace);
                    response.StatusCode = 500;
                    response.Close();
                }

            }, TaskCreationOptions.AttachedToParent);

            result.ContinueWith( antecedent => {
                if (result.IsFaulted) {
                    var e = antecedent.Exception.InnerException;
                    Console.WriteLine("Error handling commit message: {0} -- {1}\r\n{2}", e.GetType(), e.Message, e.StackTrace);
                    response.StatusCode = 500;
                    response.Close();
                }
            }, TaskContinuationOptions.OnlyOnFaulted);

            return result;
        }
    }
}