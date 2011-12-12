//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Developer.Toolkit.Exceptions {
    using CoApp.Toolkit.Exceptions;
    using CoApp.Toolkit.Extensions;

    public class FailedTimestampException : CoAppException {
        public FailedTimestampException(string filename, string timestampurl)
            : base("Failed to get timestamp for '{0}' from '{1}'".format(filename, timestampurl)) {
        }
    }

    public class DigitalSignFailure : CoAppException {
        public uint Win32Code;
        public DigitalSignFailure(string filename, uint win32Code)
            : base("Failed to digitally sign '{0}' Win32 RC: '{1:x}'".format(filename, win32Code)) {
            Win32Code = win32Code;
        }
    }

    public class AssemblyNotFoundException : CoAppException {
        public AssemblyNotFoundException(string filename, string version)
            : base("Failed to find assembly '{0}' version: '{1}'".format(filename, version)) {
        }
    }
}

