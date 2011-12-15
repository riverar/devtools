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
    using System.Text;

    public static class HttpListenerResponseExtensions {
        public static void WriteString( this HttpListenerResponse response, string format, params string[] args ) {
            var text = string.Format(format, args);
            var buffer = Encoding.UTF8.GetBytes(text);
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Flush();
        }
    }
}