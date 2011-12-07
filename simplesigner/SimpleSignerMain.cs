namespace coapp_simplesigner {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using CoApp.Developer.Toolkit.Publishing;
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
    --remember                  store certificate details in registry (encrypted)

    --sign-only                 just sign the binary, no strong-naming
    --no-metadata               don't try to adjust any metadata

Metadata Options:
-----------------

    --company=<Name>            set the Company Name to <Name>
    --description=<value>       set the File Description to <value>
    --internal-name=<value>     set the Internal Name of the binary to <value>
    --copyright=<value>         set the Copyright to <value>
    --original-filename=<value> set the Original Filename to <value>
    --product-name=<value>      set the Product Name to <value>

    --product-version=<value>   set the Product Version to <value>
    --file-version=<value>      set the File Version to <value>
";

        private bool _resign;
        private bool _remember;
        private bool _sign = true;
        private bool _strongname = true;
        private bool _verbose;
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

                    case "resign" :
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
                Console.WriteLine("Loaded certificate with private key {0}", _certificate.Location);
            }


            if (_remember) {
                Verbose("Storing certificate details in the registry.");
                _certificate.RememberPassword();
                CertificateReference.Default = _certificate;
            }

            if (parameters.Count() < 1) {
                return Fail("Missing files to sign/name. \r\n\r\n    Use --help for command line help.");
            }

            var tasks = new List<Task>();

            try {
                var allFiles = parameters.FindFilesSmarter().ToArray();

                
                List<PeBinary> binaries = new List<PeBinary>();

                foreach (var f in allFiles) {
                    Console.WriteLine("Loading File: {0}", f);
                    var filename = f;

                    tasks.Add(Task.Factory.StartNew(() => {

                        // var result = "";
                        if (CoApp.Toolkit.Crypto.Verifier.HasValidSignature(filename) && !_resign) {
                            using (new ConsoleColors(ConsoleColor.Yellow, ConsoleColor.Black)) {
                                Console.WriteLine("[{0}] already has a valid signature; skipping.", filename);
                            }
                            return;
                        }

                        try {
                            var info = PEInfo.Scan(filename);

                            if (info.IsPEBinary) {
                                var peBinary = PeBinary.Load(filename);
                                lock( binaries) {
                                    binaries.Add(peBinary);
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
                                if (_strongname) {
                                    peBinary.StrongNameKeyCertificate = _certificate;
                                }
                                if (_sign) {
                                    peBinary.SigningCertificate = _certificate;
                                }


                                peBinary.Save();

                                using (new ConsoleColors(ConsoleColor.Green, ConsoleColor.Black)) {
                                    Console.WriteLine("Success {0}", filename);
                                }
                            } else {
                                PeBinary.SignFile(filename, _certificate);
                            }
                        } catch (Exception e) {
                            using (new ConsoleColors(ConsoleColor.Red, ConsoleColor.Black)) {
                                Console.WriteLine("Failed {0} : {1}", filename, e.Message);
                                Console.WriteLine(e.StackTrace);
                            }
                        }
                    }));
                }

                Console.WriteLine("Tasks: {0}", tasks.ToArray().Count());

                Task.Factory.ContinueWhenAll(
                    tasks.ToArray(), (antecedent) => {
                        Console.WriteLine("Loading Done.");
                    }).Wait();

                /*
                tasks.Clear();

                foreach (var binary in binaries) {
                    var peBinary = binary;

                    tasks.Add(Task.Factory.StartNew(() => {
                        peBinary.Save(); 

                    }));

                    Task.Factory.ContinueWhenAll(
                    tasks.ToArray(), (antecedent) => {
                        Console.WriteLine("Loading Done.");
                    }).Wait();

                }
                 * */
            }
            catch (Exception e) {
                return Fail(e.Message);
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
