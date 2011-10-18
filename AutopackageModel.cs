//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Autopackage {
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;
    using System.ServiceModel.Syndication;
    using System.Xml;
    using System.Xml.Serialization;
    using Developer.Toolkit.Publishing;
    using Properties;
    using Toolkit.Crypto;
    using Toolkit.Engine;
    using Toolkit.Engine.Client;
    using Toolkit.Engine.Model;
    using Toolkit.Engine.Model.Atom;
    using Toolkit.Extensions;
    using Toolkit.Win32;

    [XmlRoot(ElementName = "Package", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class AutopackageModel : PackageModel {
        [XmlIgnore]
        private PackageSource Source;

        [XmlIgnore]
        internal string Vendor;

        [XmlIgnore]
        internal IEnumerable<FileEntry> DestinationDirectoryFiles;

        // Assemblies Roles

        [XmlIgnore]
        internal List<PackageAssembly> Assemblies;

        // package templates 
        [XmlIgnore]
        private string _managedPublisherConfiguration;

        [XmlIgnore]
        private string _nativePublisherConfiguration;

        [XmlIgnore]
        private string _nativeAssemblyManifest;

        [XmlIgnore]
        internal string WixTemplate;

        [XmlIgnore]
        private AtomFeed AtomFeed;

        internal AutopackageModel() {
            DestinationDirectoryFiles = Enumerable.Empty<FileEntry>();
            Assemblies = new List<PackageAssembly>();
        }

        internal AutopackageModel(PackageSource source, AtomFeed feed) {
            Source = source;
            DestinationDirectoryFiles = Enumerable.Empty<FileEntry>();
            Assemblies = new List<PackageAssembly>();
            foreach( var sheet in Source.PropertySheets ) {
                sheet.GetMacroValue += GetMacroValue;
            }
            AtomFeed = feed;
        }

        internal string GetMacroValue( string macroKey ) {
            if( macroKey.StartsWith("Model.") ) {
                var result = this.SimpleEval(macroKey.Substring(6));
                if (result == null || string.Empty == result.ToString()) {
                    return null;
                }

                return result.ToString();
            }
            return null;
        }

        internal void ProcessCertificateInformation() {
            Vendor = Source.Certificate.CommonName;
            PublisherDirectory = Vendor.MakeAttractiveFilename();
        }

        internal void ProcessPackageTemplates() {
            // load template data from script stack
            _nativePublisherConfiguration = Source.AllRules.GetProperty("templates", "native-publisher-configuration").Value as string;
            _managedPublisherConfiguration = Source.AllRules.GetProperty("templates", "managed-publisher-configuration").Value as string;
            _nativeAssemblyManifest = Source.AllRules.GetProperty("templates", "native-assembly-manifest").Value as string;

            WixTemplate = Resources.WixTemplate;
        }

        internal void ProcessFileLists() {
            // -----------------------------------------------------------------------------------------------------------------------------------
            // Run through the file lists and gather in all the files that we're going to include in the package.
            foreach (var fileSet in Source.FileRules.Select(each => each.Parameter).Distinct()) {
                FileList.GetFileList(fileSet, Source.FileRules);
            }
        }

        internal void ProcessApplicationRole() {
            // application rule supports the following properties:
            // include -- may include files or filesets; can not set 'destination' here, must set that in previously defined filesets.
            foreach (var AppRule in Source.ApplicationRules) {
                var files = FileList.ProcessIncludes(null, AppRule, "application", Source.FileRules, Environment.CurrentDirectory);
                var name = AppRule.Parameter;

                if (!string.IsNullOrEmpty(name)) {
                    files = files.Select(
                        each => new FileEntry ( each.SourcePath, Path.Combine(name.MakeSafeFileName(), each.DestinationPath))).ToArray();
                }
                DestinationDirectoryFiles = DestinationDirectoryFiles.Union(files);

            }
        }

        internal void ProcessAssemblyRules() {
            foreach (var asmRule in Source.AssembliesRules) {
                // create an assembly for each one of the files.
                var asmFiles = FileList.ProcessIncludes(null, asmRule, "assemblies", Source.FileRules, Environment.CurrentDirectory);
                Assemblies.AddRange(
                    asmFiles.Select(file => new PackageAssembly(Path.GetFileNameWithoutExtension(file.SourcePath), asmRule, file.SourcePath)));
            }

            foreach (var asm in Source.AssemblyRules) {
                var fileList = FileList.ProcessIncludes(null, asm, "assembly", Source.FileRules, Environment.CurrentDirectory);
                Assemblies.Add(new PackageAssembly(asm.Parameter, asm, fileList.Select(each => each.SourcePath)));
            }

            // now, check to see that our assemblies are unique.
            var assemblyNames = Assemblies.Select(each => each.Name).ToArray();
            if (assemblyNames.Count() != assemblyNames.Distinct().Count()) {
                // there is a duplicate there somewhere. run thru the list and rat em out.
                foreach (var name in assemblyNames) {
                    var asms = Assemblies.Where(each => each.Name == name);
                    if (asms.Count() > 1) {
                        foreach (var a in asms) {
                            AutopackageMessages.Invoke.Error(
                                MessageCode.DuplicateAssemblyDefined, a.Rule.SourceLocation, "Assembly with name '{0}' defined more than once.", name);
                        }
                    }
                }
                // fail fast, this is pointless.
                return;
            }

            // check to see that all the assemblies are the same archetecture.
            var arches = Assemblies.Select(each => each.Architecture).Distinct().ToArray();
            if (arches.Length > 1) {
                foreach (var asm in Assemblies) {
                    AutopackageMessages.Invoke.Error(
                        MessageCode.MultipleAssemblyArchitectures, asm.Rule.SourceLocation,
                        "All Assemblies must have the same architecture. '{0}' architecure => {1}.", asm.Name, asm.Architecture);
                }
                // fail fast, this is pointless.
                return;
            }

            // check to see that all the assemblies are the same version.
            var versions = Assemblies.Select(each => each.Version).Distinct().ToArray();
            if (versions.Length > 1) {
                foreach (var asm in Assemblies) {
                    AutopackageMessages.Invoke.Error(
                        MessageCode.MultipleAssemblyVersions, asm.Rule.SourceLocation, "All Assemblies must have the same version. '{0}' Version => {1}.",
                        asm.Name, asm.Version);
                }
                // fail fast, this is pointless.
                return;
            }

            foreach (var assembly in Assemblies) {
                assembly.PublicKeyToken = Source.Certificate.PublicKeyToken;
            }
        }

        internal void ProcessDependencyInformation() {
            // -----------------------------------------------------------------------------------------------------------------------------------
            // Step 3 : Gather the dependency information for the package

            // explictly defined
            var dependentPackages = new List<Package>();

            foreach (var pkgName in Source.RequiresRules.SelectMany(each => each["package"].Values)) {
                // for now, lets just see if we can do a package match, and grab just that packages
                // in the future, we should figure out how to make better decisions for this.
                try {
                    var package = Source.PackageManager.GetPackages(pkgName, null, null, null, null, null, null, null, false, null, false, AutopackageMain._messages).Result;

                    if( package.IsNullOrEmpty()) {
                        AutopackageMessages.Invoke.Error( MessageCode.FailedToFindRequiredPackage, null, "Failed to find package '{0}'.", pkgName);
                    }

                    var pkg = package.FirstOrDefault();
                    Source.PackageManager.GetPackageDetails(pkg.CanonicalName,AutopackageMain._messages).Wait();

                    dependentPackages.Add(package.FirstOrDefault());
                    
                } catch (Exception e) {
                    AutopackageMessages.Invoke.Error(
                        MessageCode.FailedToFindRequiredPackage, null, "Failed to find package '{0}'. [{1}]", pkgName, e.Message);
                }
            }

            
            foreach( var pkg in dependentPackages) {
                if (Dependencies == null ) {
                    Dependencies = new List<Guid>();
                }
                Dependencies.Add(new Guid(pkg.ProductCode));
                // also, add that package's atom feed items to this package's feed.
                if(! string.IsNullOrEmpty(pkg.PackageItemText) ) {
                    var item = SyndicationItem.Load<AtomItem>(XmlReader.Create(new StringReader(pkg.PackageItemText)));
                    AtomFeed.Add(item);
                }
            }

            // implicitly defined (check all binaries, to see what they depend on)
            // maybe in RC.
        }

        private void DigitallySign(string filename) {
            var peBinary = PeBinary.Load(filename);
            if (peBinary.IsManaged) {
                peBinary.StrongNameKeyCertificate = Source.Certificate;
            }
            peBinary.SigningCertificate = Source.Certificate;
            peBinary.Save();
        }

        internal void ProcessDigitalSigning() {
            // -----------------------------------------------------------------------------------------------------------------------------------
            // Step 4 : Ensure digital signatures and strong names are all good to go
            foreach (var signRule in Source.SigningRules) {
                var reSign = signRule.HasProperty("replace-signature") && signRule["replace-signature"].Value.IsTrue();

                var filesToSign = FileList.ProcessIncludes(null, signRule, "signing", Source.FileRules, Environment.CurrentDirectory);
                foreach (var file in filesToSign) {
                    if (reSign || !Verifier.HasValidSignature(file.SourcePath)) {
                        DigitallySign(file.SourcePath);
                    }
                }
            }

            // verify that all files that should be signed are actually signed.
            // TODO : make sure stuff is actually signed.
        }

        internal void ProcessBasicPackageInformation() {
            // -----------------------------------------------------------------------------------------------------------------------------------
            // New Step: Validate the basic information of this package
            Name = Source.PackageRules.GetProperty("name").Value;

            if (string.IsNullOrEmpty(Name)) {
                AutopackageMessages.Invoke.Error(
                    MessageCode.MissingPackageName, Source.PackageRules.Last().SourceLocation, "Missing property 'name' in 'package' rule.");
            }

            Version = (Source.PackageRules.GetProperty("version") as string).VersionStringToUInt64();
            if (Version == 0) {
                // try to figure out package version from binaries.
                // check assemblies first
                foreach (var assembly in Assemblies) {
                    Version = assembly.Version.VersionStringToUInt64();
                    if (Version == 0) {
                        AutopackageMessages.Invoke.Error(
                            MessageCode.AssemblyHasNoVersion, assembly.Rule.SourceLocation, "Assembly '{0}' doesn't have a version.", assembly.Name);
                    } else {
                        AutopackageMessages.Invoke.Warning(
                            MessageCode.AssumingVersionFromAssembly, Assemblies.First().Rule.SourceLocation,
                            "Package Version not specified, assuming version '{0}' from first assembly", Version.UInt64VersiontoString());

                        if (Architecture == Architecture.Auto || Architecture == Architecture.Unknown) {
                            // while we're here, let's grab this as the architecture.
                            Architecture = assembly.Architecture;
                        }

                        break;
                    }
                }

                // check application next 
                foreach (var file in DestinationDirectoryFiles) {
                    var pe = PEInfo.Scan(file.SourcePath);
                    if (pe.IsPEBinary) {
                        Version = pe.FileVersion.VersionStringToUInt64();

                        if (Architecture == Architecture.Auto || Architecture == Architecture.Unknown) {
                            // while we're here, let's grab this as the architecture.
                            if (pe.IsAny) {
                                Architecture = Architecture.Any;
                            } else if (pe.Is64Bit) {
                                Architecture = Architecture.x64;
                            } else {
                                Architecture = Architecture.x86;
                            }
                        }

                        if (Version == 0) {
                            AutopackageMessages.Invoke.Warning(
                                MessageCode.AssumingVersionFromApplicationFile, null,
                                "Package Version not specified, assuming version '{0}' from application file '{1}'", Version.UInt64VersiontoString(),
                                file.SourcePath);

                            if (Architecture == Architecture.Auto || Architecture == Architecture.Unknown) {
                                // while we're here, let's grab this as the architecture.
                                if (pe.IsAny) {
                                    Architecture = Architecture.Any;
                                } else if (pe.Is64Bit) {
                                    Architecture = Architecture.x64;
                                } else {
                                    Architecture = Architecture.x86;
                                }
                            }
                            break;
                        }
                    }
                }

                if (Version == 0) {
                    AutopackageMessages.Invoke.Error(MessageCode.UnableToDeterminePackageVersion, null, "Unable to determine package version.");
                }
            }

            var arch = Source.PackageRules.GetProperty("arch");
            if ((Architecture == Architecture.Auto || Architecture == Architecture.Unknown )&& arch != null) {
                try {
                    Architecture = Enum.Parse(typeof(Architecture), arch, true);
                } catch {
                }
            }

            // is it still not set?
            if (Architecture == Architecture.Auto || Architecture == Architecture.Unknown) {
                // figure it out from what's going in the package.
                AutopackageMessages.Invoke.Error(MessageCode.UnableToDeterminePackageArchitecture, null, "Unable to determine package architecture.");
            }

            if (string.IsNullOrEmpty(PublicKeyToken)) {
                PublicKeyToken = Source.Certificate.PublicKeyToken;
            }

            var locations = Source.PackageRules.GetPropertyValues("location").Union(Source.PackageRules.GetPropertyValues("locations"));
            if( !locations.IsNullOrEmpty()) {
                Locations = new List<Uri>();
                Locations.AddRange(locations.Select(location => location.ToUri()).Where(uri => uri != null));
            }
            

            var feeds = Source.PackageRules.GetPropertyValues("feed").Union(Source.PackageRules.GetPropertyValues("feeds"));
            if (!feeds.IsNullOrEmpty()) {
                Feeds = new List<Uri>();
                Feeds.AddRange(feeds.Select(feed => feed.ToUri()).Where(uri => uri != null));
            }

            var publisher = Source.PackageRules.GetPropertyValue("publisher");

            if( !string.IsNullOrEmpty(publisher)) {
                var identityRules = Source.IdentityRules.GetRulesByParameter(publisher);
                if (!identityRules.IsNullOrEmpty()) {
                    PackageDetails.Publisher = new Identity {
                        Name = identityRules.GetPropertyValue("name"),
                        Email = identityRules.GetPropertyValue("email"),
                        Location = identityRules.GetPropertyValue("website").ToUri()
                    };
                }

            }

        }

        internal void ProcessAssemblyManifests() {
            // -----------------------------------------------------------------------------------------------------------------------------------
            // Step 5 : Build Assembly Manifests, catalog files and policy files
            var policyRule = Source.CompatabilityPolicyRules.FirstOrDefault();
            var versionRange = string.Empty;

            IEnumerable<string> versions = Enumerable.Empty<string>();

            // figure out what major/minor versions we need are overriding.
            var minimum = policyRule["minimum"].Value.VersionStringToUInt64();
            var maximum = policyRule["maximum"].Value.VersionStringToUInt64();
            if (minimum != 0 && maximum == 0) {
                maximum = Version - 1;
            }

            if (minimum != 0) {
                versionRange = @"{0}-{1}".format(minimum.UInt64VersiontoString(), maximum.UInt64VersiontoString());
            }

            if (policyRule != null) {
                versions = policyRule["versions"].Values;

                if (versions.IsNullOrEmpty()) {
                    // didn't specify versions explicitly.
                    // we can check for overriding versions.
                    // TODO: SOON
                }
            }

            if (minimum > 0) {
                BindingPolicyMinVersion = minimum;
                BindingPolicyMaxVersion = maximum;
            }


            var nativeAssemblies = Assemblies.Where(each => !each.IsManaged).ToArray();
            foreach (var nativeAssembly in nativeAssemblies) {
                // create assembly manifest
                var manifestXml = _nativeAssemblyManifest.FormatWithMacros(Source.GetMacroValue, new {Assembly = nativeAssembly});
                // write out manifest wherever we need it.

                if (minimum != 0) {
                    foreach (var oldVersion in versions) {
                        var policyXml = _nativePublisherConfiguration.FormatWithMacros(
                            Source.GetMacroValue, new {
                                Assembly = nativeAssembly,
                                OldAssembly = new {
                                    MajorMinorVersion = oldVersion,
                                    VersionRange = versionRange
                                },
                            });

                        // write out policyXml wherever we're needin' it.
                    }
                }
            }

            // create a policy assembly for each one of the policies required for each of the managed assemblies.
            var managedAssemblies = Assemblies.Where(each => each.IsManaged).ToArray();
            if (minimum != 0) {
                foreach (var managedAssembly in managedAssemblies) {
                    // create the policy file 
                    foreach (var oldVersion in versions) {
                        var policyXml = _managedPublisherConfiguration.FormatWithMacros(
                            Source.GetMacroValue, new {
                                Assembly = managedAssembly,
                                OldAssembly = new {
                                    MajorMinorVersion = oldVersion,
                                    VersionRange = versionRange
                                },
                            });

                        var policyConfigFile = "policy.{0}.{1}.dll.config".format(oldVersion, managedAssembly.Name).InTempFolder();
                        var policyFile = "policy.{0}.{1}.dll".format(oldVersion, managedAssembly.Name).InTempFolder();

                        // write out the policy config file
                        File.WriteAllText(policyConfigFile, policyXml);

                        var rc = Tools.AssemblyLinker.Exec("/link:{0} /out:{1} /v:{2}", policyConfigFile, policyFile, Version.UInt64VersiontoString());
                        if (rc != 0) {
                            AutopackageMessages.Invoke.Error(
                                MessageCode.AssemblyLinkerError, null, "Unable to make policy assembly\r\n{0}",
                                Tools.AssemblyLinker.StandardError + Tools.AssemblyLinker.StandardOut);
                        }

                        DigitallySign(policyFile);

                        // and now we can create assembly entries for these.
                        Assemblies.Add(new PackageAssembly(Path.GetFileName(policyFile), null, new[] {policyFile, policyConfigFile}));
                    }
                }
            }
        }

        internal void ProcessCosmeticMetadata() {
            PackageDetails.Description = Source.MetadataRules.GetPropertyValue("description").LiteralOrFileText();
            PackageDetails.SummaryDescription = Source.MetadataRules.GetPropertyValue("summary");

            var iconFilename = Source.MetadataRules.GetPropertyValue("icon");

            if (File.Exists(iconFilename)) {
                try {
                    Image img = Image.FromFile(iconFilename);
                    if (img.Width > 256 || img.Height > 256) {
                        var widthIsConstraining = img.Width > img.Height;
                        // Prevent using images internal thumbnail
                        img.RotateFlip(RotateFlipType.Rotate180FlipNone);
                        img.RotateFlip(RotateFlipType.Rotate180FlipNone);
                        var newWidth = widthIsConstraining ? 256 : img.Width * 256 / img.Height;
                        var newHeight = widthIsConstraining ? img.Height * 256 / img.Width : 256;
                        var newImage = img.GetThumbnailImage(newWidth, newHeight, null, IntPtr.Zero);
                        img.Dispose();
                        img = newImage;
                    }

                    using (var ms = new MemoryStream()) {
                        img.Save(ms, ImageFormat.Png);
                        PackageDetails.Icon = Convert.ToBase64String(ms.ToArray());

                    }
                }
                catch (Exception e) {
                    AutopackageMessages.Invoke.Warning(MessageCode.BadIconReference, Source.MetadataRules.GetProperty("icon").SourceLocation,
                        "Unable to use specified image for icon {0}", e.Message);
                }
            }
            else {
                AutopackageMessages.Invoke.Warning(MessageCode.NoIcon, Source.MetadataRules.GetProperty("icon").SourceLocation,
                    "Image for icon not specified (or not found) {0}", iconFilename);
            }
            var licenses = Source.MetadataRules.GetPropertyValues("licenses");
            if( licenses.Any()) {
                PackageDetails.Licenses = new List<License>();
            }
            foreach( var l in licenses) {

                LicenseId lid;
                if( LicenseId.TryParse(l, true, out lid)) {
                    PackageDetails.Licenses.Add(new License { 
                        LicenseId = lid, 
                        Location = lid.GetUrl(), 
                        Name = lid.GetDescription(),
                       // Text = lid.GetText(),
                    });
                }

                // todo : let the user specify the license data in a license rule.
            }

            PackageDetails.AuthorVersion = Source.MetadataRules.GetPropertyValue("author-version");
            PackageDetails.BugTracker = Source.MetadataRules.GetPropertyValue("bug-tracker");
            var pubDate = Source.MetadataRules.GetPropertyValue("publish-date");
            PackageDetails.PublishDate = DateTime.Now;

            if( !string.IsNullOrEmpty(pubDate) && pubDate != "auto") {
                DateTime dt;
                if( DateTime.TryParse(pubDate,out dt) ) {
                    PackageDetails.PublishDate = dt;
                } else {
                    AutopackageMessages.Invoke.Warning(MessageCode.BadDate, Source.MetadataRules.GetProperty("publish-date").SourceLocation,
                       "Can't parse publish date {0}, assuming now", pubDate);
                }
            }
            
            PackageDetails.IsNsfw = Source.MetadataRules.GetPropertyValue("nsfw").IsTrue();
            PackageDetails.Stability = (sbyte)(Source.MetadataRules.GetPropertyValue("stability").ToInt32());
            

            PackageDetails.Tags = Source.MetadataRules.GetPropertyValues("tags").ToList();
            

            var contributors = Source.MetadataRules.GetPropertyValues("contributors");

            if (!contributors.IsNullOrEmpty()) {
                PackageDetails.Contributors = new List<Identity>();

                foreach( var contributor in contributors ) {
                    var identityRules = Source.IdentityRules.GetRulesByParameter(contributor);
                    if (!identityRules.IsNullOrEmpty()) {
                        PackageDetails.Contributors.Add(  new Identity {
                            Name = identityRules.GetPropertyValue("name"),
                            Email = identityRules.GetPropertyValue("email"),
                            Location = identityRules.GetPropertyValue("website").ToUri()
                        });
                    }
                }
            }


        }

        internal void ProcessCompositionRules() {
            CompositionRules = new List<CompositionRule>();
            // PackageCompositionRules = AllRules.GetRulesByName("package-composition");
            var compositionRuleCategories = Source.PackageCompositionRules.Select(each => each.Parameter).Distinct();
            
            foreach( var category in compositionRuleCategories) {
                var categoryRules = Source.PackageCompositionRules.GetRulesByParameter(category);
                foreach(var rule in categoryRules) {
                    foreach( var propertyName in rule.PropertyNames) {
                        CompositionAction type;

                        switch( propertyName ) {
                            case "symlink":
                            case "symlinks":
                            case "symlink-file":
                            case "symlink-files":
                                type = CompositionAction.SymlinkFile;
                                break;
                            case "registry":
                            case "registry-keys":
                                type = CompositionAction.Registry;
                                break;
                            case "symlink-folder":
                            case "symlink-folderss":
                                type = CompositionAction.SymlinkFolder;
                                break;
                            case "environment-variable":
                            case "environment-variables":
                                type = CompositionAction.EnvironmentVariable;
                                break;
                            case "shortcut":
                            case "shortcuts":
                                type = CompositionAction.Shortcut;
                                break;
                            default:
                                AutopackageMessages.Invoke.Error(MessageCode.UnknownCompositionRuleType, rule.SourceLocation, "Unknown composition rule '{0}'",
                                    propertyName);
                                continue;
                        }

                        var propertyValue = rule[propertyName];

                        if (!propertyValue.Labels.IsNullOrEmpty()) {
                            foreach (var label in propertyValue.Labels) {
                                CompositionRules.Add( new CompositionRule {
                                    Action = type,
                                    Category = category,
                                    Link = label,
                                    Target = propertyValue[label].Value
                                });
                            }
                        }
                    }
                }
            }
        }
    }
}