using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoApp.Developer.Toolkit.Publishing {
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using System.Text.RegularExpressions;
    using CoApp.Toolkit.Configuration;
    using CoApp.Toolkit.Extensions;

    internal static class MatchExtensions {
        internal static string Value(this Match match, string group, string _default = null) {
            return (match.Groups[group].Success ? match.Groups[group].Captures[0].Value : _default??string.Empty).Trim('-',' ');
        }
    }

    /// <summary>
    /// This class loads a certificate, and can persist knowledge of the certificate
    /// (ie, the path and password in encrypted form) but NEVER stores or moves the 
    /// certificate itself.
    /// </summary>
    public class CertificateReference {
        private static readonly Regex _certLocationParser = new Regex(@"^(?<location>..+):(?<name>.+)\\(?<cert>.+)$", RegexOptions.IgnoreCase);
        private readonly X509Certificate2 _certificate;
        private readonly string _location;
        private readonly string _password;
        private static readonly RegistryView _settings = RegistryView.CoAppUser["Certificates"];

        private static CertificateReference _default;
        public static CertificateReference Default { get {
            if (_default == null) {
                var location = _settings["#CurrentCertificate"].EncryptedStringValue;
                if (!IsValidCertificateLocation(location)) {
                    return null;
                }
                _default = new CertificateReference(location);
            }
            return _default;
        }
        set {
            if( value == null ) {
                _settings["#CurrentCertificate"].EncryptedStringValue = null;
            } else if( value != _default) {
                _settings["#CurrentCertificate"].EncryptedStringValue = _default._location;
            }
        }}

        private static X509Certificate2 LoadCertificateFromStore(string location) {
           if (string.IsNullOrEmpty(location)) {
                throw new ArgumentNullException("location");
            }
            
            // check for name:location\path style
            var match = _certLocationParser.Match(location);
            if (match.Success) {
                X509Store store = null;
                try {
                    StoreLocation storeLocation;
                    if (!Enum.TryParse(match.Value("location", ""), true, out storeLocation)) {
                        return null;
                    }
                    var storename = match.Value("name", "");
                    var cert = match.Value("cert", "");

                    store = new X509Store(storename, storeLocation);
                    if( store.Certificates.Count > 0 ) {
                        var certs = new X509Certificate2[store.Certificates.Count];
                        store.Certificates.CopyTo(certs, 0);

                        certs = certs.Where(each => each.HasPrivateKey).ToArray();

                        var matches = certs.Where( each => (!string.IsNullOrEmpty(each.Thumbprint)) && each.Thumbprint.Equals(cert, StringComparison.CurrentCultureIgnoreCase)).ToArray();
                        if( matches.Count() == 1 ) {
                            return matches.FirstOrDefault();
                        }

                        matches = certs.Where( each => (!string.IsNullOrEmpty(each.SubjectName.Name)) && each.SubjectName.Name.Equals(cert, StringComparison.CurrentCultureIgnoreCase)).ToArray();
                        if( matches.Count() == 1 ) {
                            return matches.FirstOrDefault();
                        }
                    }
                } finally {
                    if( store!=null) {
                        store.Close();
                    }
                }
            }

            return null; 
        }

        private static bool IsValidCertificateLocation(string location) {
            if (string.IsNullOrEmpty(location)) {
                return false;
            }
            if (File.Exists(location)) {
                return true;
            }
            // check for name:location\path style
            return LoadCertificateFromStore(location) != null;
        }

        /// <summary>
        /// Opens a certificate from a path or store. Stores are expressed as 
        ///     storeLocation:storeName\thumbprint   
        /// or
        ///     storeLocation:storeName\subjectName   (will throw if not unique)
        /// 
        /// where storeLocation must be either 'currentuser' or 'localmachine'
        /// </summary>
        /// <param name="location"></param>
        /// <param name="password"></param>
        public CertificateReference(string location, string password) {
            if (string.IsNullOrEmpty(location)) {
                throw new ArgumentNullException("location");
            }

            if (File.Exists(location)) {
                _location = location;
                _password = password;
                if( string.IsNullOrEmpty(_password) ) {
                    _certificate = new X509Certificate2(_location );
                } else {
                    _certificate = new X509Certificate2(_location , _password, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable );
                }
                if( !_certificate.HasPrivateKey ) {
                    throw new Exception("Certificate '{0}' does not have private key".format(_location));
                }
            }
            _certificate = new X509Certificate2(location, password);
        }

        /// <summary>
        /// Opens a certificate from a path or store. Stores are expressed as 
        ///     storeLocation:storeName\thumbprint   
        /// or
        ///     storeLocation:storeName\subjectName   (will throw if not unique)
        /// 
        /// where storeLocation must be either 'currentuser' or 'localmachine'        
        /// 
        /// If the certificate is a file, it will use the password stored in the encrypted registry.
        /// </summary>
        /// <param name="location"></param>
        public CertificateReference(string location) {
            if (string.IsNullOrEmpty(location)) {
                throw new ArgumentNullException("location");
            }

            if (File.Exists(location)) {
                _location = location;
                _password = _settings[Path.GetFileName(location), "Password"].EncryptedStringValue;
                if( string.IsNullOrEmpty(_password) ) {
                    _certificate = new X509Certificate2(_location );
                } else {
                    _certificate = new X509Certificate2(_location , _password, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable );
                }
                if( !_certificate.HasPrivateKey ) {
                    throw new Exception("Certificate '{0}' does not have private key".format(_location));
                }
            } else {
                _certificate = LoadCertificateFromStore(location);
                if( _certificate == null ) {
                    throw new Exception("Unable to load certificate '{0}' with a private key".format(_location));
                }
            }
        }

        public void RememberPassword() {
            _settings[Path.GetFileName(_location), "Password"].EncryptedStringValue = _password;
        }

        public static void ClearPassword( string location ) {
            _settings[Path.GetFileName(location), "Password"].EncryptedStringValue = null;
        }

        public static void ClearPasswords() {
            RegistryView.CoAppUser.DeleteSubkey("Certificates");
        }

        public string Location {
            get { return _location; }
        }

        internal X509Certificate2 Certificate { get { return _certificate; }}
    }
}
