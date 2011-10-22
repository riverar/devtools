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

namespace CoApp.Developer.Toolkit.Publishing {
    using System.ComponentModel;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using CoApp.Toolkit.Extensions;
    using CoApp.Toolkit.Win32;
    using Microsoft.Cci;
    using Microsoft.Cci.MutableCodeModel;
    using ResourceLib;
    using Resource = ResourceLib.Resource;

    public class PeBinary : IDisposable {

        // const byte PUBLICKEYBLOB	= 	0x06;
        // const byte CUR_BLOB_VERSION	= 	0x02;
        // const ushort reserved 		= 	0x0000;
        // const uint CALG_RSA_KEYX 	= 	0x0000a400;
        // const uint CALG_RSA_SIGN 	= 	0x00002400; 

        // There is an interesting class here that may shed more light on all this. http://www.jensign.com/JavaScience/dotnet/JKeyNet/RSAPubKeyData.cs
        // info:
        // Public Key Blobs are documented here: http://msdn.microsoft.com/en-us/library/Aa387459
        // Private Key Blobs are documented here: http://msdn.microsoft.com/en-us/library/Aa387401


        private readonly string _filename;
        private bool _pendingChanges;
        private readonly PEInfo _info;
        private MetadataReaderHost _host;
        private static Dictionary<string, PeBinary> _cache = new Dictionary<string, PeBinary>();

        public static PeBinary Load(string filename) {
            return !_cache.ContainsKey(filename) ? new PeBinary(filename) : _cache[filename];
        }

        public static PeBinary FindAssembly(string assemblyname, string version) {
            lock (_cache) {
                var asm =
                    _cache.Values.Where(
                        each => each.IsManaged && each.MutableAssembly.Name.Value == assemblyname && each.MutableAssembly.Version.ToString() == version).
                        FirstOrDefault();
                if (asm == null) {
                    // see if we can find it in the same folder as one of the assemblies we already have.
                    foreach (var folder in _cache.Keys.Select(each => Path.GetDirectoryName(each.GetFullPath()).ToLower()).Distinct()) {
                        var probe = Path.Combine(folder, assemblyname) + ".dll";
                        if (File.Exists(probe)) {
                            var probeAsm = Load(probe);
                            if (probeAsm.IsManaged && probeAsm.MutableAssembly.Name.Value == assemblyname &&
                                probeAsm.MutableAssembly.Version.ToString() == version) {
                                asm = probeAsm;
                                break;
                            }
                        }
                    }
                }
                return asm;
            }
        }

        private PeBinary(string filename) {
            filename = filename.GetFullPath();
            if(!File.Exists(filename) ) {
                throw new FileNotFoundException("Unable to find file", filename);
            }
            _filename = filename;

            _info = PEInfo.Scan(filename);
            if(!_info.IsPEBinary) {
                throw new Exception("File {0} does not appear to be a PE Binary".format(filename));
            }

            lock (typeof (PeBinary)) { // CCI operations are not always threadsafe. :S
                using (var ri = new ResourceInfo()) {
                    // lets pull out the relevant resources first.
                    ri.Load(_filename);
                    try {
                        
                        var versionKey = ri.Resources.Keys.Where(each => each.ResourceType == ResourceTypes.RT_VERSION).FirstOrDefault();
                        var versionResource = ri.Resources[versionKey].First() as VersionResource;
                        var versionStringTable = (versionResource["StringFileInfo"] as StringFileInfo).Strings.Values.First();

                        _comments = TryGetVersionString(versionStringTable, "Comments");
                        _companyName = TryGetVersionString(versionStringTable, "CompanyName");
                        _productName = TryGetVersionString(versionStringTable, "ProductName");
                        _assemblyVersion = TryGetVersionString(versionStringTable, "Assembly Version");
                        _fileVersion = TryGetVersionString(versionStringTable, "FileVersion");
                        _internalName = TryGetVersionString(versionStringTable, "InternalName");
                        _originalFilename = TryGetVersionString(versionStringTable, "OriginalFilename");
                        _legalCopyright = TryGetVersionString(versionStringTable, "LegalCopyright");
                        _legalTrademarks = TryGetVersionString(versionStringTable, "LegalTrademarks");
                        _fileDescription = TryGetVersionString(versionStringTable, "FileDescription");
                        _bugTracker = TryGetVersionString(versionStringTable, "BugTracker");
                    }
                    catch {
                        // skip it if this fails.
                    }
                }

                if (IsManaged) { // we can read in the binary using CCI
                    _host = new PeReader.DefaultHost();

                    try {
                        
                        if (MutableAssembly != null) {
                           

                            // we should see if we can get assembly attributes, since sometimes they can be set, but not the native ones.
                            foreach (var a in MutableAssembly.ContainingAssembly.AssemblyAttributes) {
                                var attributeArgument = (a.Arguments.FirstOrDefault() as MetadataConstant);
                                if (attributeArgument != null) {
                                    var attributeName = a.Type.ToString();
                                    var attributeValue = attributeArgument.Value.ToString();
                                    if (!string.IsNullOrEmpty(attributeValue)) {
                                        switch (attributeName) {
                                            case "System.Reflection.AssemblyTitleAttribute":
                                                if (string.IsNullOrEmpty(AssemblyTitle)) {
                                                    AssemblyTitle = attributeValue;
                                                }
                                                break;
                                            case "System.Reflection.AssemblyCompanyAttribute":
                                                if (string.IsNullOrEmpty(AssemblyCompany)) {
                                                    AssemblyCompany = attributeValue;
                                                }
                                                break;
                                            case "System.Reflection.AssemblyProductAttribute":
                                                if (string.IsNullOrEmpty(AssemblyProduct)) {
                                                    AssemblyProduct = attributeValue;
                                                }
                                                break;
                                            case "System.Reflection.AssemblyVersionAttribute":
                                                if (string.IsNullOrEmpty(AssemblyVersion)) {
                                                    AssemblyVersion = attributeValue;
                                                }
                                                break;
                                            case "System.Reflection.AssemblyFileVersionAttribute":
                                                if (string.IsNullOrEmpty(AssemblyFileVersion)) {
                                                    AssemblyFileVersion = attributeValue;
                                                }
                                                if (string.IsNullOrEmpty(_productVersion)) {
                                                    _productVersion = attributeValue;
                                                }
                                                break;
                                            case "System.Reflection.AssemblyCopyrightAttribute":
                                                if (string.IsNullOrEmpty(AssemblyCopyright)) {
                                                    AssemblyCopyright = attributeValue;
                                                }
                                                break;
                                            case "System.Reflection.AssemblyTrademarkAttribute":
                                                if (string.IsNullOrEmpty(AssemblyTrademark)) {
                                                    AssemblyTrademark = attributeValue;
                                                }
                                                break;
                                            case "System.Reflection.AssemblyDescriptionAttribute":
                                                if (string.IsNullOrEmpty(AssemblyDescription)) {
                                                    AssemblyDescription = attributeValue;
                                                }
                                                break;
                                            case "BugTrackerAttribute":
                                                if (string.IsNullOrEmpty(BugTracker)) {
                                                    BugTracker = attributeValue;
                                                }
                                                break;
                                        }
                                    }
                                }
                            }
                        }

                    }
                    catch
                    {
                    }
                }
            }
            lock (_cache) {
                _cache.Add(_filename, this);
            }

            // check each of the assembly references, 
            if (IsManaged) {
                foreach (var ar in MutableAssembly.AssemblyReferences) {
                    if (!ar.PublicKeyToken.Any()) {
                        // dependent assembly isn't signed. 
                        // look for it.
                        var dep = FindAssembly(ar.Name.Value, ar.Version.ToString());
                        if( dep == null) {
                            Console.WriteLine("WARNING: Unsigned Dependent Assembly {0}-{1} not found.",ar.Name.Value, ar.Version.ToString());
                        }
                    }
                }
            }
            _pendingChanges = false;
        }

        private static string TryGetVersionString(StringTable stringTable, string name) {
            try {
                var result = stringTable[name];
                if (!string.IsNullOrEmpty(result)) {
                    return result;
                }
            }
            catch {
                
            }
            return null;
        }
        
        public bool IsManaged {
            get {
                return _info.IsManaged;
            }
        }

        public bool ILOnly { get; private set; }

        private Assembly _mutableAssembly;
        private Assembly MutableAssembly { 
            get {
                if( !IsManaged ) {
                    return null;
                }

                if (_mutableAssembly == null) {
                    // copy this to a temporary file, because it locks the file until we're *really* done.
                    var temporaryCopy = _filename.CreateBackupWorkingCopy();
                    try {

                        var module = _host.LoadUnitFrom(temporaryCopy) as IModule;

                        if (module == null || module is Dummy) {
                            throw new Exception("{0} is not a PE file containing a CLR module or assembly.".format(_filename));
                        }
                        ILOnly = module.ILOnly;
                        
                        //Make a mutable copy of the module.
                        var copier = new MetadataDeepCopier(_host);
                        var mutableModule = copier.Copy(module);

                        //Traverse the module. In a real application the MetadataVisitor and/or the MetadataTravers will be subclasses
                        //and the traversal will gather information to use during rewriting.
                        var traverser = new MetadataTraverser() {
                            PreorderVisitor = new MetadataVisitor(),
                            TraverseIntoMethodBodies = true
                        };
                        traverser.Traverse(mutableModule);

                        //Rewrite the mutable copy. In a real application the rewriter would be a subclass of MetadataRewriter that actually does something.
                        var rewriter = new MetadataRewriter(_host);
                        _mutableAssembly = rewriter.Rewrite(mutableModule) as Assembly;
                    }
                    finally {
                        // delete it, or at least trash it & queue it up for next reboot.
                        temporaryCopy.TryHardToDeleteFile();
                    }
                }
                return _mutableAssembly;
            }
        }

        private string _comments;       //AssemblyDescription
        private string _companyName;    //AssemblyCompany
        private string _productName;    //AssemblyProduct
        private string _assemblyVersion; //AssemblyVersion
        private string _fileVersion;    //AssemblyFileVersion, 
        private string _productVersion;    //<AssemblyFileVersion>
        private string _internalName;   //<filename>
        private string _originalFilename;   //<filename>
        private string _legalCopyright; //AssemblyCopyright
        private string _legalTrademarks;//AssemblyTrademark
        private string _bugTracker;     //AssemblyBugtracker
        private string _fileDescription; //AssemblyTitle

        public string AssemblyTitle {
            get { return FileDescription; }
            set { FileDescription = value; }
        } 
        public string FileDescription {
            get { return _fileDescription; }
            set { _pendingChanges = true; _fileDescription = value; }
        }

        public string BugTracker {
            get { return _bugTracker; }
            set { _pendingChanges = true; _bugTracker = value; }
        }
        public string AssemblyTrademark {
            get { return LegalTrademarks; }
            set { _pendingChanges = true; LegalTrademarks = value; }
        } 
        public string LegalTrademarks {
            get { return _legalTrademarks; }
            set { _pendingChanges = true; _legalTrademarks = value; }
        }
        public string AssemblyCopyright {
            get { return LegalCopyright; }
            set { _pendingChanges = true;  LegalCopyright = value; }
        } 
        public string LegalCopyright {
            get { return _legalCopyright; }
            set { _pendingChanges = true; _legalCopyright = value; }
        }
        public string InternalName {
            get { return _internalName; }
            set { _pendingChanges = true; _internalName = value; }
        }
        public string OriginalFilename {
            get { return _originalFilename; }
            set { _pendingChanges = true; _originalFilename = value; }
        }
        public string ProductVersion {
            get { return _productVersion; }
            set { _pendingChanges = true; _productVersion = value; }
        } 
        public string AssemblyFileVersion {
            get { return FileVersion; }
            set { _pendingChanges = true; FileVersion = value; }
        } 
        public string FileVersion {
            get { return _fileVersion; }
            set { _pendingChanges = true; _fileVersion = value; }
        }
        public string AssemblyVersion {
            get { return _assemblyVersion; }
            set { _pendingChanges = true; _assemblyVersion = value; }
        }
        public string AssemblyProduct {
            get { return ProductName; }
            set { _pendingChanges = true; ProductName = value; }
        } 
        public string ProductName {
            get { return _productName; }
            set { _pendingChanges = true; _productName = value; }
        }
        public string AssemblyDescription {
            get { return Comments; }
            set { _pendingChanges = true; Comments = value; }
        } 
        public string Comments {
            get { return _comments; }
            set { _pendingChanges = true; _comments = value; }
        }
        public string AssemblyCompany {
            get { return CompanyName; }
            set { _pendingChanges = true; CompanyName = value; }
        } 
        public string CompanyName {
            get { return _companyName; }
            set { _pendingChanges = true; _companyName = value; }
        }

        private CertificateReference _strongNameKeyCertificate;
        public CertificateReference StrongNameKeyCertificate {
            get {
                return _strongNameKeyCertificate;
            }
            set { 
                _strongNameKeyCertificate = value;
                _pendingChanges = true;
                var pubKey = (_strongNameKeyCertificate.Certificate.PublicKey.Key as RSACryptoServiceProvider).ExportCspBlob(false);
                var strongNameKey = new byte[pubKey.Length + 12];
                // the strong name key requires a header in front of the public key:
                    // unsigned int SigAlgId;
                    // unsigned int HashAlgId;
                    // ULONG cbPublicKey;

                pubKey.CopyTo(strongNameKey, 12);

                // Set the AlgId in the header (CALG_RSA_SIGN)
                strongNameKey[0] = 0;
                strongNameKey[1] = 0x24;
                strongNameKey[2] = 0;
                strongNameKey[3] = 0;
 
                // Set the AlgId in the key blob (CALG_RSA_SIGN)
                // I've noticed this comes from the RSACryptoServiceProvider as CALG_RSA_KEYX
                // but for strong naming we need it to be marked CALG_RSA_SIGN
                strongNameKey[16] = 0;
                strongNameKey[17] = 0x24;
                strongNameKey[18] = 0;
                strongNameKey[19] = 0;

                // set the hash id (SHA_1-Hash -- 0x8004)
                // Still not sure *where* this value comes from. 
                strongNameKey[4] = 0x04;
                strongNameKey[5] = 0x80;
                strongNameKey[6] = 0;
                strongNameKey[7] = 0;
                
                strongNameKey[8] = (byte)(pubKey.Length);
                strongNameKey[9] = (byte)(pubKey.Length >> 8);
                strongNameKey[10] = (byte)(pubKey.Length >> 16);
                strongNameKey[11] = (byte)(pubKey.Length >> 24);

                StrongNameKey = strongNameKey;
            }
        }

        private byte[] _strongNameKey;
        public byte[] StrongNameKey {
            get {
                return _strongNameKey;
            }
            set { 
                _strongNameKey = value;
                _pendingChanges = true;
            }
        }

        public string PublicKeyToken {
            get {
                if (_strongNameKey == null) {
                    return null;
                }
                return UnitHelper.ComputePublicKeyToken(_strongNameKey).ToHexString();
            }
        }

        private CertificateReference _signingCertificate;
        public CertificateReference SigningCertificate {
            get { return _signingCertificate; }
            set {
                _signingCertificate = value;
                _pendingChanges = true;
            }
        }

        public void Save() {
            if (_pendingChanges) {
                // saves any changes made to the binary.
                lock (typeof (PeBinary)) {
                    // operations are not always threadsafe. :S
                    // work on a back up of the file
                    var tmpFilename = _filename.CreateBackupWorkingCopy();

                    try {
                        // remove any digital signatures from the binary before doing anything
                        if (!IsManaged) {
                            StripSignatures(tmpFilename); // this is irrelevant if the binary is managed--we'll be writing out a new one.
                        }
                        // rewrite any native resources that we want to change.

                        if (IsManaged && ILOnly) { // we can only edit the file if it's IL only, mixed mode assemblies can only be strong named, signed and native-resource-edited.
                            // set the strong name key data
                            MutableAssembly.PublicKey = StrongNameKey.ToList();

                            // change any assembly attributes we need to change
                            if (MutableAssembly != null) {
                                if (StrongNameKeyCertificate != null) {
                                    foreach (var ar in MutableAssembly.AssemblyReferences) {
                                        if (!ar.PublicKeyToken.Any()) {
                                            var dep = FindAssembly(ar.Name.Value, ar.Version.ToString());
                                            if (dep == null) {
                                                // can't strong name a file that doesn't have its deps all strong named.
                                                throw new Exception(
                                                    "Unable to strong name '{0}' -- dependent assembly '{1}-{2}' not available for strong naming".format(
                                                        _filename, ar.Name.Value, ar.Version.ToString()));
                                            }

                                            if (dep.MutableAssembly.PublicKey.IsNullOrEmpty()) {
                                                Console.WriteLine(
                                                    "Warning: Non-strong-named dependent reference found: '{0}-{1}' updating with same strong-name-key.",
                                                    ar.Name, ar.Version);
                                                dep.StrongNameKeyCertificate = StrongNameKeyCertificate;
                                                dep.SigningCertificate = SigningCertificate;
                                                dep.Save();
                                            }
                                            (ar as AssemblyReference).PublicKeyToken = dep.MutableAssembly.PublicKeyToken.ToList();
                                            (ar as AssemblyReference).PublicKey = dep.MutableAssembly.PublicKey;
                                        }
                                    }
                                }

                                // we should see if we can get assembly attributes, since sometimes they can be set, but not the native ones.
                                try {
                                    foreach (var a in MutableAssembly.AssemblyAttributes) {
                                        var attributeArgument = (a.Arguments.FirstOrDefault() as Microsoft.Cci.MutableCodeModel.MetadataConstant);
                                        if (attributeArgument != null) {
                                            var attributeName = a.Type.ToString();
                                            switch (attributeName) {
                                                case "System.Reflection.AssemblyTitleAttribute":
                                                    attributeArgument.Value = string.IsNullOrEmpty(AssemblyTitle) ? string.Empty : AssemblyTitle;
                                                    break;
                                                case "System.Reflection.AssemblyDescriptionAttribute":
                                                    attributeArgument.Value = string.IsNullOrEmpty(AssemblyDescription) ? string.Empty : AssemblyDescription;
                                                    break;
                                                case "System.Reflection.AssemblyCompanyAttribute":
                                                    attributeArgument.Value = string.IsNullOrEmpty(AssemblyCompany) ? string.Empty : AssemblyCompany;
                                                    break;
                                                case "System.Reflection.AssemblyProductAttribute":
                                                    attributeArgument.Value = string.IsNullOrEmpty(AssemblyProduct) ? string.Empty : AssemblyProduct;
                                                    break;
                                                case "System.Reflection.AssemblyVersionAttribute":
                                                    attributeArgument.Value = string.IsNullOrEmpty(AssemblyVersion) ? string.Empty : AssemblyVersion;
                                                    break;
                                                case "System.Reflection.AssemblyFileVersionAttribute":
                                                    attributeArgument.Value = string.IsNullOrEmpty(AssemblyFileVersion) ? string.Empty : AssemblyFileVersion;
                                                    break;
                                                case "System.Reflection.AssemblyCopyrightAttribute":
                                                    attributeArgument.Value = string.IsNullOrEmpty(AssemblyCopyright) ? string.Empty : AssemblyCopyright;
                                                    break;
                                                case "System.Reflection.AssemblyTrademarkAttribute":
                                                    attributeArgument.Value = string.IsNullOrEmpty(AssemblyTrademark) ? string.Empty : AssemblyTrademark;
                                                    break;
                                                case "BugTrackerAttribute":
                                                    attributeArgument.Value = string.IsNullOrEmpty(BugTracker) ? string.Empty : BugTracker;
                                                    break;
                                            }
                                        }
                                    }
                                } catch {
                                    // hmm. carry on.
                                }
                            }

                            // save it to disk
                            using (var peStream = File.Create(tmpFilename)) {
                                PeWriter.WritePeToStream(MutableAssembly, _host, peStream);
                            }
                        }
                        

                        // update native metadata 
                        try {
                            var ri = new ResourceInfo();

                            ri.Load(tmpFilename);

                            VersionResource versionResource;
                            StringTable versionStringTable;

                            var versionKey = ri.Resources.Keys.Where(each => each.ResourceType == ResourceTypes.RT_VERSION).FirstOrDefault();
                            if( versionKey != null ) {
                                versionResource = ri.Resources[versionKey].First() as VersionResource;    
                                versionStringTable = (versionResource["StringFileInfo"] as StringFileInfo).Strings.Values.First();
                            } else {
                                versionResource = new VersionResource();
                                ri.Resources.Add(new ResourceId(ResourceTypes.RT_VERSION), new List<Resource> {versionResource});
                                
                                var sfi = new StringFileInfo();
                                versionResource["StringFileInfo"] = sfi;
                                sfi.Strings["040904b0"] = (versionStringTable = new StringTable("040904b0"));

                                var vfi = new VarFileInfo();
                                versionResource["VarFileInfo"] = vfi;
                                var translation = new VarTable("Translation");
                                vfi.Vars["Translation"] = translation;
                                translation[0x0409] = 0x04b0;
                            }

                            versionResource.FileVersion = FileVersion;
                            versionResource.ProductVersion = ProductVersion;

                            versionStringTable["ProductName"] = ProductName;
                            versionStringTable["CompanyName"] = CompanyName;
                            versionStringTable["FileDescription"] = FileDescription;
                            versionStringTable["Comments"] = _comments;
                            versionStringTable["Assembly Version"] = _assemblyVersion;
                            versionStringTable["FileVersion"] = _fileVersion;
                            versionStringTable["ProductVersion"] = _productVersion;
                            versionStringTable["InternalName"] = _internalName;
                            versionStringTable["OriginalFilename"] = _originalFilename;
                            versionStringTable["LegalCopyright"] = _legalCopyright;
                            versionStringTable["LegalTrademarks"] = _legalTrademarks;
                            versionStringTable["BugTracker"] = _bugTracker;

                            versionResource.SaveTo(tmpFilename);
                        } catch {
                            
                        }

                        // strong name the binary
                        if (IsManaged && StrongNameKeyCertificate != null && (StrongNameKeyCertificate.Certificate.PrivateKey is RSACryptoServiceProvider)) {
                            ApplyStrongName(tmpFilename, StrongNameKeyCertificate);
                        }

                        // sign the binary
                        if (_signingCertificate != null) {
                            SignFile(tmpFilename, SigningCertificate.Certificate);
                        }

                        _filename.TryHardToDeleteFile();
                        File.Move(tmpFilename, _filename);  

                    } catch (Exception e) {
                        // get rid of whatever we tried
                        tmpFilename.TryHardToDeleteFile();
                        
                        Console.WriteLine(e.StackTrace);

                        // as you were...
                        throw;
                    }
                }
            }
            _pendingChanges = false;
        }


        /// <summary>
        /// This puts the strong name into the actual file on disk.
        /// 
        /// The file MUST be delay signed by this point.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="?"></param>
        public static void ApplyStrongName(string filename, CertificateReference certificate) {
            filename = filename.GetFullPath();
            filename.TryHardToMakeFileWriteable();

            if (!File.Exists(filename)) {
                throw new FileNotFoundException("Can't find file", filename);
            }

            // strong name the binary using the private key from the certificate.
            var wszKeyContainer = Guid.NewGuid().ToString();
            var privateKey = (certificate.Certificate.PrivateKey as RSACryptoServiceProvider).ExportCspBlob(true);
            if (!Mscoree.StrongNameKeyInstall(wszKeyContainer, privateKey, privateKey.Length)) {
                throw new Exception("Unable to create KeyContainer");
            }
            if (!Mscoree.StrongNameSignatureGeneration(filename, wszKeyContainer, IntPtr.Zero, 0, 0, 0)) {
                throw new Exception("Unable Strong name assembly '{0}'.".format(filename));
            }
            Mscoree.StrongNameKeyDelete(wszKeyContainer);
        }

        public static void StripSignatures( string filename ) {
            filename = filename.GetFullPath();
            filename.TryHardToMakeFileWriteable();

            if( !File.Exists(filename)) {
                throw new FileNotFoundException("Can't find file", filename );
            }

            using (var f = File.Open(filename, FileMode.Open)) {
                uint certCount = 0;
                var rc = ImageHlp.ImageEnumerateCertificates(f.SafeFileHandle, CertSectionType.Any, out certCount, IntPtr.Zero, 0);
                if (!rc) {
                    throw new Exception("Failed to find certificates in file {0}".format(filename));
                }

                var errCount = 0;
                for (uint certIndex = 0; certIndex < certCount; certIndex++) {
                    if (!ImageHlp.ImageRemoveCertificate(f.SafeFileHandle, certIndex)) {
                        errCount++;
                    }
                }

                if (errCount != 0) {
                    throw new Exception("Had errors removing {0} certificates from file {1}".format(errCount, filename));
                }
            }
        }
        public static void SignFile(string filename, CertificateReference certificate) {
            SignFile(filename, certificate.Certificate);
        }

        public static void SignFile(string filename, X509Certificate2 certificate) {
            filename = filename.GetFullPath();
            filename.TryHardToMakeFileWriteable();

            if (!File.Exists(filename)) {
                throw new FileNotFoundException("Can't find file", filename);
            }

            // Variables
            //
            var digitalSignInfo = default(DigitalSignInfo);
            // var signContext = default(DigitalSignContext);
            var pSignContext = IntPtr.Zero;

            // FileStream fileOut = null;
            // BinaryWriter binWriter = null;
            // byte[] blob = null;

            try {
                // Prepare signing info: exe and cert
                //
                digitalSignInfo = new DigitalSignInfo();
                digitalSignInfo.dwSize = Marshal.SizeOf(digitalSignInfo);
                digitalSignInfo.dwSubjectChoice = DigitalSignSubjectChoice.File;
                digitalSignInfo.pwszFileName = filename;
                digitalSignInfo.dwSigningCertChoice = DigitalSigningCertificateChoice.Certificate;
                digitalSignInfo.pSigningCertContext = certificate.Handle;
                digitalSignInfo.pwszTimestampURL = "http://timestamp.comodoca.com/authenticode"; // it's sometimes dying when we give it a timestamp url....

                digitalSignInfo.dwAdditionalCertChoice = DigitalSignAdditionalCertificateChoice.AddChainNoRoot;
                digitalSignInfo.pSignExtInfo = IntPtr.Zero;

                var digitalSignExtendedInfo = new DigitalSignExtendedInfo("description", "http://moerinfo");
                var ptr = Marshal.AllocCoTaskMem(Marshal.SizeOf(digitalSignExtendedInfo));
                Marshal.StructureToPtr(digitalSignExtendedInfo, ptr, false);
                // digitalSignInfo.pSignExtInfo = ptr;


                // Sign exe
                //
                if ((!CryptUi.CryptUIWizDigitalSign(DigitalSignFlags.NoUI, IntPtr.Zero, null, ref digitalSignInfo, ref pSignContext))) {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "CryptUIWizDigitalSign - "+filename);
                }


                // Get the blob with the signature
                //
                // signContext = (DigitalSignContext) Marshal.PtrToStructure(pSignContext, typeof (DigitalSignContext));

                // Free blob
                //
                if ((!CryptUi.CryptUIWizFreeDigitalSignContext(pSignContext))) {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "CryptUIWizFreeDigitalSignContext");
                }

                // Free additional Info
                Marshal.FreeCoTaskMem(ptr);
            }
            catch (Win32Exception ex) {
			// Any expected errors?
			//
			Console.WriteLine(ex.Message + " error# {0:x}" , ex.NativeErrorCode);
		} catch (Exception ex) {
			// Any unexpected errors?
			//
			Console.WriteLine(ex.Message);
		}
        }

        public void Dispose() {
            _mutableAssembly = null;
            _host.Dispose();
            _host = null;
        }
    }
}
