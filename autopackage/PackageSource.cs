using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoApp.Autopackage {
    using System.IO;
    using Developer.Toolkit.Publishing;
    using Toolkit.Engine.Client;
    using Toolkit.Exceptions;
    using Toolkit.Extensions;
    using Toolkit.Scripting.Languages.PropertySheet;

    internal class PackageSource {
        internal CertificateReference Certificate;
        internal string SigningCertPassword;
        internal string SigningCertPath = string.Empty;
        internal bool Remember;

        internal PackageManager PackageManager;
        // collection of propertysheets
        internal  PropertySheet[] PropertySheets;

        // all the different sets of rules 
        internal Rule[] AllRules;
        internal Rule[] DefineRules;
        internal Rule[] ApplicationRules;
        internal Rule[] AssemblyRules;
        internal Rule[] AssembliesRules;
        internal Rule[] DeveloperLibraryRules;
        internal Rule[] SourceCodeRules;
        internal Rule[] ServiceRules;
        internal Rule[] WebApplicationRules;
        internal Rule[] DriverRules;
        internal Rule[] AllRoles;

        internal IEnumerable<Rule> PackageRules;
        internal IEnumerable<Rule> MetadataRules;
        internal IEnumerable<Rule> RequiresRules;
        internal IEnumerable<Rule> ProvidesRules;
        internal IEnumerable<Rule> CompatabilityPolicyRules;
        internal IEnumerable<Rule> PackageCompositionRules;
        internal IEnumerable<Rule> IdentityRules;
        internal IEnumerable<Rule> SigningRules;
        internal IEnumerable<Rule> FileRules;

        internal void FindCertificate() {

            if (string.IsNullOrEmpty(SigningCertPath)) {
                Certificate = CertificateReference.Default;
                if (Certificate == null) {
                    throw new ConsoleException("No default certificate stored in the registry");
                }
            } else if (string.IsNullOrEmpty(SigningCertPassword)) {
                Certificate = new CertificateReference(SigningCertPath);
            } else {
                Certificate = new CertificateReference(SigningCertPath, SigningCertPassword);
            }

            AutopackageMessages.Invoke.Verbose("Loaded certificate with private key {0}", Certificate.Location);

            if (Remember) {
                AutopackageMessages.Invoke.Verbose("Storing certificate details in the registry.");
                Certificate.RememberPassword();
                CertificateReference.Default = Certificate;
            }

           
        }

        internal void LoadPackageSourceData(IEnumerable<string> parameters) {
            // ------ Load Information to create Package 

            FindCertificate();

            // better make sure that the package manager is running/listening...
            StartPackageManager();

            // load up all the specified property sheets
            LoadPropertySheets(parameters);

            // Determine the roles that are going into the MSI, and ensure we know the basic information for the package (ver, arch, etc)
            CollectRoleRules();
        }

        internal Dictionary<string, string> MacroValues = new Dictionary<string, string>();

        internal string GetMacroValue(string valuename) {
            if( valuename == "DEFAULTLAMBDAVALUE") {
                return "${each.Path}";
            }

            var parts = valuename.Split('.');
            if( parts.Length == 3) {
                var result = AllRules.GetRulesByName(parts[0]).GetRulesByParameter(parts[1]).GetPropertyValue(parts[2]);
                if( result != null ) {
                    return result;
                }
            }

            if( parts.Length == 2) {
                var result = AllRules.GetRulesByName(parts[0]).GetPropertyValue(parts[1]);
                if( result != null ) {
                    return result;
                }
            }

            return DefineRules.GetPropertyValue(valuename) ?? (MacroValues.ContainsKey(valuename) ? MacroValues[valuename] : null);
        }

        internal IEnumerable<object> GetFileCollection(string collectionname) {
            // we use this to pick up file collections.
            var fileRule = FileRules.Where(each => each.Parameter == collectionname).FirstOrDefault();

            if( fileRule == null) {
                AutopackageMessages.Invoke.Error(MessageCode.UnknownFileList, null, "Reference to unknown file list '{0}'", collectionname);
            } else {
                var list = FileList.GetFileList(collectionname, FileRules);
                return list.FileEntries.Select(each => new {
                    Path = each.DestinationPath,
                    Name = Path.GetFileName(each.DestinationPath),
                    Extension = Path.GetExtension(each.DestinationPath),
                    NameWithoutExtension = Path.GetFileNameWithoutExtension(each.DestinationPath),
                });
            }
            
            return Enumerable.Empty<object>();
        }

        internal void LoadPropertySheets(IEnumerable<string> parameters) {
            PropertySheets = parameters.Select(
                each => {
                    if (!File.Exists(each.GetFullPath())) {
                        throw new ConsoleException("Can not find autopackage file '{0}'", each.GetFullPath());
                    }

                    var result = PropertySheet.Load(each);
                    result.GetCollection += GetFileCollection;
                    result.GetMacroValue += GetMacroValue;

                    return result;
                }).ToArray();

            // this is the master list of all the rules from all included sheets
            AllRules = PropertySheets.SelectMany(each => each.Rules).Reverse().ToArray();

            // this is the collection of rules for all the #define category. (macros)
            DefineRules = AllRules.GetRulesById("define").GetRulesByName("*").ToArray();

            // lets generate ourselves some rule lists from the loaded propertysheets.
            FileRules = AllRules.GetRulesByName("files");

            PackageRules = AllRules.GetRulesByName("package");
            MetadataRules = AllRules.GetRulesByName("metadata");
            RequiresRules = AllRules.GetRulesByName("requires");
            ProvidesRules = AllRules.GetRulesByName("provides");

            CompatabilityPolicyRules = AllRules.GetRulesByName("compatability-policy");
            PackageCompositionRules = AllRules.GetRulesByName("package-composition");
            IdentityRules = AllRules.GetRulesByName("identity");
            SigningRules = AllRules.GetRulesByName("signing");
        }

        internal void CollectRoleRules() {
            // -----------------------------------------------------------------------------------------------------------------------------------
            // Determine the roles that are going into the MSI, and ensure we know the basic information for the package (ver, arch, etc)
            // Available Roles are:
            // application 
            // assembly (assemblies is a short-cut for making many assembly rules)
            // service
            // web-application
            // developer-library
            // source-code
            // driver

            ApplicationRules = AllRules.GetRulesByName("application").ToArray();
            AssemblyRules = AllRules.GetRulesByName("assembly").ToArray();
            AssembliesRules = AllRules.GetRulesByName("assemblies").ToArray();
            DeveloperLibraryRules = AllRules.GetRulesByName("developer-library").ToArray();
            SourceCodeRules = AllRules.GetRulesByName("source-code").ToArray();
            ServiceRules = AllRules.GetRulesByName("service").ToArray();
            WebApplicationRules = AllRules.GetRulesByName("web-application").ToArray();
            DriverRules = AllRules.GetRulesByName("driver").ToArray();
            AllRoles = ApplicationRules.Union(AssemblyRules).Union(DeveloperLibraryRules).Union(SourceCodeRules).Union(ServiceRules).Union(WebApplicationRules).
                Union(DriverRules).ToArray();

            // check for any roles...
            if (!AllRoles.Any()) {
                AutopackageMessages.Invoke.Error(
                    MessageCode.ZeroPackageRolesDefined, null,
                    "No package roles are defined. Must have at least one of {{ application, assembly, service, web-application, developer-library, source-code, driver }} rules defined.");
            }
        }

        internal void StartPackageManager() {
            // ok, we're looking like we're ready to need the package manager.
            // make sure its running.
            PackageManager.Instance.ConnectAndWait("autopackage", null, 15000);

            PackageManager = PackageManager.Instance;
            PackageManager.AddFeed(Environment.CurrentDirectory,true);

            if (AutopackageMain._verbose) {
                PackageManager.SetLogging(true, true, true);
            }
        }
    }
}
