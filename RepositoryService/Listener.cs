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
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Toolkit.Extensions;
    using Toolkit.Pipes;

    public class Listener {
        private readonly HttpListener _listener = new HttpListener();
        private readonly List<string> _hosts = new List<string>();
        private readonly List<int> _ports = new List<int>();
        private readonly Dictionary<string, RequestHandler> _paths = new Dictionary<string, RequestHandler>();
        private Task<HttpListenerContext> _current = null;
     
        public Listener() {
            
        }

        Regex ipAddrRx = new Regex(@"^([1-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])(\.([0-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])){3}$");
        Regex hostnameRx = new Regex(@"(([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9])\.)*");

        public void AddHost(string host) {
            if (string.IsNullOrEmpty(host)) {
                return;
            }

            host = host.ToLower();

            if( _hosts.Contains(host)) {
                return;
            }

            if (host == "+" || host == "*" || ipAddrRx.IsMatch(host) || hostnameRx.IsMatch(host)) {
                _hosts.Add(host);
                if( _current != null) {
                    Restart();
                }
                return;
            }
        }

        public void RemoveHost( string host ) {
            if (string.IsNullOrEmpty(host)) {
                return;
            }

            host = host.ToLower();

            if (_hosts.Contains(host)) {
                _hosts.Remove(host);
                if (_current != null) {
                    Restart();
                }
            }
        }

        public void AddPort(int port) {
            if( port <= 0 || port > 65535 ) {
                return;
            }

            if( _ports.Contains(port)) {
                return;
            }

            _ports.Add(port);
            if (_current != null) {
                Restart();
            }
        }

        public void RemovePort( int port ) {
            if (_ports.Contains(port)) {
                _ports.Remove(port);
                if (_current != null) {
                    Restart();
                }
            }
        }

        public void AddHandler( string path, RequestHandler handler ) {
            if(  string.IsNullOrEmpty(path) ) {
                path = "/";
            }
            path = path.ToLower();

            if( !path.StartsWith("/")) {
                path = "/" + path;
            }

            if (!path.EndsWith("/")) {
                path = path + "/";
            }

            if( _paths.ContainsKey(path)) {
                return;
            }

            _paths.Add( path, handler );
            if (_current != null) {
                Restart();
            }
        }

        public void RemoveHandler( string path ) {
            if (string.IsNullOrEmpty(path)) {
                path = "/";
            }

            path = path.ToLower();

            if (!path.StartsWith("/")) {
                path = "/" + path;
            }

            if (!path.EndsWith("/")) {
                path = path + "/";
            }

            if (_paths.ContainsKey(path)) {
                _paths.Remove(path);
                if (_current != null) {
                    Restart();
                }
            }
        }


        public void Restart() {
            try {
                Stop();
            } catch {
                
            }

            try {
                Start();
            } catch {

            }

        }

        public void Stop() {
            _listener.Stop();
            _current = null;
        }


        public void Start() {
            if( _current == null ) {
                _listener.Prefixes.Clear();
                foreach( var host in _hosts ) {
                    foreach( var port in _ports ) {
                        foreach( var path in _paths.Keys) {
                            
                            _listener.Prefixes.Add("http://{0}:{1}{2}".format(host, port, path));
                        }
                    }
                }
            }

            _listener.Start();

            _current  = Task.Factory.FromAsync<HttpListenerContext>(_listener.BeginGetContext, _listener.EndGetContext, _listener);

            _current.ContinueWith(antecedent => {
                if( antecedent.IsCanceled || antecedent.IsFaulted ) {
                    _current = null;
                    return;
                }
                Start(); // start a new listener.

                try {
                    var request = antecedent.Result.Request;
                    var response = antecedent.Result.Response;
                    var url = request.Url;
                    var path = url.AbsolutePath.ToLower();

                    var handlerKey = _paths.Keys.OrderByDescending(each => each.Length).Where(path.StartsWith).FirstOrDefault();
                    if (handlerKey == null) {
                        // no handler
                        response.StatusCode = 404;
                        response.Close();
                        return;
                    }

                    var relativePath = path.Substring(handlerKey.Length);

                    if( string.IsNullOrEmpty(relativePath) ) {
                        relativePath = "index";
                    }

                    var handler = _paths[handlerKey];
                    Task handlerTask = null;
                    var length = request.ContentLength64;

                    switch (request.HttpMethod) {

                        case "PUT":
                            try {
                                var putData = new byte[length];
                                var read = 0;
                                var offset = 0;
                                do {
                                    read = request.InputStream.Read(putData, offset, (int)length-offset);
                                    offset += read;
                                } while (read > 0 && offset < length);

                                handlerTask = handler.Put(response,relativePath, putData);
                            } catch (Exception e) {
                                Console.WriteLine("{0} -- {1}\r\n{2}", e.GetType(), e.Message, e.StackTrace);
                                response.StatusCode = 500;
                                response.Close();
                            }
                            break;

                        case "HEAD":
                            try {
                                handlerTask = handler.Head(response, relativePath, new UrlEncodedMessage(relativePath + "?" + url.Query));
                            } catch (Exception e) {
                                Console.WriteLine("{0} -- {1}\r\n{2}", e.GetType(), e.Message, e.StackTrace);
                                response.StatusCode = 500;
                                response.Close();
                            }
                            break;

                        case "GET":
                            try {
                                handlerTask = handler.Get(response, relativePath, new UrlEncodedMessage(relativePath + "?" + url.Query));
                            } catch (Exception e) {
                                Console.WriteLine("{0} -- {1}\r\n{2}", e.GetType(), e.Message, e.StackTrace);
                                response.StatusCode = 500;
                                response.Close();
                            }
                            break;

                        case "POST":
                            try {
                                var postData = new byte[length];
                                var read = 0;
                                var offset = 0;
                                do {
                                    read = request.InputStream.Read(postData, offset, (int)length - offset);
                                    offset += read;
                                } while (read > 0 && offset < length);

                                handlerTask = handler.Post(response, relativePath, new UrlEncodedMessage(relativePath + "?" + Encoding.UTF8.GetString(postData)));
                            } catch (Exception e) {
                                Console.WriteLine("{0} -- {1}\r\n{2}", e.GetType(), e.Message, e.StackTrace);
                                response.StatusCode = 500;
                                response.Close();
                            }
                            break;
                    }

                    if (handlerTask != null) {
                        handlerTask.ContinueWith( (antecedent2) => {
                            if (antecedent2.IsFaulted && antecedent2.Exception != null) {
                                var e = antecedent2.Exception.InnerException;
                                Console.WriteLine("{0} -- {1}\r\n{2}", e.GetType(), e.Message, e.StackTrace);
                                response.StatusCode = 500;
                            }

                            response.Close();
                        }, TaskContinuationOptions.AttachedToParent);
                    } else {
                        // nothing retured? must be unimplemented.
                        response.StatusCode = 405;
                        response.Close();
                    }
                } catch (Exception e) {
                    Console.WriteLine("{0} -- {1}\r\n{2}", e.GetType(), e.Message, e.StackTrace);
                }
            }, TaskContinuationOptions.AttachedToParent);
        }
    }
}