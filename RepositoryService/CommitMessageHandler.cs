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
    using System.Net;
    using System.Threading.Tasks;
    using Toolkit.Pipes;
    using Toolkit.Tasks;

    public class CommitMessageHandler : RequestHandler {
        private Tweeter _tweeter;
        public CommitMessageHandler(string twitterHandle) {
            if( !string.IsNullOrEmpty(twitterHandle) ) {
                _tweeter = new Tweeter(twitterHandle);
            }
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
            return null;
        }
    }
}