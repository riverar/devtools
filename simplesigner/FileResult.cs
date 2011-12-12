//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011  Garrett Serack. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace coapp_simplesigner {
    using System;
    using CoApp.Toolkit.Extensions;

    public class FileResult {
        public string FullPath = string.Empty;
        public string OriginalMD5 = string.Empty;
        public string NewMD5 = string.Empty;

        public string Message = string.Empty;
        public bool AlreadySigned = false;
        public ConsoleColor Color = ConsoleColor.Green;
    }
}