using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoApp.Bootstrapper {
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;

    internal class AsyncDownloader {
        internal Uri uri;
        internal FileStream filestream;
        internal HttpWebResponse httpWebResponse;
        internal TaskCompletionSource<HttpWebResponse> tcs;
        internal string Filename;
        internal Action<int> PercentComplete;
        internal long contentLength;

        internal static string Download(string serverUrl, string filename, Action<int> percentComplete = null) {
            var tempFilenme = Path.GetTempFileName() + filename;
            
            if (File.Exists(tempFilenme)) {
                File.Delete(tempFilenme);
            }
            
            return new AsyncDownloader {
                Filename = tempFilenme,
                PercentComplete = percentComplete,
                uri = new Uri(new Uri(serverUrl), filename)
            }.Get();
        }

        internal string Get() {
            var webRequest = (HttpWebRequest)WebRequest.Create(uri);
            webRequest.AllowAutoRedirect = true;
            webRequest.Method = WebRequestMethods.Http.Get;

            Task.Factory.FromAsync<WebResponse>(webRequest.BeginGetResponse, (Func<IAsyncResult, WebResponse>)webRequest.BetterEndGetResponse, this).
                ContinueWith(asyncResult => {
                    try {
                        if (SingleStep.Cancelling) {
                            return;
                        }

                        httpWebResponse = asyncResult.Result as HttpWebResponse;
                        if (httpWebResponse.StatusCode == HttpStatusCode.OK) {
                            contentLength = httpWebResponse.ContentLength;
                            try {
                                // open the file here, so that it's ready when we start the async read cycle.
                                filestream = File.Open(Filename, FileMode.Create);
                                tcs = new TaskCompletionSource<HttpWebResponse>(TaskCreationOptions.AttachedToParent);
                                tcs.Iterate(AsyncReadImpl());
                                return;
                            } catch (Exception e) {
                                Logger.Warning(e);
                            }
                        }
                    } catch (Exception e) {
                        Logger.Warning(e);
                    }

                }, TaskContinuationOptions.AttachedToParent).Wait();

            return File.Exists(Filename) ? Filename : null;
        }

        private IEnumerable<Task> AsyncReadImpl() {
            using (var responseStream = httpWebResponse.GetResponseStream()) {
                
                var total = 0L;
                var buffer = new byte[65535];
                while (true) {
                    if (SingleStep.Cancelling) {
                        tcs.SetResult(null);
                        break;
                    }

                    var read = Task<int>.Factory.FromAsync(responseStream.BeginRead, responseStream.EndRead, buffer, 0, buffer.Length, this);
                    yield return read;
                    var bytesRead = read.Result;
                    if (bytesRead == 0) {
                        break;
                    }
                    total += bytesRead;
                    if( PercentComplete != null ) {
                        PercentComplete((int)(contentLength <= 0 ? total : (int)(total*100/contentLength)));
                    }

                    // write to output file.
                    filestream.Write(buffer, 0, bytesRead);
                    filestream.Flush();
                }
                // end of the file!
                filestream.Close();

                try {
                    File.SetCreationTime(Filename, httpWebResponse.LastModified);
                    File.SetLastWriteTime(Filename, httpWebResponse.LastModified);
                    tcs.SetResult(null);
                } catch (Exception e) {
                    Logger.Warning(e);
                    tcs.SetException(e);
                }
            }
        }
    }

    public static class WebRequestExtensions {
        public static WebResponse BetterEndGetResponse(this WebRequest request, IAsyncResult asyncResult) {
            try {
                return request.EndGetResponse(asyncResult);
            } catch (WebException wex) {
                if (wex.Response != null) {
                    return wex.Response;
                }
                throw;
            }
        }

        public static void Iterate<TResult>(this TaskCompletionSource<TResult> tcs, IEnumerable<Task> asyncIterator) {
            var enumerator = asyncIterator.GetEnumerator();
            Action<Task> recursiveBody = null;
            recursiveBody = completedTask => {
                if (completedTask != null && completedTask.IsFaulted) {
                    tcs.TrySetException(completedTask.Exception.InnerExceptions);
                    enumerator.Dispose();
                } else if (enumerator.MoveNext()) {
                    enumerator.Current.ContinueWith(recursiveBody, TaskContinuationOptions.AttachedToParent | TaskContinuationOptions.ExecuteSynchronously);
                } else {
                    enumerator.Dispose();
                }
            };
            recursiveBody(null);
        }

        public static WebResponse BetterGetResponse(this WebRequest request) {
            try {
                return request.GetResponse();
            } catch (WebException wex) {
                if (wex.Response != null) {
                    return wex.Response;
                }
                throw;
            }
        }
    }
}
