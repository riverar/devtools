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
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using CoApp.Developer.Toolkit.Exceptions;
    using CoApp.Developer.Toolkit.Publishing;
    using CoApp.Toolkit.Exceptions;
    using CoApp.Toolkit.Extensions;
    using CoApp.Toolkit.Win32;

    internal class SimpleSignerMain {
        /// <summary>
        ///   Command line help information
        /// </summary>
        private const string HelpMessage =
            @"
Usage:
-------

SimpleSigner [options] <file(s)-to-sign...>

Options:
--------
    --help                      this help
    --nologo                    don't display the logo
    --load-config=<file>        loads configuration from <file>
    --verbose                   prints verbose messages

    --certificate-path=<c.pfx>  path to load signing certificate (w/pvt key)
    --password=<pwd>            password for certificate file
    --remember                  store certificate details in registry 
                                (encrypted)

    --sign-only                 just sign the binary, no strong-naming
    --no-metadata               don't try to adjust any metadata
    --force                     re-sign the binaries, even if they have 
                                a signature

    --verify                    show certificate & verification info for 
                                binaries (don't sign)

    --auto                      automatically handle unsigned dependent 
                                assemblies


Metadata Options:
-----------------

    --company=<Name>            set the Company Name to <Name>*
    --description=<value>       set the File Description to <value>
    --internal-name=<value>     set the Internal Name of the binary to <value>
    --copyright=<value>         set the Copyright to <value>
    --original-filename=<value> set the Original Filename to <value>
    --product-name=<value>      set the Product Name to <value>

    --product-version=<value>   set the Product Version to <value>
    --file-version=<value>      set the File Version to <value>

    * use value AUTO to have it pull company name from the certificate
";

        private bool _resign;
        private bool _remember;
        private bool _sign = true;
        private bool _strongname = true;
        private bool _verbose;
        private bool _verify;
        private bool _auto;

        private string _signingCertPath = string.Empty;
        private string _signingCertPassword;
        private CertificateReference _certificate;
        private string _fileVersion;
        private string _company;
        private string _description;
        private string _internalName;
        private string _copyright;
        private string _productName;
        private string _originalFilename;
        private string _productVersion;

        private static int Main(string[] args) {
            return new SimpleSignerMain().Startup(args);
        }

        private int Startup(IEnumerable<string> args) {
            var options = args.Switches();
            var parameters = args.Parameters();

            foreach (var arg in options.Keys) {
                var argumentParameters = options[arg];

                switch (arg) {
                        /* global switches */
                    case "load-config":
                        // all ready done, but don't get too picky.
                        break;

                    case "nologo":
                        this.Assembly().SetLogo(string.Empty);
                        break;

                    case "help":
                        return Help();

                    case "certificate-path":
                        _signingCertPath = Path.GetFullPath(argumentParameters.Last());
                        break;

                    case "password":
                        _signingCertPassword = argumentParameters.Last();
                        break;

                    case "remember":
                        _remember = true;
                        break;

                    case "auto":
                        _auto = true;
                        break;

                    case "sign-only":
                        _strongname = false;
                        break;

                    case "name-only":
                        _sign = false;
                        break;

                    case "verbose":
                        _verbose = true;
                        break;

                    case "company":
                        _company = argumentParameters.Last();
                        break;

                    case "description":
                        _description = argumentParameters.Last();
                        break;

                    case "internal-name":
                        _internalName = argumentParameters.Last();
                        break;

                    case "copyright":
                        _copyright = argumentParameters.Last();
                        break;

                    case "original-filename":
                        _originalFilename = argumentParameters.Last();
                        break;

                    case "product-name" :
                        _productName = argumentParameters.Last();
                        break;

                    case "verify" :
                        _verify = true;
                        break;

                    case "resign" :
                    case "force":
                        _resign = true;
                        break;

                    case "product-version":
                        _productVersion = argumentParameters.Last();
                        if (_productVersion.VersionStringToUInt64() == 0 || _productVersion.VersionStringToUInt64().UInt64VersiontoString() != _productVersion) {
                            return Fail("--product-version must be in the form ##.##.##.##");
                        }

                        break;

                    case "file-version":
                        _fileVersion  = argumentParameters.Last();
                        if (_fileVersion.VersionStringToUInt64() == 0 || _fileVersion.VersionStringToUInt64().UInt64VersiontoString() != _fileVersion) {
                            return Fail("--file-version must be in the form ##.##.##.##");
                        }

                        break;

                    default:
                        return Fail("Unknown parameter [--{0}]", arg);
                }
            }

            Logo();

            if (parameters.Count() < 1) {
                return Fail("Missing files to sign/name. \r\n\r\n    Use --help for command line help.");
            }

            if( _verify ) {
                return Verify(parameters);
            }

            if( string.IsNullOrEmpty(_signingCertPath) ) {
                _certificate = CertificateReference.Default;
                if( _certificate == null ) {
                    return Fail("No default certificate stored in the registry");
                }
            } else if( string.IsNullOrEmpty(_signingCertPassword) ) {
                _certificate = new CertificateReference(_signingCertPath);
            } else {
                _certificate = new CertificateReference(_signingCertPath,_signingCertPassword);  
            }

            using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                Verbose("Loaded certificate with private key {0}", _certificate.Location);
            }

            if (_remember) {
                Verbose("Storing certificate details in the registry.");
                _certificate.RememberPassword();
                CertificateReference.Default = _certificate;
            }

            var tasks = new List<Task>();

            if( _company != null && _company.Equals("auto", StringComparison.CurrentCultureIgnoreCase) ) {
                _company = _certificate.CommonName;
            }


            try {
                var allFiles = parameters.FindFilesSmarter().ToArray();
                
                var binaries = new List<PeBinary>();
                var nonBinaries = new List<PEInfo>();

                var results = new List<FileResult>();


                foreach (var f in allFiles) {
                    Verbose("Inspecting File: {0}", f);
                    var filename = f;

                    // first, load all the binaries
                    // 

                    tasks.Add(Task.Factory.StartNew(() => {
                        if (CoApp.Toolkit.Crypto.Verifier.HasValidSignature(filename) && !_resign) {
                            results.Add( new FileResult {FullPath= filename, AlreadySigned = true, OriginalMD5 = filename.GetFileMD5(), Message = "Already Signed (skipped)", Color = ConsoleColor.Yellow});
                            return;
                        }

                        try {
                            var info = PEInfo.Scan(filename);

                            if (info.IsPEBinary) {
                                var peBinary = PeBinary.Load(filename);
                                lock (binaries) {
                                    if (!binaries.Contains(peBinary)) {
                                        binaries.Add(peBinary);
                                    }

                                    foreach (var depBinary in peBinary.UnsignedDependentBinaries.Where(depBinary => !binaries.Contains(depBinary))) {
                                        binaries.Add(depBinary);
                                    }
                                }
                            } else {
                                if (!nonBinaries.Contains(info)) {
                                    nonBinaries.Add(info);
                                }
                            }

                        } catch (Exception e) {
                            results.Add(new FileResult { FullPath = filename, Message = "Failed to load--{0}".format(e.GetType()), Color = ConsoleColor.Red });
                        }
                    }));
                }
                Task.Factory.ContinueWhenAll(tasks.ToArray(), (antecedent) => Verbose("Completed loading files.")).Wait();
                tasks.Clear();

                // Now, go ahead and modify all the binaries 
                // and sign all the files.

                foreach( var nBin in nonBinaries ) {
                    var nonBinary = nBin;
                    tasks.Add(Task.Factory.StartNew(() => {
                        var filename = nonBinary.Filename;
                        try {
                            PeBinary.SignFile(filename, _certificate);
                            results.Add( new FileResult { FullPath = filename, OriginalMD5 = nonBinary.MD5, NewMD5 = filename.GetFileMD5(), Message = "Success." });

                        } catch (DigitalSignFailure  exc) {
                            if (exc.Win32Code == 0x800b0003) {
                                results.Add(new FileResult { FullPath = filename, OriginalMD5 = nonBinary.MD5, Message = "Unable to sign unrecognized file", Color = ConsoleColor.Red });
                            } else {
                                results.Add(new FileResult {FullPath = filename, OriginalMD5 = nonBinary.MD5, Message = exc.Message, Color = ConsoleColor.Red});
                            }
                        } catch (CoAppException exc) {
                            results.Add(new FileResult { FullPath = filename, OriginalMD5 = nonBinary.MD5, Message = exc.Message, Color = ConsoleColor.Red });
                        }
                        catch (Exception exc) {
                            results.Add(new FileResult { FullPath = filename, OriginalMD5 = nonBinary.MD5, Message = "Unable to sign unrecognized, non-binary file--{0}".format(exc.GetType()), Color = ConsoleColor.Red});
                        }
                    }));
                }
                binaries.Reverse();
                foreach (var bin in binaries) {
                    var peBinary = bin;

                    tasks.Add(Task.Factory.StartNew(() => {
                        var filename = peBinary.Filename;

                        try {
                            if (_strongname) {
                                peBinary.StrongNameKeyCertificate = _certificate;
                            }

                            if (_sign) {
                                peBinary.SigningCertificate = _certificate;
                            }

                            if (_company != null) {
                                peBinary.CompanyName = _company;
                            }
                            if (_description != null) {
                                peBinary.FileDescription = _description;
                            }
                            if (_internalName != null) {
                                peBinary.InternalName = _internalName;
                            }
                            if (_copyright != null) {
                                peBinary.LegalCopyright = _copyright;
                            }
                            if (_originalFilename != null) {
                                peBinary.OriginalFilename = _originalFilename;
                            }
                            if (_productName != null) {
                                peBinary.ProductName = _productName;
                            }
                            if (_productVersion != null) {
                                peBinary.ProductVersion = _productVersion;
                            }
                            if (_fileVersion != null) {
                                peBinary.FileVersion = _fileVersion;
                            }

                            peBinary.Save(_auto);

                            results.Add(new FileResult { FullPath = filename, OriginalMD5 = peBinary.Info.MD5, NewMD5 = filename.GetFileMD5(), Message = "Success"  });

                        } catch (CoAppException exc) {
                            results.Add(new FileResult { FullPath = filename, OriginalMD5 = peBinary.Info.MD5, Message = exc.Message, Color = ConsoleColor.Red });
                        } catch (Exception exc) {
                            results.Add(new FileResult { FullPath = filename, OriginalMD5 = peBinary.Info.MD5, Message = "Unable to sign PE Binary file--{0}".format(exc.GetType()), Color = ConsoleColor.Red });
                        }
                    }));
                }

                if (tasks.Any()) {
                    // wait for all the work to be done.
                    Task.Factory.ContinueWhenAll(tasks.ToArray(), (antecedent) => { Verbose("Completed Signing Files."); }).Wait();
                }
                var output = results.OrderByDescending(each => each.Color).ToArray();

                var outputLines = (from each in output
                    select new {
                        Filename = Path.GetFileName(each.FullPath),
                        Original_MD5 = each.OriginalMD5,
                        New_MD5 = each.NewMD5,
                        Status = each.Message,
                    }).ToTable().ToArray();


                Console.WriteLine(outputLines[0]);
                var footer = outputLines[1];
                Console.WriteLine(footer);
                
                // trim the header/footer
                outputLines = outputLines.Skip(2).Reverse().Skip(1).Reverse().ToArray();

                for (int i = 0; i < outputLines.Length; i++  ) {
                    using (new ConsoleColors(output[i].Color, ConsoleColor.Black)) {
                        Console.WriteLine(outputLines[i]);
                    }
                }

                Console.WriteLine(footer);
            }
            catch (Exception e) {
                return Fail(e.Message);
            }

            return 0;
        }

        public int Verify(IEnumerable<string> parameters) {
            var allFiles = parameters.FindFilesSmarter().ToArray().AsParallel();

            using (new ConsoleColors(ConsoleColor.Green, ConsoleColor.Black)) {
                (from each in allFiles
                    let info = PEInfo.Scan(each)
                    let bin = info.IsPEBinary ?  PeBinary.Load(each) : null
                    orderby info.IsPEBinary 
                    select new {
                        Filename = Path.GetFileName(each),
                        Version = info.IsPEBinary ? info.FileVersion : "",
                        Signed = CoApp.Toolkit.Crypto.Verifier.HasValidSignature(each),
                        MD5 = info.MD5,
                        Company_Name = bin == null ? "" : bin.CompanyName,

                        //Binary =  info.IsPEBinary , 
                        // Managed = is  bin.IsManaged,
                    }).ToArray().OrderBy(each => each.Signed).ToTable().ConsoleOut();
            }
            return 0;
        }

        #region fail/help/logo

        /// <summary>
        ///   Displays a failure message.
        /// </summary>
        /// <param name = "text">
        ///   The text format string.
        /// </param>
        /// <param name = "par">
        ///   The parameters for the formatted string.
        /// </param>
        /// <returns>
        ///   returns 1 (usually passed out as the process end code)
        /// </returns>
        public int Fail(string text, params object[] par) {
            Logo();
            using (new ConsoleColors(ConsoleColor.Red, ConsoleColor.Black)) {
                Console.WriteLine("Error: {0}", text.format(par));
            }

            return 1;
        }

        /// <summary>
        ///   Displays the program help.    
        /// </summary>
        /// <returns>
        ///   returns 0.
        /// </returns>
        private int Help() {
            Logo();
            using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                HelpMessage.Print();
            }

            return 0;
        }

        /// <summary>
        ///   Displays the program logo.
        /// </summary>
        private void Logo() {
            using (new ConsoleColors(ConsoleColor.Cyan, ConsoleColor.Black)) {
                this.Assembly().Logo().Print();
            }

            this.Assembly().SetLogo(string.Empty);
        }

        private void Verbose(string text, params object[] par) {
            if (_verbose) {
                using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                    Console.WriteLine(text.format(par));
                }
            }
        }

        #endregion
    }

    
}
