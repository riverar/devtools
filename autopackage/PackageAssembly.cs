//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoApp.Autopackage {
    using Toolkit.Engine;
    using Toolkit.Engine.Model;
    using Toolkit.Extensions;
    using Toolkit.Scripting.Languages.PropertySheet;

    public class PackageAssembly {
        public string Name { get; set; }

        private Architecture _architecture;
        public Architecture Architecture { get {
            if( _architecture == Architecture.Unknown) {
                var firstPE = Filenames.Select(Toolkit.Win32.PEInfo.Scan).Where(each => each.IsPEBinary).FirstOrDefault();
                if( firstPE.Is64Bit ) {
                    _architecture = Architecture.x64;
                } else if ( firstPE.IsAny ) {
                    _architecture= Architecture.Any;
                }
                else {
                    _architecture = Architecture.x86;
                }
            }
            return _architecture;
        }}

        public Rule Rule { get; set; }
        internal IEnumerable<string> Filenames;
        private bool? _isManaged;
        private string _version;
        private bool? _isErrorFree;
        public string PublicKeyToken { get; set; }

        public bool FilesAreSigned {
            get {
                return !Filenames.Any(file => !Toolkit.Crypto.Verifier.HasValidSignature(file));
            }
        }

        public bool IsManaged {
            get {
                if( _isManaged == null ) {
                    _isManaged =!Filenames.Select(Toolkit.Win32.PEInfo.Scan).Any(info => info.IsPEBinary && !info.IsManaged);
                }
                return _isManaged.Value;
            }
        }

        public string Version { 
            get {
                if (_version == null) {
                    foreach (var s in Filenames.Select(Toolkit.Win32.PEInfo.Scan).Where(s => s.IsPEBinary)) {
                        _version = s.FileVersion;
                        break;
                    }
                }
                return _version;
            }
        }
        
        public bool IsErrorFree {
            get {
                if (_isErrorFree == null) {
                    _isErrorFree = true;

                    /*
                    if (IsManaged && Filenames.Count() > 1) {
                        AutopackageMessages.Invoke.Error(
                            MessageCode.ManagedAssemblyWithMoreThanOneFile, Rule.SourceLocation, "Managed assembly '{0}' with more than one file in include",
                            Name);
                        _isErrorFree = false;
                    }
                     // managed assemblies need to support more than one file :)
                     * */

                   
                }
                return _isErrorFree.Value;
            }
        }

        public PackageAssembly(string assemblyName, Rule rule ,string filename ) {
            Name = assemblyName;
            Rule = rule;
            Filenames = filename.SingleItemAsEnumerable();
        }

        public PackageAssembly(string assemblyName, Rule rule, IEnumerable<string> filenames) {
            Name = assemblyName;
            Rule = rule;
            Filenames = filenames;
        }
    }
}
