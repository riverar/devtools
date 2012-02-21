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
    using System.IO;
    using Developer.Toolkit.Publishing;
    using Toolkit.Engine;
    using Toolkit.Engine.Model;
    using Toolkit.Extensions;
    using Toolkit.Scripting.Languages.PropertySheet;
    using Toolkit.Win32;

    public class PackageAssembly {
        public string Name { get; set; }

        private Binary _firstPE;
        internal Binary FirstPEBinary { get { return _firstPE ?? (_firstPE = PEFiles.FirstOrDefault()); } }

        private IEnumerable<Binary> _peFiles;
        internal IEnumerable<Binary> PEFiles { get { return _peFiles ?? (_peFiles = BinaryFiles.Where(each => each.IsPEFile)); } }

        private IEnumerable<Binary> _binaryFiles;
        private IEnumerable<Binary> BinaryFiles { get { return _binaryFiles ?? (_binaryFiles = SourceFiles.Select(each => Binary.Load(each).Result)); }}

        private Architecture _architecture;
        public Architecture Architecture { get {
            if( _architecture == Architecture.Unknown) {
                if (FirstPEBinary.Is64Bit) {
                    _architecture = Architecture.x64;
                } else if (FirstPEBinary.IsAnyCpu) {
                    _architecture= Architecture.Any;
                }
                else {
                    _architecture = Architecture.x86;
                }
            }
            return _architecture;
        }}

        public Rule Rule { get; set; }
        private bool? _isManaged;
        private bool? _isPolicy;
        private bool? _isNative;

        private FourPartVersion _version;
        private bool? _isErrorFree;
        public string PublicKeyToken { get; set; }

        private readonly IEnumerable<FileEntry> _files;

        internal IEnumerable<FileEntry>  Files {get { return _files; } }

        internal IEnumerable<string>  SourceFiles {
            get { return _files.Select(each => each.SourcePath); }
        }

        public bool FilesAreSigned {
            get {
                return SourceFiles.All(Toolkit.Crypto.Verifier.HasValidSignature);
            }
        }

        public bool IsManaged {
            get {
                if( _isPolicy == true || _isNative == true) {
                    return false;
                }
                // if none of them are not managed. 
                return (_isManaged ?? (_isManaged = !PEFiles.Any(each => !each.IsManaged))).Value;
            }
        }

        public bool IsNative {
            get {
                if (_isPolicy == true || _isManaged == true) {
                    return false;
                }
                return (_isNative ?? (_isNative = !IsManaged)).Value;
            }
        }

        public bool IsNativePolicy {
            get { return _isPolicy == true; }
            set { _isPolicy = value; }
        }
        public FourPartVersion Version { 
            get {
                return _version == 0L ? (_version = FirstPEBinary.FileVersion) : _version;
            }
        }
        
        public bool IsErrorFree {
            get {
                if (_isErrorFree == null) {
                    _isErrorFree = true;

                    /*
                    if (IsManaged && SourceFiles.Count() > 1) {
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
            _files = new FileEntry(filename, Path.GetFileName(filename)).SingleItemAsEnumerable();
        }

        
        public PackageAssembly(string assemblyName, Rule rule, IEnumerable<string> files) {
            Name = assemblyName;
            Rule = rule;
            // when given just filenames, strip the 
            _files = files.Select(each => new FileEntry(each, Path.GetFileName(each)));
        }

        public PackageAssembly(string assemblyName, Rule rule, IEnumerable<FileEntry> files) {
            Name = assemblyName;
            Rule = rule;
            _files = files;
        }

        public PackageAssembly(string assemblyName, Rule rule, NativeManifest policyManifest) {
            Name = assemblyName;
            Rule = rule;
            _assemblyManifest = policyManifest;
            _isPolicy = true;
            _files = Enumerable.Empty<FileEntry>();
        }

        private NativeManifest _assemblyManifest;
        public string AssemblyManifest {
            get {
                if (_assemblyManifest == null) {
                    _assemblyManifest  = new NativeManifest(null) {
                        AssemblyName = Name,
                        AssemblyArchitecture = Architecture,
                        AssemblyLanguage = "*",
                        AssemblyVersion = Version,
                        AssemblyPublicKeyToken = PublicKeyToken,
                        AssemblyType = AssemblyType.win32
                    };

                    foreach( var file in _files) {
                        _assemblyManifest.AddFile(file.DestinationPath, file.SourcePath.GetFileSHA1());
                    }
                }
                return _assemblyManifest.ToString();
            }
        }

    }
}
