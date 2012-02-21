using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoApp.Developer.Toolkit.Publishing {
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using CoApp.Toolkit.Exceptions;
    using CoApp.Toolkit.Extensions;
    using CoApp.Toolkit.Tasks;
    using CoApp.Toolkit.Utility;
    using CoApp.Toolkit.Win32;
    using Exceptions;
    using Microsoft.Cci;
    using Microsoft.Cci.MutableCodeModel;
    using ResourceLib;
    using Resource = ResourceLib.Resource;

    [Flags]
    public enum BinaryLoadOptions {
        DelayLoad       = 0,        // load nothing by default

        PEInfo          = 1,        // load any PE header information 
        VersionInfo     = 2,        // load any file version information
        DependencyData  = 4,        // load any information about native dependencies

        Managed         = 8,        // explicitly load managed IL 
        Resources       = 16,        // explicitly load resources
        Manifest        = 32,        // explicitly load manifest
        
        MD5             = 64,       // calculate the MD5 hash

        NoManaged       = 128,        // don't do any managed-code stuff at all.
        NoResources     = 256,       // don't change any native resources
        NoManifest      = 512,       // don't change any manifest data
        NoSignature     = 1024,      // don't attempt to sign this when you save it.

        ValidateSignature = 2048,     // validate that this file has a valid signature

        UnsignedManagedDependencies     = 32768,        // loads unsigned dependent assemblies too
        NoUnsignedManagedDependencies   = 65536,        // don't load unsigned dependent assemblies too

        All = PEInfo | VersionInfo | Managed | Resources | Manifest | UnsignedManagedDependencies, // explictly preload all useful data
    }

    public class CoAppBinaryException : CoAppException {
        public CoAppBinaryException(string message, params object[] args ): base(message.format(args)) {
        }
    }

    public class Binary {
        private static readonly Dictionary<string, Task<Binary>> LoadingTasks = new Dictionary<string, Task<Binary>>();
        private static readonly Dictionary<string, Binary> LoadedFiles = new Dictionary<string, Binary>();

        public static bool IsAnythingStillLoading { get { return LoadingTasks.Any(); } }
        public static IEnumerable<Binary> Files { get { return LoadedFiles.Values.ToArray(); }}

        public static void UnloadAndResetAll() {
            LoadedFiles.Clear();
            LoadingTasks.Clear();
        }

        public static Task<Binary> Load(string filename, BinaryLoadOptions loadOptions = BinaryLoadOptions.DelayLoad) {
            filename = filename.GetFullPath();

            lock(typeof(Binary)) {
                if( LoadingTasks.ContainsKey(filename)) {
                    return LoadingTasks[filename];
                }

                if( LoadedFiles.ContainsKey(filename)) {
                    return LoadedFiles[filename].AsResultTask();
                }

                var result = Task<Binary>.Factory.StartNew(() => new Binary(filename, loadOptions));
                LoadingTasks.Add(filename, result );
                return result;
            }
        }

        public static void StripSignatures(string filename) {
            filename = filename.GetFullPath();
            filename.TryHardToMakeFileWriteable();

            if (!File.Exists(filename)) {
                throw new FileNotFoundException("Can't find file [{0}]".format(filename), filename);
            }

            using (var f = File.Open(filename, FileMode.Open)) {
                uint certCount = 0;
                var rc = ImageHlp.ImageEnumerateCertificates(f.SafeFileHandle, CertSectionType.Any, out certCount, IntPtr.Zero, 0);
                if (!rc) {
                    // no certificates. that's ok, we're done then.
                    return;
                }

                var errCount = 0;
                for (uint certIndex = 0; certIndex < certCount; certIndex++) {
                    if (!ImageHlp.ImageRemoveCertificate(f.SafeFileHandle, certIndex)) {
                        errCount++;
                    }
                }

                if (errCount != 0) {
                    throw new CoAppException("Had errors removing {0} certificates from file {1}".format(errCount, filename));
                }
            }
        }

        public static void SignFile(string filename, CertificateReference certificate) {
            SignFile(filename, certificate.Certificate);
        }

        public static void SignFile(string filename, X509Certificate2 certificate) {
            filename = filename.GetFullPath();
            if (!File.Exists(filename)) {
                throw new FileNotFoundException("Can't find file", filename);
            }

            filename.TryHardToMakeFileWriteable();

            var urls = new[] {
                "http://timestamp.verisign.com/scripts/timstamp.dll", "http://timestamp.comodoca.com/authenticode", "http://www.startssl.com/timestamp","http://timestamp.globalsign.com/scripts/timstamp.dll", "http://time.certum.pl/"
            };

            var signedOk = false;
            // try up to three times each url if we get a timestamp error
            for (var i = 0; i < urls.Length * 3; i++) {
                try {
                    SignFileImpl(filename, certificate, urls[i % urls.Length]);
                    signedOk = true;
                    break; // whee it worked!
                } catch (FailedTimestampException) {
                    continue;
                }
            }

            if (!signedOk) {
                // we went thru each one 3 times, and it never signed?
                throw new FailedTimestampException(filename, "All of them!");
            }
        }

        private static void SignFileImpl(string filename, X509Certificate2 certificate, string timeStampUrl) {
            // Variables
            //
            var digitalSignInfo = default(DigitalSignInfo);
            // var signContext = default(DigitalSignContext);

            var pSignContext = IntPtr.Zero;

            // Prepare signing info: exe and cert
            //
            digitalSignInfo = new DigitalSignInfo();
            digitalSignInfo.dwSize = Marshal.SizeOf(digitalSignInfo);
            digitalSignInfo.dwSubjectChoice = DigitalSignSubjectChoice.File;
            digitalSignInfo.pwszFileName = filename;
            digitalSignInfo.dwSigningCertChoice = DigitalSigningCertificateChoice.Certificate;
            digitalSignInfo.pSigningCertContext = certificate.Handle;
            digitalSignInfo.pwszTimestampURL = timeStampUrl; // it's sometimes dying when we give it a timestamp url....

            digitalSignInfo.dwAdditionalCertChoice = DigitalSignAdditionalCertificateChoice.AddChainNoRoot;
            digitalSignInfo.pSignExtInfo = IntPtr.Zero;

            var digitalSignExtendedInfo = new DigitalSignExtendedInfo("description", "http://moerinfo");
            var ptr = Marshal.AllocCoTaskMem(Marshal.SizeOf(digitalSignExtendedInfo));
            Marshal.StructureToPtr(digitalSignExtendedInfo, ptr, false);
            // digitalSignInfo.pSignExtInfo = ptr;


            // Sign exe
            //
            if ((!CryptUi.CryptUIWizDigitalSign(DigitalSignFlags.NoUI, IntPtr.Zero, null, ref digitalSignInfo, ref pSignContext))) {
                var rc = (uint)Marshal.GetLastWin32Error();
                if (rc == 0x8007000d) {
                    // this is caused when the timestamp server fails; which seems intermittent for any timestamp service.
                    throw new FailedTimestampException(filename, timeStampUrl);
                }
                throw new DigitalSignFailure(filename, rc);
            }

            // Free blob
            //
            if ((!CryptUi.CryptUIWizFreeDigitalSignContext(pSignContext))) {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CryptUIWizFreeDigitalSignContext");
            }

            // Free additional Info
            Marshal.FreeCoTaskMem(ptr);
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
                throw new CoAppException("Unable to create KeyContainer");
            }
            if (!Mscoree.StrongNameSignatureGeneration(filename, wszKeyContainer, IntPtr.Zero, 0, 0, 0)) {
                throw new CoAppException("Unable Strong name assembly '{0}'.".format(filename));
            }
            Mscoree.StrongNameKeyDelete(wszKeyContainer);
        }

        // ====================================================================================================================
        private bool _modified;
        private bool _modifiedResources;
        private bool _modifiedManaged;
        private bool _modifiedSignature;

        public string Filename { get; set; }
        private string WorkingCopy { get; set; }

        public bool Modified {
            get { return _modified || _modifiedResources || _modifiedSignature || _modifiedManaged || (Manifest.Value != null && Manifest.Value.Modified); }
            private set { _modified = value; }
        }

        private bool Unloaded { get; set; }
        
        private readonly TaskList _tasks = new TaskList();
        private readonly BinaryLoadOptions _loadOptions;

        public readonly Prerequisite<bool> IsPEFile;
        public readonly Prerequisite<string> MD5;
        public readonly Prerequisite<FileVersionInfo> VersionInfo;

        public readonly Prerequisite<bool> Is64BitPE;
        public readonly Prerequisite<bool> Is32BitPE;
        public readonly Prerequisite<bool> IsManaged;
        public readonly Prerequisite<bool> IsNative;

        public readonly Prerequisite<bool> Is32Bit;
        public readonly Prerequisite<bool> Is64Bit;
        public readonly Prerequisite<bool> IsAnyCpu;
        public readonly Prerequisite<bool> IsConsoleApp;
        public readonly Prerequisite<ExecutableInfo> ExecutableInformation;

        public readonly Prerequisite<bool> ILOnly;

        public readonly Prerequisite<ResourceInfo> NativeResources;
        public readonly Prerequisite<NativeManifest> Manifest;

        public readonly Prerequisite<bool> IsSigned;
        public readonly Prerequisite<bool> IsValidSigned;

        public readonly Prerequisite<bool> IsStrongNamed;
        public readonly Prerequisite<bool> IsDelaySigned;

        private Assembly _mutableAssembly;
        private ImageCoffHeader CoffHeader;
        private ImageCor20Header CorHeader;
        private ImageOptionalHeaderNt NtHeader;
        private ImageSectionHeader[] SectionHeaders;
        private ImageDataDirectory _baseRelocationTable;
        private ImageDataDirectory _boundImport;
        private ImageDataDirectory _certificateTable;
        private ImageDataDirectory _copyright;
        private ImageDataDirectory _debug;
        private ImageDataDirectory _delayImportDescriptor;
        private ImageDataDirectory _exceptionTable;
        private ImageDataDirectory _exportTable;
        private ImageDataDirectory _globalPtr;
        private ImageDataDirectory _iat;
        private ImageDataDirectory _importTable;
        private ImageDataDirectory _loadConfigTable;
        private ImageDataDirectory _reserved;
        private ImageDataDirectory _resourceTable;
        private ImageDataDirectory _runtimeHeader;
        private ImageDataDirectory _tlsTable;

        /// <summary>
        /// Synchronously loads the binary
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="loadOptions"></param>
        private Binary(string filename, BinaryLoadOptions loadOptions ) {
            _loadOptions = loadOptions;
            Filename = filename;
            IsPEFile = new Prerequisite<bool>(LoadPEInfo);

            Is64Bit = Is64BitPE = new Prerequisite<bool>(LoadPEInfo);
            Is32BitPE = new Prerequisite<bool>(LoadPEInfo);
            IsManaged = new Prerequisite<bool>(LoadPEInfo);
            IsNative = new Prerequisite<bool>(LoadPEInfo);

            Is32Bit = new Prerequisite<bool>(LoadPEInfo);
            IsAnyCpu = new Prerequisite<bool>(LoadPEInfo, () => IsPEFile && (Is32BitPE && IsManaged && ((CorHeader.Flags & 0x0002) == 0)) );
            IsConsoleApp = new Prerequisite<bool>(LoadPEInfo, () => IsPEFile && (NtHeader.SubSystem & 1) == 1);

            ExecutableInformation = new Prerequisite<ExecutableInfo>(LoadPEInfo, () => {
                var result = IsManaged ? ExecutableInfo.managed : ExecutableInfo.native;
                if (IsAnyCpu) {
                    result |= ExecutableInfo.any;
                } else {
                    switch (CoffHeader.Machine) {
                        case 0x01c0:
                            result |= ExecutableInfo.arm;
                            break;
                        case 0x014c:
                            result |= ExecutableInfo.x86;
                            break;
                        case 0x0200:
                            result |= ExecutableInfo.ia64;
                            break;
                        case 0x8664:
                            result |= ExecutableInfo.x64;
                            break;
                        default:
                            throw new CoAppException("Unrecognized Executable Machine Type.");
                    }
                }
                return result;
            });

            VersionInfo = new Prerequisite<FileVersionInfo>(LoadVersionInfo);

            MD5 = new Prerequisite<string>(LoadMD5);

            ILOnly = new Prerequisite<bool>(LoadManagedData);

            NativeResources = new Prerequisite<ResourceInfo>(LoadResourceData);
            Manifest = new Prerequisite<NativeManifest>(LoadManifestData);

            IsSigned = new Prerequisite<bool>(LoadSignature);
            IsValidSigned = new Prerequisite<bool>(LoadSignature);

            IsStrongNamed = new Prerequisite<bool>(LoadManagedData);
            IsDelaySigned = new Prerequisite<bool>(LoadManagedData);


            LoadData();
        }

        private void LoadData() {
            // first, copy original into temporary file 
            WorkingCopy = Path.GetFileName(Filename).GenerateTemporaryFilename();

            File.Copy(Filename, WorkingCopy);

            if (_loadOptions.HasFlag(BinaryLoadOptions.PEInfo)) {
                LoadPEInfo();
            }
            if (_loadOptions.HasFlag(BinaryLoadOptions.VersionInfo)) {
                LoadVersionInfo();
            }
            if (_loadOptions.HasFlag(BinaryLoadOptions.DependencyData)) {
                LoadDependencyInfo();
            }
            if (_loadOptions.HasFlag(BinaryLoadOptions.Managed)) {
                LoadManagedData();
            }
            if (_loadOptions.HasFlag(BinaryLoadOptions.Resources)) {
                LoadResourceData();
            }
            if (_loadOptions.HasFlag(BinaryLoadOptions.Manifest)) {
                LoadManifestData();
            }
            if (_loadOptions.HasFlag(BinaryLoadOptions.ValidateSignature)) {
                LoadSignature();
            }

            // block on any requested loading to complete
            _tasks.WaitAll();

            // toss this into the loaded bucket.
            lock( typeof(Binary)) {
                LoadedFiles.Add(Filename, this);

                if( LoadingTasks.ContainsKey(Filename)) {
                    LoadingTasks.Remove(Filename);
                }
            }
        }


        private Task _loadingPeInfo;
        private Task LoadPEInfo() {
            return _loadingPeInfo ?? (_loadingPeInfo = _tasks.Start(() => {
                // load PE data from working file

                using (var reader = new BinaryReader(File.Open(WorkingCopy, FileMode.Open, FileAccess.Read, FileShare.Read))) {
                    // Skip DOS Header and seek to PE signature
                    if (reader.ReadUInt16() != 0x5A4D) {
                        IsPEFile.Value = false;
                        Is64BitPE.Value = false;
                        Is32BitPE.Value = false;
                        IsManaged.Value = false;
                        IsNative.Value = false;

                        Is32Bit.Value = false;
                        return;
                    }

                    reader.ReadBytes(58);
                    reader.BaseStream.Position = reader.ReadUInt32();

                    // Read "PE\0\0" signature
                    if (reader.ReadUInt32() != 0x00004550) {
                        IsPEFile.Value = false;
                        Is64BitPE.Value = false;
                        Is32BitPE.Value = false;
                        IsManaged.Value = false;
                        IsNative.Value = false;
                        Is32Bit.Value = false;
                        return;
                    }

                    // Read COFF header
                    CoffHeader = new ImageCoffHeader {
                        Machine = reader.ReadUInt16(),
                        NumberOfSections = reader.ReadUInt16(),
                        TimeDateStamp = reader.ReadUInt32(),
                        SymbolTablePointer = reader.ReadUInt32(),
                        NumberOfSymbols = reader.ReadUInt32(),
                        OptionalHeaderSize = reader.ReadUInt16(),
                        Characteristics = reader.ReadUInt16()
                    };

                    // Compute data sections offset
                    var dataSectionsOffset = reader.BaseStream.Position + CoffHeader.OptionalHeaderSize;

                    // Read NT-specific fields
                    NtHeader = new ImageOptionalHeaderNt();

                    NtHeader.Magic = reader.ReadUInt16();
                    NtHeader.MajorLinkerVersion = reader.ReadByte();
                    NtHeader.MinorLinkerVersion = reader.ReadByte();
                    NtHeader.SizeOfCode = reader.ReadUInt32();
                    NtHeader.SizeOfInitializedData = reader.ReadUInt32();
                    NtHeader.SizeOfUninitializedData = reader.ReadUInt32();
                    NtHeader.AddressOfEntryPoint = reader.ReadUInt32();
                    NtHeader.BaseOfCode = reader.ReadUInt32();

                    // identify this as 64bit or 32bit binary
                    Is64BitPE.Value = NtHeader.Magic == 0x20b;
                    Is32BitPE.Value = NtHeader.Magic == 0x10b;
                    IsPEFile.Value = true;

                    if (Is32BitPE) {
                        NtHeader.BaseOfData_32bit = reader.ReadUInt32();
                        NtHeader.ImageBase_32bit = reader.ReadUInt32();
                    }

                    if (Is64BitPE) {
                        NtHeader.ImageBase_64bit = reader.ReadUInt64();
                    }

                    NtHeader.SectionAlignment = reader.ReadUInt32();
                    NtHeader.FileAlignment = reader.ReadUInt32();
                    NtHeader.OsMajor = reader.ReadUInt16();
                    NtHeader.OsMinor = reader.ReadUInt16();
                    NtHeader.UserMajor = reader.ReadUInt16();
                    NtHeader.UserMinor = reader.ReadUInt16();
                    NtHeader.SubSysMajor = reader.ReadUInt16();
                    NtHeader.SubSysMinor = reader.ReadUInt16();
                    NtHeader.Reserved = reader.ReadUInt32();
                    NtHeader.ImageSize = reader.ReadUInt32();
                    NtHeader.HeaderSize = reader.ReadUInt32();
                    NtHeader.FileChecksum = reader.ReadUInt32();
                    NtHeader.SubSystem = reader.ReadUInt16();
                    NtHeader.DllFlags = reader.ReadUInt16();

                    if (Is32BitPE) {
                        NtHeader.StackReserveSize_32bit = reader.ReadUInt32();
                        NtHeader.StackCommitSize_32bit = reader.ReadUInt32();
                        NtHeader.HeapReserveSize_32bit = reader.ReadUInt32();
                        NtHeader.HeapCommitSize_32bit = reader.ReadUInt32();
                    }
                    if (Is64BitPE) {
                        NtHeader.StackReserveSize_64bit = reader.ReadUInt64();
                        NtHeader.StackCommitSize_64bit = reader.ReadUInt64();
                        NtHeader.HeapReserveSize_64bit = reader.ReadUInt64();
                        NtHeader.HeapCommitSize_64bit = reader.ReadUInt64();
                    }
                    NtHeader.LoaderFlags = reader.ReadUInt32();
                    NtHeader.NumberOfDataDirectories = reader.ReadUInt32();
                    if (NtHeader.NumberOfDataDirectories < 16) {
                        IsManaged.Value = false;
                        IsNative.Value = true;
                        Is32Bit.Value = Is32BitPE;
                        return;
                    }

                    // Read data directories
                    _exportTable = ReadDataDirectory(reader);
                    _importTable = ReadDataDirectory(reader);
                    _resourceTable = ReadDataDirectory(reader);
                    _exceptionTable = ReadDataDirectory(reader);
                    _certificateTable = ReadDataDirectory(reader);
                    _baseRelocationTable = ReadDataDirectory(reader);
                    _debug = ReadDataDirectory(reader);
                    _copyright = ReadDataDirectory(reader);
                    _globalPtr = ReadDataDirectory(reader);
                    _tlsTable = ReadDataDirectory(reader);
                    _loadConfigTable = ReadDataDirectory(reader);
                    _boundImport = ReadDataDirectory(reader);
                    _iat = ReadDataDirectory(reader);
                    _delayImportDescriptor = ReadDataDirectory(reader);
                    _runtimeHeader = ReadDataDirectory(reader);
                    _reserved = ReadDataDirectory(reader);

                    if (_runtimeHeader.Size == 0) {
                        IsManaged.Value = false;
                        IsNative.Value = true;
                        Is32Bit.Value = Is32BitPE;
                        return;
                    }

                    // Read data sections
                    reader.BaseStream.Position = dataSectionsOffset;
                    SectionHeaders = new ImageSectionHeader[CoffHeader.NumberOfSections];
                    for (var i = 0; i < SectionHeaders.Length; i++) {
                        reader.ReadBytes(12);
                        SectionHeaders[i].VirtualAddress = reader.ReadUInt32();
                        SectionHeaders[i].SizeOfRawData = reader.ReadUInt32();
                        SectionHeaders[i].PointerToRawData = reader.ReadUInt32();
                        reader.ReadBytes(16);
                    }

                    // Read COR20 Header
                    reader.BaseStream.Position = RvaToVa(_runtimeHeader.Rva);
                    CorHeader = new ImageCor20Header {
                        Size = reader.ReadUInt32(),
                        MajorRuntimeVersion = reader.ReadUInt16(),
                        MinorRuntimeVersion = reader.ReadUInt16(),
                        MetaData = ReadDataDirectory(reader),
                        Flags = reader.ReadUInt32(),
                        EntryPointToken = reader.ReadUInt32(),
                        Resources = ReadDataDirectory(reader),
                        StrongNameSignature = ReadDataDirectory(reader),
                        CodeManagerTable = ReadDataDirectory(reader),
                        VTableFixups = ReadDataDirectory(reader),
                        ExportAddressTableJumps = ReadDataDirectory(reader)
                    };

                    // we got a CorHeader -- we must be managed.
                    IsManaged.Value = true;
                    IsNative.Value = false;
                    Is32Bit.Value = (CorHeader.Flags & 0x0002) != 0;
                }
            }));
        }

        private static ImageDataDirectory ReadDataDirectory(BinaryReader reader) {
            return new ImageDataDirectory {
                Rva = reader.ReadUInt32(),
                Size = reader.ReadUInt32()
            };
        }

        private long RvaToVa(long rva) {
            for (var i = 0; i < SectionHeaders.Length; i++) {
                if ((SectionHeaders[i].VirtualAddress <= rva) && (SectionHeaders[i].VirtualAddress + SectionHeaders[i].SizeOfRawData > rva)) {
                    return (SectionHeaders[i].PointerToRawData + (rva - SectionHeaders[i].VirtualAddress));
                }
            }
            throw new CoAppException("Invalid RVA address.");
        }

        private Task _loadingVersionInfo;
        private Task LoadVersionInfo() {
            return _loadingVersionInfo ?? (_loadingVersionInfo = _tasks.Start(() => {
                // load version info data from working file
                VersionInfo.Value = FileVersionInfo.GetVersionInfo(WorkingCopy);
            }));
        }

        private Task _loadingDependencyInfo;
        private Task LoadDependencyInfo() {
            return _loadingDependencyInfo ?? (_loadingDependencyInfo = _tasks.Start(() => {
                // TODO: Not Implemented Yet!
                // load * data from working file
                // first, we need to know if this is a PE file.
                
                if( IsPEFile == false ) {
                    return; 
                }

            }));
        }

        private readonly MetadataReaderHost _host = new PeReader.DefaultHost();
        private Task _loadingManagedData;
        private Task LoadManagedData() {
            return _loadingManagedData ?? (_loadingManagedData = _tasks.Start(() => {
                if (_loadOptions.HasFlag(BinaryLoadOptions.NoManaged)) {
                    return;
                }
                // load managed data from working file
                if (!IsManaged) {
                    ILOnly.Value = false;
                    return;
                }
                // copy this to a temporary file, because it locks the file until we're *really* done.
                
                try {
                    var module = _host.LoadUnitFrom(WorkingCopy) as IModule;

                    if (module == null || module is Dummy) {
                        throw new CoAppException("{0} is not a PE file containing a CLR module or assembly.".format(Filename));
                    }

                    ILOnly.Value = module.ILOnly;

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
                } finally {
                    // delete it, or at least trash it & queue it up for next reboot.
                    // temporaryCopy.TryHardToDelete();
                }

                try {
                    if (_mutableAssembly != null) {
                        // we should see if we can get assembly attributes, since sometimes they can be set, but not the native ones.
                        foreach (var a in _mutableAssembly.ContainingAssembly.AssemblyAttributes) {
                            var attributeArgument = (a.Arguments.FirstOrDefault() as MetadataConstant);
                            if (attributeArgument != null) {
                                var attributeValue = attributeArgument.Value.ToString();
                                if (!string.IsNullOrEmpty(attributeValue)) {
                                    switch (a.Type.ToString()) {
                                        case "System.Reflection.AssemblyTitleAttribute":
                                            _fileDescription =  _fileDescription  ?? attributeValue;
                                            break;
                                        case "System.Reflection.AssemblyCompanyAttribute":
                                            _companyName = _companyName ?? attributeValue;
                                            break;
                                        case "System.Reflection.AssemblyProductAttribute":
                                            _productName = _productName  ?? attributeValue;
                                            break;
                                        case "System.Reflection.AssemblyVersionAttribute":
                                            _assemblyVersion = _assemblyVersion == 0L ? (FourPartVersion)attributeValue : _assemblyVersion;
                                            break;
                                        case "System.Reflection.AssemblyFileVersionAttribute":
                                            _fileVersion = _fileVersion == 0L ? (FourPartVersion) attributeValue : _fileVersion;
                                            _productVersion = _productVersion == 0L ? (FourPartVersion)attributeValue : _productVersion;
                                            break;
                                        case "System.Reflection.AssemblyCopyrightAttribute":
                                            _legalCopyright = _legalCopyright  ?? attributeValue;
                                            break;
                                        case "System.Reflection.AssemblyTrademarkAttribute":
                                            _legalTrademarks = _legalTrademarks  ?? attributeValue;
                                            break;
                                        case "System.Reflection.AssemblyDescriptionAttribute":
                                            _comments = _comments  ?? attributeValue;
                                            break;
                                        case "BugTrackerAttribute":
                                            _bugTracker = _bugTracker ?? attributeValue;
                                            break;
                                    }
                                }
                            }
                        }
                    }
                } catch (Exception e) {
                    Console.WriteLine("{0} -- {1}", e.Message, e.StackTrace);
                }

                // if there are dependencies, this will load them.
                if (_loadOptions.HasFlag(BinaryLoadOptions.UnsignedManagedDependencies)) {
                    LoadUnsignedManagedDependencies();
                }
            }));
        }

        private Binary FindAssembly(string assemblyName, string version) {
            // look thru the loaded binaries first
            var binary = LoadedFiles.Values.FirstOrDefault(each => each.IsManaged && each._mutableAssembly.Name.Value == assemblyName && each._mutableAssembly.Version.ToString() == version);
            
            if (binary != null) {
                return binary;
            }

            // it's not already loaded
            // try finding it in the folders where we are working so far...
            foreach (var folder in LoadedFiles.Keys.ToArray().Union(LoadingTasks.Keys.ToArray()).Select(each => Path.GetDirectoryName(each.GetFullPath()).ToLower()).Distinct()) {
                var probe = Path.Combine(folder, assemblyName) + ".dll";
                if (File.Exists(probe)) {
                    // let's load this one...
                    // GS01: We may need to get smarter here... just sayin'
                    var bin = Load(probe, _loadOptions).Result;

                    if (bin.IsManaged && bin._mutableAssembly.Name.Value == assemblyName && bin._mutableAssembly.Version.ToString() == version) {
                        return bin;
                    }
                }
            }
            return null;
        }

        private Task _loadingUnsignedDependencies;
        private Task LoadUnsignedManagedDependencies() {
            return _loadingUnsignedDependencies ?? (_loadingUnsignedDependencies = _tasks.Start(() => {
                // load addtional dependencies
                if (!IsManaged || _loadOptions.HasFlag(BinaryLoadOptions.NoUnsignedManagedDependencies)) {
                    return;
                }

                // load 

                foreach (var ar in _mutableAssembly.AssemblyReferences) {
                    if (!ar.PublicKeyToken.Any()) {
                        // dependent assembly isn't signed. 
                        // look for it.
                        var dep = FindAssembly(ar.Name.Value, ar.Version.ToString());
                        if (dep == null) {
                            Console.WriteLine("WARNING: Unsigned Dependent Assembly {0}-{1} not found.", ar.Name.Value, ar.Version.ToString());
                        }
                    }
                }

            }));
        }

        private Task _loadingResourceData;
        private Task LoadResourceData() {
            return _loadingResourceData ?? (_loadingResourceData = _tasks.Start(() => {
                // load resource data from working file
                if (_loadOptions.HasFlag(BinaryLoadOptions.NoResources)) {
                    NativeResources.Value = null;
                    return;
                }

                var resinfo = new ResourceInfo();
                
                try {
                    resinfo.Load(WorkingCopy);
                } catch( Exception e ) {
                    // even though nothing was loaded, let's keep the blank resources object around.
                    _modifiedResources = false;
                    NativeResources.Value = resinfo;
                    return;
                }

                // lets pull out the relevant resources first.
                var versionKey = resinfo.Resources.Keys.FirstOrDefault(each => each.ResourceType == ResourceTypes.RT_VERSION);
                try {
                    var versionResource = resinfo.Resources[versionKey].First() as VersionResource;
                    var versionStringTable = (versionResource["StringFileInfo"] as StringFileInfo).Strings.Values.First();

                    _comments = _comments ?? TryGetVersionString(versionStringTable, "Comments");
                    _companyName = _companyName ?? TryGetVersionString(versionStringTable, "CompanyName");
                    _productName = _productName ?? TryGetVersionString(versionStringTable, "ProductName");
                    _assemblyVersion = _assemblyVersion == 0L ? (FourPartVersion)TryGetVersionString(versionStringTable, "Assembly Version") : _assemblyVersion;
                    _fileVersion = _fileVersion == 0L ? (FourPartVersion)TryGetVersionString(versionStringTable, "FileVersion") : _fileVersion;
                    _internalName = _internalName ?? TryGetVersionString(versionStringTable, "InternalName");
                    _originalFilename = _originalFilename ?? TryGetVersionString(versionStringTable, "OriginalFilename");
                    _legalCopyright = _legalCopyright ?? TryGetVersionString(versionStringTable, "LegalCopyright");
                    _legalTrademarks = _legalTrademarks ?? TryGetVersionString(versionStringTable, "LegalTrademarks");
                    _fileDescription = _fileDescription ?? TryGetVersionString(versionStringTable, "FileDescription");
                    _bugTracker = _bugTracker ?? TryGetVersionString(versionStringTable, "BugTracker");
                    _productVersion = _productVersion == 0L ? (FourPartVersion)TryGetVersionString(versionStringTable, "ProductVersion"): _productVersion;
                } catch( Exception e ) {
                    // no version resources it seems.
                }
                NativeResources.Value = resinfo;
            }));
        }

        private static string TryGetVersionString(StringTable stringTable, string name) {
            try {
                var result = stringTable[name];
                if (!string.IsNullOrEmpty(result)) {
                    return result.TrimEnd('\0');
                }
            } catch {

            }
            return null;
        }

        private Task _loadingManifestData;
        private Task LoadManifestData() {
            return _loadingManifestData ?? (_loadingManifestData = _tasks.Start(() => {
                // load * data from working file
                if (_loadOptions.HasFlag(BinaryLoadOptions.NoManifest)) {
                    Manifest.Value = null;
                    return;
                }

                if( NativeResources.Value == null ) {
                    // create a default manifest.
                    Manifest.Value = IsPEFile ? new NativeManifest(null) : null;
                    return;
                }
                var manifests = NativeResources.Value.Resources.Keys.Where(each => each.ResourceType == ResourceTypes.RT_MANIFEST);
                var manifestResources = manifests.Select(each => NativeResources.Value.Resources[each].FirstOrDefault() as ManifestResource);
                Manifest.Value = manifestResources.Any() ? new NativeManifest(manifestResources.FirstOrDefault().ManifestText) : new NativeManifest(null);
            }));
        }

        private Task _loadingMD5;
        private Task LoadMD5() {
            return _loadingMD5 ?? (_loadingMD5 = _tasks.Start(() => {
                // generate MD5 from working file
                MD5.Value = WorkingCopy.GetFileMD5();
            }));
        }

        private Task _loadingSignature;
        private Task LoadSignature() {
            return _loadingSignature ?? (_loadingSignature = _tasks.Start(() => {
                // figure out if it's signed.
                IsValidSigned.Value = CoApp.Toolkit.Crypto.Verifier.HasValidSignature(WorkingCopy);
            }));
        }


        public Task<Binary> Revert() {
            if( !Unloaded ) {
                Unload();
            }

            lock (typeof(Binary)) {
                var result = Task<Binary>.Factory.StartNew(() => {
                    LoadData();
                    return this;
                });

                LoadingTasks.Add(Filename, result);
                return result;
            }
        }

        private Task<Binary> _saving;
        public Task<Binary> Save() {
            lock (this) {
                if (_saving != null) {
                    return _saving;
                }

                if (Unloaded) {
                    throw new CoAppBinaryException("Binary '{0}' has been unloaded", Filename);
                }

                if (!Modified) {
                    return this.AsResultTask();
                }

                _saving = Task<Binary>.Factory.StartNew(() => {
                    Console.WriteLine("Actual Signing Process started for [{0}]/[{1}]", Filename , WorkingCopy);
                    if (!IsManaged) {
                        StripSignatures(WorkingCopy); // this is irrelevant if the binary is managed--we'll be writing out a new one.
                    }

                    if (!_loadOptions.HasFlag(BinaryLoadOptions.NoManaged) && IsManaged && (_modifiedManaged || _modifiedResources)) {
                        WaitForResourceAndManagedLoaders();

                        // handle managed code rewrites
                        // we can only edit the file if it's IL only, mixed mode assemblies can only be strong named, signed and native-resource-edited.
                        // set the strong name key data
                        if (!StrongNameKey.IsNullOrEmpty()) {
                            if (_mutableAssembly == null) {
                                Console.WriteLine("HEY! : {0}", Filename);
                            }

                            _mutableAssembly.PublicKey = StrongNameKey.ToList();

                            // change any assembly attributes we need to change
                            if (_mutableAssembly != null) {

                                if (StrongNameKeyCertificate != null) {
                                    foreach (var ar in _mutableAssembly.AssemblyReferences) {
                                        // rewrite assembly references that need to be updated.
                                        if (!ar.PublicKeyToken.Any()) {

                                            var dep = FindAssembly(ar.Name.Value, ar.Version.ToString());
                                            if (dep == null) {
                                                // can't strong name a file that doesn't have its deps all strong named.
                                                throw new CoAppException("dependent assembly '{0}-{1}' not available for strong naming".format(ar.Name.Value,
                                                    ar.Version.ToString()));
                                            }

                                            if (dep._mutableAssembly.PublicKey.IsNullOrEmpty()) {
                                                if (!_loadOptions.HasFlag(BinaryLoadOptions.NoUnsignedManagedDependencies)) {
                                                    Console.WriteLine(
                                                        "Warning: Non-strong-named dependent reference found: '{0}-{1}' updating with same strong-name-key.",
                                                        ar.Name, ar.Version);
                                                    dep.StrongNameKeyCertificate = StrongNameKeyCertificate;
                                                    dep.SigningCertificate = SigningCertificate;

                                                    dep.AssemblyCopyright = AssemblyCopyright;
                                                    dep.AssemblyCompany = AssemblyCompany;
                                                    dep.AssemblyProduct = AssemblyProduct;

                                                    // wait for the dependency to finish saving.
                                                    dep.Save().Wait();

                                                } else {
                                                    throw new CoAppException("dependent assembly '{0}-{1}' not strong named".format(ar.Name.Value,
                                                        ar.Version.ToString()));
                                                }
                                            }
                                            (ar as Microsoft.Cci.MutableCodeModel.AssemblyReference).PublicKeyToken =
                                                dep._mutableAssembly.PublicKeyToken.ToList();
                                            (ar as Microsoft.Cci.MutableCodeModel.AssemblyReference).PublicKey = dep._mutableAssembly.PublicKey;
                                        }

                                    }
                                }
                            }
                            // we should see if we can get assembly attributes, since sometimes they can be set, but not the native ones.
                            try {
                                foreach (var a in _mutableAssembly.AssemblyAttributes) {
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
                                                attributeArgument.Value = (string)AssemblyVersion;
                                                break;
                                            case "System.Reflection.AssemblyFileVersionAttribute":
                                                attributeArgument.Value = (string)AssemblyFileVersion;
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
                        WorkingCopy.TryHardToMakeFileWriteable();
                        using (var peStream = File.Create(WorkingCopy)) {
                            PeWriter.WritePeToStream(_mutableAssembly, _host, peStream);
                        }

                    }

                    if (!_loadOptions.HasFlag(BinaryLoadOptions.NoManifest) && Manifest.Value != null && Manifest.Value.Modified) {
                        // rewrite Manifests
                        // GS01: We only support one manifest right now. 
                        // so we're gonna remove the extra ones.
                        // figure out the bigger case later. 
                        var manifestKeys = NativeResources.Value.Resources.Keys.Where(each => each.ResourceType == ResourceTypes.RT_MANIFEST).ToArray();
                        foreach (var k in manifestKeys) {
                            var v = NativeResources.Value.Resources[k];
                            if (!v.IsNullOrEmpty()) {
                                foreach (var inst in v) {
                                    Resource.Delete(WorkingCopy, inst.Type, inst.Name, inst.Language);
                                }
                            }
                            NativeResources.Value.Resources.Remove(k);
                        }

                        var manifestResource = new ManifestResource {ManifestText = Manifest.Value.ToString(), Language = 1033};
                        // GS01: I'm hardcoding this for now. We're probably gonna have to be way smarter about this.
                        NativeResources.Value.Resources.Add(new ResourceId(ResourceTypes.RT_MANIFEST), new List<Resource> {
                            manifestResource
                        });

                        manifestResource.SaveTo(WorkingCopy);
                    }

                    if (!_loadOptions.HasFlag(BinaryLoadOptions.NoResources) && _modifiedResources) {
                        // rewrite Resources
                        VersionResource versionResource;
                        StringTable versionStringTable;

                        var versionKey = NativeResources.Value.Resources.Keys.FirstOrDefault(each => each.ResourceType == ResourceTypes.RT_VERSION);
                        if (versionKey != null) {
                            versionResource = NativeResources.Value.Resources[versionKey].First() as VersionResource;
                            versionStringTable = (versionResource["StringFileInfo"] as StringFileInfo).Strings.Values.First();
                        } else {
                            versionResource = new VersionResource();
                            NativeResources.Value.Resources.Add(new ResourceId(ResourceTypes.RT_VERSION), new List<Resource> {
                                versionResource
                            });

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

                        versionResource.SaveTo(WorkingCopy);
                    }

                    if (!_loadOptions.HasFlag(BinaryLoadOptions.NoSignature) && _modifiedSignature && _signingCertificate != null) {
                        // Strongname & Sign the package

                        // strong name the binary (if we're doing managed stuff).
                        if (!_loadOptions.HasFlag(BinaryLoadOptions.NoManaged) && IsManaged && StrongNameKeyCertificate != null &&
                            (StrongNameKeyCertificate.Certificate.PrivateKey is RSACryptoServiceProvider)) {
                            ApplyStrongName(WorkingCopy, StrongNameKeyCertificate);
                        }

                        // sign the binary
                        SignFile(WorkingCopy, SigningCertificate.Certificate);
                    }

                    if (_loadingMD5 != null) {
                        _loadingMD5 = null;
                    }

                    LoadMD5().Wait();

                    Console.WriteLine("Replacing original File [{0}]", Filename);
                    Filename.TryHardToDelete();
                    File.Copy(WorkingCopy, Filename);

                    _modified = false;
                    _modifiedResources = false;
                    _modifiedManaged = false;
                    _modifiedSignature = false;

                    Console.WriteLine("Completed Signing Process started for [{0}]/[{1}]", Filename, WorkingCopy);
                    return this;
                });
                _saving.ContinueWith((a) => { _saving = null; }, TaskContinuationOptions.AttachedToParent);
            }
            return _saving;
        }

        public void Unload() {
            lock( typeof(Binary)) {
                Unloaded = true;
                LoadedFiles.Remove(Filename);
                WorkingCopy.TryHardToDelete();
                _tasks.Clear();

                _loadingMD5 = null;
                _loadingManagedData = null;
                _loadingPeInfo = null;
                _loadingManifestData = null;
                _loadingResourceData = null;
                _loadingVersionInfo = null;
                
                _modified = false;
                _modifiedResources = false;
                _modifiedManaged = false;
                _modifiedSignature = false;
            }
        }

        private string _comments;       //AssemblyDescription
        private string _companyName;    //AssemblyCompany
        private string _productName;    //AssemblyProduct
        private FourPartVersion _assemblyVersion; //AssemblyVersion
        private FourPartVersion _fileVersion;    //AssemblyFileVersion, 
        private FourPartVersion _productVersion;    //<AssemblyFileVersion>
        private string _internalName;   //<filename>
        private string _originalFilename;   //<filename>
        private string _legalCopyright; //AssemblyCopyright
        private string _legalTrademarks;//AssemblyTrademark
        private string _bugTracker;     //AssemblyBugtracker
        private string _fileDescription; //AssemblyTitle

        private void WaitForResourceAndManagedLoaders() {
            if( _loadingResourceData != null ) {
                LoadResourceData();
            } 
            if(IsManaged && _loadingManagedData == null) {
                LoadResourceData();   
            }
            
            if (_loadingResourceData != null && !_loadingResourceData.IsCompleted) {
                _loadingResourceData.Wait();
            }

            if (_loadingManagedData != null && !_loadingManagedData.IsCompleted) {
                _loadingManagedData.Wait();
            }
        }

        public string AssemblyTitle {
            get { return FileDescription; }
            set { FileDescription = value; _modifiedResources = true; }
        }
        public string FileDescription {
            get {
                if( _fileDescription == null ) {
                    WaitForResourceAndManagedLoaders();
                }
                return _fileDescription;
            }
            set { _modifiedResources = true; _fileDescription = value; }
        }
        public string BugTracker {
            get {
                if (_bugTracker == null) {
                    WaitForResourceAndManagedLoaders();
                }
                return _bugTracker;
            }
            set { _modifiedResources = true; _bugTracker = value; }
        }
        public string AssemblyTrademark {
            get { return LegalTrademarks; }
            set { _modifiedResources = true; LegalTrademarks = value; }
        }
        public string LegalTrademarks {
            get {
                if (_legalTrademarks == null) {
                    WaitForResourceAndManagedLoaders();
                }
                return _legalTrademarks;
            }
            set { _modifiedResources = true; _legalTrademarks = value; }
        }
        public string AssemblyCopyright {
            get { return LegalCopyright; }
            set { _modifiedResources = true; LegalCopyright = value; }
        }
        public string LegalCopyright {
            get {
                if (_legalCopyright == null) {
                    WaitForResourceAndManagedLoaders();
                }
                return _legalCopyright;
            }
            set { _modifiedResources = true; _legalCopyright = value; }
        }
        public string InternalName {
            get {
                if (_internalName == null) {
                    WaitForResourceAndManagedLoaders();
                }
                return _internalName;
            }
            set { _modifiedResources = true; _internalName = value; }
        }
        public string OriginalFilename {
            get {
                if (_originalFilename == null) {
                    WaitForResourceAndManagedLoaders();
                }
                return _originalFilename;
            }
            set { _modifiedResources = true; _originalFilename = value; }
        }
        public FourPartVersion ProductVersion {
            get {
                if (_productVersion == 0L) {
                    WaitForResourceAndManagedLoaders();
                }
                return _productVersion;
            }
            set { _modifiedResources = true; _productVersion = value; }
        }
        public FourPartVersion AssemblyFileVersion {
            get { return FileVersion; }
            set { _modifiedResources = true; FileVersion = value; }
        }
        public FourPartVersion FileVersion {
            get {
                if (_fileVersion == 0L) {
                    WaitForResourceAndManagedLoaders();
                }
                return _fileVersion;
            }
            set { _modifiedResources = true; _fileVersion = value; }
        }
        public FourPartVersion AssemblyVersion {
            get {
                if (_assemblyVersion== 0L) {
                    WaitForResourceAndManagedLoaders();
                }
                return _assemblyVersion;
            }
            set { _modifiedResources = true; _assemblyVersion = value; }
        }
        public string AssemblyProduct {
            get { return ProductName; }
            set { _modifiedResources = true; ProductName = value; }
        }
        public string ProductName {
            get {
                if (_productName == null) {
                    WaitForResourceAndManagedLoaders();
                }
                return _productName;
            }
            set { _modifiedResources = true; _productName = value; }
        }
        public string AssemblyDescription {
            get { return Comments; }
            set { _modifiedResources = true; Comments = value; }
        }
        public string Comments {
            get {
                if (_comments == null) {
                    WaitForResourceAndManagedLoaders();
                }
                return _comments;
            }
            set { _modifiedResources = true; _comments = value; }
        }
        public string AssemblyCompany {
            get { return CompanyName; }
            set { _modifiedResources = true; CompanyName = value; }
        }
        public string CompanyName {
            get {
                if (_companyName == null) {
                    WaitForResourceAndManagedLoaders();
                }
                return _companyName;
            }
            set { _modifiedResources = true; _companyName = value; }
        }

        private CertificateReference _signingCertificate;
        public CertificateReference SigningCertificate {
            get { return _signingCertificate; }
            set {
                _signingCertificate = value;
                _modifiedSignature = true;
            }
        }

        private CertificateReference _strongNameKeyCertificate;
        public CertificateReference StrongNameKeyCertificate {
            get {
                return _strongNameKeyCertificate;
            }
            set {
                _strongNameKeyCertificate = value;
                _modifiedManaged = true;
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
                WaitForResourceAndManagedLoaders();
                return _strongNameKey;
            }
            set {
                WaitForResourceAndManagedLoaders();
                _strongNameKey = value;
                _modifiedManaged = true;
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
    }
}
