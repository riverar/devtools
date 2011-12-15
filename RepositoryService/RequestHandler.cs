using System.Collections.Generic;
using System.Linq;

namespace CoApp.RepositoryService {
    using System.Net;
    using System.Threading.Tasks;
    using Toolkit.Pipes;

    public class RequestHandler {
        public virtual Task Put(HttpListenerResponse response, string relativePath, byte[] data ) {
            return null;
        }

        public virtual Task Get(HttpListenerResponse response, string relativePath, UrlEncodedMessage message) {
            return null;
        }

        public virtual Task Post(HttpListenerResponse response, string relativePath, UrlEncodedMessage message) {
            return null;
        }

        public virtual Task Head(HttpListenerResponse response, string relativePath, UrlEncodedMessage message) {
            return null;
        }
    }
}
