using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoApp.Autopackage {
    using System.IO;
    using System.Xml.Linq;
    using Developer.Toolkit.Publishing;
    using Toolkit.DynamicXml;
    using Toolkit.Engine.Model.Atom;
    using Toolkit.Extensions;

    internal class WixDocument {
        private dynamic wix;
        private XDocument wixXml;
        internal PackageSource Source;
        internal AutopackageModel Model;
        internal AtomFeed Feed;

        private dynamic TargetDir;
        private dynamic VendorDir { get {
            return FindOrCreateDirectory(TargetDir, Model.PublisherDirectory);
        }}
        private dynamic ProductDir { get {
            return FindOrCreateDirectory(VendorDir, "{0}-{1}-{2}".format(Model.Name, Model.Version.UInt64VersiontoString(), Model.Architecture).MakeAttractiveFilename());
        }}

        internal WixDocument(PackageSource source, AutopackageModel model, AtomFeed feed) {
            Source = source;
            Model = model;
            Feed = feed;
        }

        public void FillInTemplate() {
            // -----------------------------------------------------------------------------------------------------------------------------------
            // Fill in the package template

            using (var reader = new StringReader(Model.WixTemplate)) {
                wixXml = XDocument.Load(reader);
            }

            wix = new DynamicNode(wixXml);

            SetBasicWixProperties();

            // add all the files
            AddFilesToWix();

            // add the assemblies to the package.
            AddAssembliesToWix();

            // add the bootstrappers
            AddBootstrappersToWix();

            //Add the CoApp Properties
            AddCoAppProperties();
        }

        public void SetBasicWixProperties() {
            wix.Product.Attributes.Id = Model.ProductCode;
            wix.Product.Attributes.Manufacturer = Model.Vendor;
            wix.Product.Attributes.Name = Model.Name;
            wix.Product.Attributes.Version = Model.Version.UInt64VersiontoString();

            TargetDir = wix.Product["Id=TARGETDIR"];
        }

        private void AddBootstrappersToWix() {
            // "CoappBootstrapNativeBin"

            var coappBootstrapNativeBin = wix.Product["Id=bootstrap.exe"];
            if (coappBootstrapNativeBin != null) {
                var bootstrapTempFile = "bootstrap.exe".GenerateTemporaryFilename();

                using (var fs = System.IO.File.Create(bootstrapTempFile)) {
                    fs.Write(Properties.Resources.coapp_native_bootstrap, 0, Properties.Resources.coapp_native_bootstrap.Length);
                }

                // resign the file
                var peBinary = PeBinary.Load(bootstrapTempFile);
                peBinary.StrongNameKeyCertificate = Source.Certificate;
                peBinary.SigningCertificate = Source.Certificate;
                peBinary.CompanyName = Model.Vendor;

                peBinary.Comments = "Installer for " + Model.DisplayName;
                peBinary.ProductName = "Installer for " + Model.DisplayName;
                peBinary.AssemblyTitle = "Installer for " + Model.DisplayName;
                peBinary.AssemblyDescription = "Installer for " + Model.DisplayName;

                peBinary.LegalCopyright = Model.PackageDetails.CopyrightStatement;

                peBinary.ProductVersion = Model.Version.UInt64VersiontoString();
                peBinary.FileVersion = Model.Version.UInt64VersiontoString();

                peBinary.FileVersion = Model.Version.UInt64VersiontoString();
                peBinary.ProductVersion = Model.Version.UInt64VersiontoString();

                peBinary.Save();

                coappBootstrapNativeBin.Attributes.SourceFile = bootstrapTempFile;
            }

            var coappBootstrapBin = wix.Product["Id=bootstrapperui.exe"];
            if (coappBootstrapBin != null) {
                var bootstrapuitempfile =   "bootstrapperui.exe".GenerateTemporaryFilename();

                
                using (var fs = System.IO.File.Create(bootstrapuitempfile)) {
                    fs.Write(Properties.Resources.coapp_managed_bootstrap, 0, Properties.Resources.coapp_managed_bootstrap.Length);
                }

                // resign the file
                var peBinary = PeBinary.Load(bootstrapuitempfile);
                peBinary.StrongNameKeyCertificate = Source.Certificate;
                peBinary.SigningCertificate = Source.Certificate;
                peBinary.CompanyName = Model.Vendor;

                peBinary.Comments = "Installer for " + Model.DisplayName;
                peBinary.ProductName = "Installer for " + Model.DisplayName;
                peBinary.AssemblyTitle = "Installer for " + Model.DisplayName;
                peBinary.AssemblyDescription = "Installer for " + Model.DisplayName;
                peBinary.LegalCopyright = Model.PackageDetails.CopyrightStatement;
                peBinary.FileVersion = Model.Version.UInt64VersiontoString();
                peBinary.ProductVersion = Model.Version.UInt64VersiontoString();

                peBinary.ProductVersion = Model.Version.UInt64VersiontoString();
                peBinary.FileVersion = Model.Version.UInt64VersiontoString();
                peBinary.Save();

                coappBootstrapBin.Attributes.SourceFile = bootstrapuitempfile;
            }
        }

        private dynamic AddNewComponent(dynamic parentDirectory, bool? isPrimary = null) {
            
            var guid = Guid.NewGuid();

            var componentRef = wix.Product["Id=ProductFeature"].Add("ComponentRef");
            componentRef.Attributes.Id = guid.ComponentId();
            if (isPrimary.HasValue) {
                componentRef.Attributes.Primary = isPrimary == true ? "yes" : "no";
            }
            var component = parentDirectory.Add("Component");
            component.Attributes.Id = guid.ComponentId();
            component.Attributes.Guid = guid.ToString("B");

            return component;
        }

        private dynamic FindOrCreateDirectory(dynamic parentDirectory, string subFolderPath) {
            if( string.IsNullOrEmpty(subFolderPath)) {
                return parentDirectory;
            }

            var path = subFolderPath;
            string childPath = null;
            var i = subFolderPath.IndexOf('\\');
            if( i > -1 ) {
                path = subFolderPath.Substring(0, i);
                childPath = subFolderPath.Substring(i+1);
            }

            var immediateSubdirectory = parentDirectory["Name=" + path];

            if( immediateSubdirectory == null) {
                immediateSubdirectory = parentDirectory.Add("Directory");
                immediateSubdirectory.Attributes.Id = path.MakeSafeDirectoryId();
                immediateSubdirectory.Attributes.Name = path;
            }

            if(! string.IsNullOrEmpty(childPath)) {
                return FindOrCreateDirectory(immediateSubdirectory, childPath);
            }

            return immediateSubdirectory;
        }

        private void AddFilesToWix() {
            var folders = Model.DestinationDirectoryFiles.Select(each => Path.GetDirectoryName(each.DestinationPath)).Distinct();
            foreach( var folder in folders ) {
                var directoryElement = FindOrCreateDirectory(ProductDir, folder);
                var component = AddNewComponent(directoryElement);
                var filesInFolder = Model.DestinationDirectoryFiles.Where(each => Path.GetDirectoryName(each.DestinationPath) == folder).ToArray();
                var first = true;

                foreach( var file in filesInFolder ) {
                    var filename = Path.GetFileName(file.DestinationPath);
                    var newFile = component.Add("File");
                    newFile.Attributes.Id = filename.MakeSafeDirectoryId();
                    newFile.Attributes.Name = filename;
                    if (first) {
                        newFile.Attributes.KeyPath = "yes";
                    }
                    newFile.Attributes.DiskId = "1";
                    newFile.Attributes.Source = file.SourcePath;
                    first = false;
                }
            }
        }

        private void AddAssembliesToWix() {
            foreach (var nativeAssembly in Model.Assemblies.Where(each => !each.IsManaged)) {

            }

            foreach( var managedAssembly in Model.Assemblies.Where(each => each.IsManaged) ) {
                var component = AddNewComponent(ProductDir, true);
                bool first = true;

                foreach (var file in managedAssembly.Filenames) {
                    var filename = Path.GetFileName(file);
                    var newFile = component.Add("File");
                    newFile.Attributes.Id = filename.MakeSafeDirectoryId();
                    newFile.Attributes.Name = filename;
                    if (first) {
                        newFile.Attributes.KeyPath = "yes";
                        newFile.Attributes.Assembly = ".net";
                        newFile.Attributes.AssemblyManifest = newFile.Attributes.Id;
                    }

                    newFile.Attributes.DiskId = "1";
                    newFile.Attributes.Source = file;
                    
                    first = false;
                }

                // any policy files needed?
                // foreach( var policy in Model)
            }
        }

       

        private void AddCoAppProperties() {
            var feed = Feed.ToString().FormatWithMacros(Source.PropertySheets.First().GetMacroValue,null);
            var property = wix.Product.Add("Property", feed);
            property.Attributes.Id = "CoAppPackageFeed";

            property = wix.Product.Add("Property", Model.CompositionRules.ToXml("CompositionRules").FormatWithMacros(Source.PropertySheets.First().GetMacroValue, null));
            property.Attributes.Id = "CoAppCompositionRules";

            property = wix.Product.Add("Property", Model.CanonicalName);
            property.Attributes.Id = "CanonicalName";
        }

        public string CreatePackageFile(string msiFilename) {
            // -----------------------------------------------------------------------------------------------------------------------------------
            // Run WiX to generate the MSI

            // at the end of the step, if there are any errors, let's print them out and exit now.

            // Set the namespace on all the elements in the doc. :(
            XNamespace wixNS = "http://schemas.microsoft.com/wix/2006/wi";
            foreach (var n in wixXml.Descendants()) {
                n.Name = wixNS + n.Name.LocalName;
            }

            AutopackageMessages.Invoke.Verbose("Generated WixFile\r\n\r\n{0}",wixXml.ToString());

            // file names
            var wixfile = (Path.GetFileNameWithoutExtension(msiFilename) + ".wxs").GenerateTemporaryFilename();
            var wixobj = wixfile.ChangeFileExtensionTo("wixobj");

            msiFilename.TryHardToDelete();

            // Write out the WixFile
            wixXml.Save(wixfile);

            // Compile the Wix File
            AutopackageMessages.Invoke.Verbose("==> Compiling Generated Wix Package.");
            var rc = Tools.WixCompiler.Exec(@"-nologo -sw1075 -out ""{0}"" ""{1}"" ", wixobj, wixfile);
            if (rc != 0) {
                AutopackageMessages.Invoke.Error(MessageCode.WixCompilerError, null, "{0}\r\n{1}", Tools.WixCompiler.StandardOut, Tools.WixCompiler.StandardError);
                return null;
            }

            AutopackageMessages.Invoke.Verbose("==> Linking Wix object files into MSI.");

            rc = Tools.WixLinker.Exec(@"-nologo -sw1076  -out ""{0}"" ""{1}""", msiFilename, wixobj);
            if (rc != 0) {
                AutopackageMessages.Invoke.Error(MessageCode.WixLinkerError, null, "{0}\r\n{1}", Tools.WixLinker.StandardOut, Tools.WixLinker.StandardError);
                return null;
            }
            AutopackageMessages.Invoke.Verbose("MSI Generated [{0}].", msiFilename);
            return msiFilename;

        }
    }
}
