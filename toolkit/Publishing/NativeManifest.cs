//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Developer.Toolkit.Publishing {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using System.Xml.Linq;
    using CoApp.Toolkit.Exceptions;
    using CoApp.Toolkit.Extensions;
    using CoApp.Toolkit.Win32;

    internal static class Namespaces {
        internal static XNamespace AssemblyV3 = XNamespace.Get("urn:schemas-microsoft-com:asm.v3");
        internal static XNamespace AssemblyV1 = XNamespace.Get("urn:schemas-microsoft-com:asm.v1");
        internal static XNamespace CompatabilityV1 = XNamespace.Get("urn:schemas-microsoft-com:compatibility.v1");
        internal static XNamespace WindowsSettings = XNamespace.Get("http://schemas.microsoft.com/SMI/2005/WindowsSettings");
    }

    public class ManifestException : CoAppException {
        public ManifestException(string message) : base(message) {
        }
    }

    public class InvalidManifestElement : ManifestException {
        public XElement Element;

        public InvalidManifestElement(string message, XElement element) : base(message) {
            Element = element;
        }
    }

    public class UnknownManifestElement : ManifestException {
        public XElement Element;

        public UnknownManifestElement(string message, XElement element)
            : base(message) {
            Element = element;
        }
    }

    /// <summary>
    /// For the UAC Execution Level in the manifest.
    /// Must not be case corrected.
    /// </summary>
    public enum ExecutionLevel {
// ReSharper disable InconsistentNaming
        none,
        asInvoker,
        requireAdministrator,
        highestAvailable,
// ReSharper restore InconsistentNaming
    }

    public enum AssemblyType {
        win32,
        win32policy,
    }

    public class BindingRedirect {
        public FourPartVersion Low;
        public FourPartVersion High;
        public FourPartVersion Target;

        public string VersionRange {
            get { return "{0}-{1}".format(Low.ToString(), High.ToString()); }
        }
    }

    public class AssemblyReference {
        public string Name;
        public FourPartVersion Version;
        public Architecture Architecture;
        public string PublicKeyToken;
        public string Language;
        public AssemblyType AssemblyType;
        public BindingRedirect BindingRedirect;
    }

    public static class XElementExtensions {
        public static bool Contains(this XElement element, string childElementName) {
            return element.Elements().Any(each => each.Name == childElementName);
        }

        public static IEnumerable<XElement> Children(this XElement element, string childElementName, params string[] childElementNames) {
            if (childElementNames == null || childElementNames.Length == 0) {
                return element.Elements().Where(each => each.Name == childElementName);
            }
            var result = element.Elements().FirstOrDefault(each => each.Name == childElementName);
            return result != null ? result.Children(childElementNames[0], childElementNames.Skip(1).ToArray()) : Enumerable.Empty<XElement>();
        }

        public static XElement AddOrGet(this XElement element, string childElementName) {
            var result = element.Children(childElementName).FirstOrDefault();
            if (result == null) {
                result = new XElement(childElementName);
                element.Add(result);
            }
            return result;
        }

        public static string AttributeValue(this XElement element, string attributeName) {
            if (element == null) {
                return null;
            }

            var attr = element.Attributes().FirstOrDefault(each => each.Name == attributeName);
            return attr != null ? attr.Value : null;
        }
    }

    public class ManifestElement {
        protected XElement _parentElement;

        public ManifestElement(XElement parentElement) {
            _parentElement = parentElement;
            Validate();
        }

        protected virtual void Validate() {
        }

        protected virtual IEnumerable<XElement> Elements {
            get { return Enumerable.Empty<XElement>(); }
        }

        protected virtual void RemoveExcessElements(int maxElements = 1) {
            // remove excess trustinfo elements, leave the first one found.
            while (Elements.Count() > maxElements) {
                Elements.Skip(maxElements).Remove();
            }
        }

        protected virtual bool Active {
            get { return Elements.Any(); }
// ReSharper disable ValueParameterNotUsed
            set { }
// ReSharper restore ValueParameterNotUsed
        }
    }

    public class AsmV3Application : ManifestElement {
        private const string Asmv3ApplicationTag = "{urn:schemas-microsoft-com:asm.v3}application";
        private const string WindowsSettingsTag = "{urn:schemas-microsoft-com:asm.v3}windowsSettings";
        private const string DpiAwareTag = "{http://schemas.microsoft.com/SMI/2005/WindowsSettings}dpiAware";

        public AsmV3Application(XElement parentElement) : base(parentElement) {
        }

        protected override IEnumerable<XElement> Elements {
            get { return _parentElement.Children(Asmv3ApplicationTag); }
        }

        protected override bool Active {
            get {
                if (Elements.Any()) {
                    return Elements.FirstOrDefault().Children(WindowsSettingsTag, DpiAwareTag).Any();
                }
                return false;
            }
            set { EnsureElementExists(); }
        }

        private XElement EnsureElementExists() {
            if( !Elements.Any()) {
                _parentElement.Add(new XElement(Asmv3ApplicationTag));
            }
            return Elements.FirstOrDefault().AddOrGet(WindowsSettingsTag).AddOrGet(DpiAwareTag);
        }

        /// <summary>
        ///   Ensures that the element contains at most, a single valid application block
        /// </summary>
        protected override void Validate() {
            RemoveExcessElements();

            if (!Active) {
                return;
            }

            // ensure that we have the right child elements
            var x = DpiAwareElenment;
        }

        private XElement DpiAwareElenment {
            get {
                if (!Active) {
                    return null;
                }
                return EnsureElementExists();
            }
        }

        public bool DpiAware {
            get {
                if (!Active) {
                    return false;
                }
                return DpiAwareElenment != null && DpiAwareElenment.Value.Trim().IsTrue();
            }
            set {
                Active = true;
                DpiAwareElenment.Value = value ? "true" : "false";
            }
        }
    }

    public class TrustInfo : ManifestElement {
        private const string TrustInfoTag = "{urn:schemas-microsoft-com:asm.v3}trustInfo";
        private const string SecurityTag = "{urn:schemas-microsoft-com:asm.v3}security";
        private const string RequestedPrivilegesTag = "{urn:schemas-microsoft-com:asm.v3}requestedPrivileges";
        private const string RequestedExecutionLevelTag = "{urn:schemas-microsoft-com:asm.v3}requestedExecutionLevel";
        private const string LevelAttribute = "level";
        private const string UiAccessAttribute = "uiAccess";

        public TrustInfo(XElement parentElement) : base(parentElement) {
        }

        protected override IEnumerable<XElement> Elements {
            get { return _parentElement.Children(TrustInfoTag); }
        }

        /// <summary>
        ///   Ensures that the element contains at most, a single valid trustInfo block
        /// </summary>
        protected override void Validate() {
            RemoveExcessElements();

            if (!Active) {
                return;
            }

            // ensure that we have the right child elements
            var x = RequestedExecutionLevelElement;
        }

        private XElement RequestedExecutionLevelElement {
            get {
                if (!Active) {
                    return null;
                }

                return Elements.FirstOrDefault().AddOrGet(SecurityTag)
                    .AddOrGet(RequestedPrivilegesTag)
                    .AddOrGet(RequestedExecutionLevelTag);
            }
        }

        protected override bool Active {
            set {
                if (value) {
                    if (!Active) {
                        _parentElement.Add(new XElement(TrustInfoTag));
                        Validate();
                    }
                } else {
                    while (Elements.Any()) {
                        Elements.Remove();
                    }
                }
            }
        }

        public ExecutionLevel Level {
            get {
                if (!Active) {
                    return ExecutionLevel.none;
                }
                return RequestedExecutionLevelElement.AttributeValue(LevelAttribute).ParseEnum(ExecutionLevel.none);
            }
            set {
                if (value == ExecutionLevel.none) {
                    if (!UiAccess) {
                        Active = false;
                    } else {
                        RequestedExecutionLevelElement.SetAttributeValue(LevelAttribute, null);
                    }
                } else {
                    Active = true;
                    RequestedExecutionLevelElement.SetAttributeValue(LevelAttribute, value.CastToString());
                }
            }
        }

        public bool UiAccess {
            get {
                if (!Active) {
                    return false;
                }
                return RequestedExecutionLevelElement.AttributeValue(UiAccessAttribute).IsTrue();
            }
            set { RequestedExecutionLevelElement.SetAttributeValue(UiAccessAttribute, value?"true":"false"); }
        }
    }

    public class DependentAssemblies : ManifestElement {
        private const string DependencyTag = "{urn:schemas-microsoft-com:asm.v1}dependency";
        private const string DependentAssemblyTag = "{urn:schemas-microsoft-com:asm.v1}dependentAssembly";

        // AssemblyReference
        // <AssemblyReference><dependentAssembly><assemblyIdentity type=""win32"" name=""[$LIBNAME]"" version=""[$LIBVERSION]"" processorArchitecture=""[$ARCH]"" publicKeyToken=""[$PUBLICKEYTOKEN]"" /></dependentAssembly></AssemblyReference>";

        public DependentAssemblies(XElement parentElement)
            : base(parentElement) {
        }

        protected override void Validate() {
        }

        protected override IEnumerable<XElement> Elements {
            get { return _parentElement.Children(DependencyTag); }
        }

        private IEnumerable<XElement> DependentAssemblyElements {
            get { return Elements.Select(elem => elem.Children(DependentAssemblyTag).FirstOrDefault()).Where(dat => dat != null); }
        }

        public IEnumerable<AssemblyReference> Dependencies {
            get {
                return from dat in DependentAssemblyElements
                    let assemblyIdentity = new AssemblyIdentity(dat)
                    select
                        new AssemblyReference {
                            Name = assemblyIdentity.Name,
                            Version = assemblyIdentity.Version,
                            Architecture = assemblyIdentity.Architecture,
                            PublicKeyToken = assemblyIdentity.PublicKeyToken,
                            Language = assemblyIdentity.Language,
                            AssemblyType = assemblyIdentity.AssemblyType,
                            BindingRedirect = assemblyIdentity.BindingRedirect,
                        };
            }
        }

        public void AddDependency(string name, FourPartVersion version, Architecture arch, string publicKeyToken, string language = "*", AssemblyType assemblyType = AssemblyType.win32, BindingRedirect bindingRedirect = null) {
            if (!(from dat in DependentAssemblyElements
                let assemblyIdentity = new AssemblyIdentity(dat)
                where
                    assemblyIdentity.Name == name &&
                        assemblyIdentity.Version == version &&
                            assemblyIdentity.Architecture == arch &&
                                assemblyIdentity.PublicKeyToken == publicKeyToken &&
                                    ((language == "*" && string.IsNullOrEmpty(assemblyIdentity.Language)) || assemblyIdentity.Language == language)
                select assemblyIdentity).Any()) {
                // add another.
                var dat = new XElement(DependencyTag, new XElement(DependentAssemblyTag));
                var identity = new AssemblyIdentity(dat.Elements().FirstOrDefault()) {
                    AssemblyType = assemblyType,
                    Name = name,
                    Version = version,
                    Architecture = arch,
                    PublicKeyToken = publicKeyToken,
                    Language = language,
                    BindingRedirect = bindingRedirect
                };
                _parentElement.Add(dat);
            }
        }

        public void RemoveDependency(string name, FourPartVersion version, Architecture arch, string publicKeyToken, string language = "*") {
            var deleteThis = (from dat in DependentAssemblyElements
                let assemblyIdentity = new AssemblyIdentity(dat)
                where
                    assemblyIdentity.Name == name &&
                        assemblyIdentity.Version == version &&
                            assemblyIdentity.Architecture == arch &&
                                assemblyIdentity.PublicKeyToken == publicKeyToken &&
                                    ((language == "*" && string.IsNullOrEmpty(assemblyIdentity.Language)) || assemblyIdentity.Language == language)
                select dat).FirstOrDefault();

            if (deleteThis != null) {
                deleteThis.Parent.Remove();
            }
        }
    }

    public class AssemblyFile : ManifestElement {
        private const string FileTag = "{urn:schemas-microsoft-com:asm.v1}file";
        public AssemblyFile(XElement parentElement)
            : base(parentElement) {
        }

        protected override IEnumerable<XElement> Elements {
            get { return _parentElement.Children(FileTag); }
        }

        protected override bool Active {
            get {
                return Elements.Any();
            }
        }

        public void AddFile(string filename, string SHA1Hash=null ) {
            RemoveFile(filename);
            
            var newFile = new XElement(FileTag);
            newFile.SetAttributeValue("name", filename);
            /*
            if( !string.IsNullOrEmpty(SHA1Hash)) {
                newFile.SetAttributeValue("hash", SHA1Hash);
                newFile.SetAttributeValue("hashalg", "SHA1");
            }*/
            _parentElement.Add( newFile);
        }

        public void RemoveFile(string filename) {
            foreach (var e in Elements.Where(each => each.AttributeValue("name").Equals(filename, StringComparison.CurrentCultureIgnoreCase)).ToArray()) {
                e.Remove();
            }
        }

        public IEnumerable<KeyValuePair<string, string>> Files {
            get { return Elements.Select(each => new KeyValuePair<string,string>(each.AttributeValue("name"), each.AttributeValue("hash"))); }
        }
    }

    public class AssemblyIdentity : ManifestElement {
        private const string AssemblyIdentityTag = "{urn:schemas-microsoft-com:asm.v1}assemblyIdentity";
        private const string TypeAttribute = "type";
        private const string LanguageAttribute = "language";
        private const string NameAttribute = "name";
        private const string VersionAttribute = "version";
        private const string ProcessorArchitectureAttribute = "processorArchitecture";
        private const string PublicKeyTokenAttribute = "publicKeyToken";

        private const string BindingRedirectTag = "{urn:schemas-microsoft-com:asm.v1}bindingRedirect";

        private const string OldVersionAttribute = "oldVersion";
        private const string NewVersionAttribute = "newVersion";


        public AssemblyIdentity(XElement parentElement)
            : base(parentElement) {
        }

        protected override IEnumerable<XElement> Elements {
            get { return _parentElement.Children(AssemblyIdentityTag); }
        }

        internal bool IsActive {
            get { return Active; }
        }

        protected override bool Active {
            set {
                if (value) {
                    if (!Active) {
                        var noinherit = _parentElement.Elements().FirstOrDefault(each => each.Name == NoInherit.NoInheritTag);
                        if (noinherit != null) {
                            noinherit.AddAfterSelf(new XElement(AssemblyIdentityTag));
                        } else {
                            _parentElement.AddFirst(new XElement(AssemblyIdentityTag));
                        }
                        Validate();
                    }
                } else {
                    while (Elements.Any()) {
                        Elements.Remove();
                    }
                }
            }
        }

        private XElement AssemblyIdentityElement {
            get {
                Active = true;
                return _parentElement.Children(AssemblyIdentityTag).FirstOrDefault();
            }
        }

        private XElement BindingRedirectElement {
            get {
                if( Active ) {
                    return _parentElement.Children(BindingRedirectTag).FirstOrDefault();
                }
                return null;
            }
        }


        public string Name {
            get { return AssemblyIdentityElement.AttributeValue(NameAttribute); }
            set { AssemblyIdentityElement.SetAttributeValue(NameAttribute, value); }
        }

        public Architecture Architecture {
            get { return AssemblyIdentityElement.AttributeValue(ProcessorArchitectureAttribute); }
            set { AssemblyIdentityElement.SetAttributeValue(ProcessorArchitectureAttribute, value == Architecture.Any ? "*" : value.ToString() ); }
        }

        public FourPartVersion Version {
            get { return AssemblyIdentityElement.AttributeValue(VersionAttribute); }
            set { AssemblyIdentityElement.SetAttributeValue(VersionAttribute, value == 0L ? null : (string)value); }
        }

        public string PublicKeyToken {
            get { return AssemblyIdentityElement.AttributeValue(PublicKeyTokenAttribute); }
            set { AssemblyIdentityElement.SetAttributeValue(PublicKeyTokenAttribute, value); }
        }

        public string Language {
            get { return AssemblyIdentityElement.AttributeValue(LanguageAttribute); }
            set {
                if (string.IsNullOrEmpty(value) || value == "*") {
                    AssemblyIdentityElement.SetAttributeValue(LanguageAttribute, null);
                    return;
                }
                
                AssemblyIdentityElement.SetAttributeValue(LanguageAttribute, value);
            }
        }

        public BindingRedirect BindingRedirect {
            get {
                var bindingRedirectElement = BindingRedirectElement;
                if( bindingRedirectElement == null ) {
                    return null;
                }
                
                FourPartVersion target = bindingRedirectElement.AttributeValue(NewVersionAttribute);
                if( target == 0L ) {
                    // invalid.
                    return null;
                }

                var rangeText = bindingRedirectElement.AttributeValue(OldVersionAttribute);
                if( string.IsNullOrEmpty(rangeText)) {
                    // invalid
                    return null;
                }

                var range = rangeText.Split('-');
                FourPartVersion from = range[0];
                FourPartVersion to = range.Length > 1 ? (FourPartVersion)range[1] : from;

                if( to == 0L) {
                    // invalid
                    return null;
                }

                return new BindingRedirect {
                    Low = from,
                    High = to,
                    Target = target
                };
            }

            set {
                if (!Active) {
                    return;
                }

                if (value == null || value.High == 0L || value.Target == 0L) {
                    // bad redirect. remove and return
                    while( BindingRedirectElement != null ) {
                        BindingRedirectElement.Remove();
                    }
                    return;
                }

                var bindingRedirectElement = BindingRedirectElement;
                if( bindingRedirectElement == null ) {
                    bindingRedirectElement = new XElement(BindingRedirectTag);
                    _parentElement.Add(bindingRedirectElement);
                }
                bindingRedirectElement.SetAttributeValue(OldVersionAttribute, value.VersionRange );
                bindingRedirectElement.SetAttributeValue(NewVersionAttribute, value.Target.ToString());
            }
        }

        public AssemblyType AssemblyType {
            get {
                switch (AssemblyIdentityElement.AttributeValue(TypeAttribute)) {
                    case "win32-policy":
                        return AssemblyType.win32policy;
                }
                return AssemblyType.win32;
            }

            set {
                switch(value) {
                    case AssemblyType.win32:
                        AssemblyIdentityElement.SetAttributeValue(TypeAttribute, "win32");
                        break;
                    case AssemblyType.win32policy:
                        AssemblyIdentityElement.SetAttributeValue(TypeAttribute, "win32-policy");
                        break;
                }
            }
        }

        protected override void Validate() {
            RemoveExcessElements();

            if (!Active) {
                return;
            }

            // this must absolutely be the first element following the noiherit. (or first otherwise)
            var element = _parentElement.Children(AssemblyIdentityTag).FirstOrDefault();
            if (_parentElement.Elements().SkipWhile(each => each.Name == NoInherit.NoInheritTag).FirstOrDefault() != element) {
                element.Remove();
                var noinherit = _parentElement.Elements().FirstOrDefault(each => each.Name == NoInherit.NoInheritTag);
                if (noinherit != null) {
                    noinherit.AddAfterSelf(element);
                } else {
                    _parentElement.AddFirst(element);
                }
            }
        }
    }

    public class NoInherit : ManifestElement {
        internal const string NoInheritTag = "{urn:schemas-microsoft-com:asm.v1}noInherit";

        public NoInherit(XElement parentElement)
            : base(parentElement) {
        }

        protected override void Validate() {
            RemoveExcessElements();

            if (!Active) {
                return;
            }

            // this must absolutely be the first element 
            var element = _parentElement.Children(NoInheritTag).FirstOrDefault();
            if (_parentElement.Elements().FirstOrDefault() != element) {
                element.Remove();
                _parentElement.AddFirst(element);
            }
        }

        protected override IEnumerable<XElement> Elements {
            get { return _parentElement.Children(NoInheritTag); }
        }

        public bool Value {
            get {
                if (!Active) {
                    return false;
                }
                return (Elements.FirstOrDefault().Value ?? string.Empty).Trim().IsTrue();
            }
            set {
                if (value) {
                    Active = true;
                    Elements.FirstOrDefault().Value = "true";
                }
                Active = false;
            }
        }

        protected override bool Active {
            set {
                if (value) {
                    if (!Active) {
                        _parentElement.AddFirst(new XElement(NoInheritTag));
                        Validate();
                    }
                } else {
                    while (Elements.Any()) {
                        Elements.Remove();
                    }
                }
            }
        }
    }

    public class Compatibility : ManifestElement {
        private const string CompatibilityTag = "{urn:schemas-microsoft-com:compatibility.v1}compatibility";
        private const string ApplicationTag = "{urn:schemas-microsoft-com:compatibility.v1}application";
        private const string SupportedOSTag = "{urn:schemas-microsoft-com:compatibility.v1}supportedOS";

        private const string WinVistaCompatabilityId = "{e2011457-1546-43c5-a5fe-008deee3d3f0}";
        private const string Win7CompatabilityId = " {35138b9a-5d96-4fbd-8e2d-a2440225f93a}";
        private const string Win8CompatabilityId = "{4a2f28e3-53b9-4441-ba9c-d69d4a4a6e38}";

        public Compatibility(XElement parentElement)
            : base(parentElement) {
        }

        private IEnumerable<XElement> SupportedOsElements {
            get { return _parentElement.Children(CompatibilityTag, ApplicationTag, SupportedOSTag); }
        }

        protected override void Validate() {
            RemoveExcessElements();
        }

        public bool WinVistaCompatibile {
            get {
                if (!Active) {
                    return false;
                }
                return SupportedOsElements.Any(each => each.AttributeValue("Id") == WinVistaCompatabilityId);
            }
            set {
                if (value) {
                    if (!WinVistaCompatibile) {
                        var vista = new XElement(SupportedOSTag);
                        vista.SetAttributeValue("Id", WinVistaCompatabilityId);
                        _parentElement.AddOrGet(CompatibilityTag).AddOrGet(ApplicationTag).Add(vista);
                    }
                } else {
                    SupportedOsElements.Where(each => each.AttributeValue("Id") == WinVistaCompatabilityId).Remove();
                    if (!SupportedOsElements.Any()) {
                        Active = false;
                    }
                }
            }
        }

        public bool Win7Compatibile {
            get {
                if (!Active) {
                    return false;
                }
                return SupportedOsElements.Any(each => each.AttributeValue("Id") == Win7CompatabilityId);
            }
            set {
                if (value) {
                    if (!Win7Compatibile) {
                        var w7 = new XElement(SupportedOSTag);
                        w7.SetAttributeValue("Id", Win7CompatabilityId);
                        _parentElement.AddOrGet(CompatibilityTag).AddOrGet(ApplicationTag).Add(w7);
                    }
                } else {
                    SupportedOsElements.Where(each => each.AttributeValue("Id") == Win7CompatabilityId).Remove();
                    if (!SupportedOsElements.Any()) {
                        Active = false;
                    }
                }
            }
        }

        public bool Win8Compatibile {
            get {
                if (!Active) {
                    return false;
                }
                return SupportedOsElements.Any(each => each.AttributeValue("Id") == Win8CompatabilityId);
            }
            set {
                if (value) {
                    if (!Win8Compatibile) {
                        var w8 = new XElement(SupportedOSTag);
                        w8.SetAttributeValue("Id", Win8CompatabilityId);
                        _parentElement.AddOrGet(CompatibilityTag).AddOrGet(ApplicationTag).Add(w8);
                    }
                } else {
                    SupportedOsElements.Where(each => each.AttributeValue("Id") == Win8CompatabilityId).Remove();
                    if (!SupportedOsElements.Any()) {
                        Active = false;
                    }
                }
            }
        }

        protected override bool Active {
            set {
                if (value) {
                    if (!Active) {
                        _parentElement.Add(new XElement(CompatibilityTag));
                        Validate();
                    }
                } else {
                    while (Elements.Any()) {
                        Elements.Remove();
                    }
                }
            }
        }

        protected override IEnumerable<XElement> Elements {
            get { return _parentElement.Children(CompatibilityTag); }
        }
    }

    public class NativeManifest {
        private const string DefaultManifestXml =
            @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<assembly xmlns=""urn:schemas-microsoft-com:asm.v1"" manifestVersion=""1.0"">
</assembly>";

        private readonly XDocument _assembly;
        private readonly TrustInfo _trustInfo;
        private readonly AsmV3Application _asmV3Application;
        private readonly DependentAssemblies _dependentAssemblies;
        private readonly NoInherit _noInherit;
        private readonly AssemblyIdentity _assemblyIdentity;
        private readonly Compatibility _compatibility;
        private readonly AssemblyFile _files;

        public bool Modified { get; set; }

        public NativeManifest(string manifestText) {
            if (string.IsNullOrEmpty(manifestText)) {
                manifestText = DefaultManifestXml;
            }
            _assembly = XDocument.Parse(manifestText);

            _noInherit = new NoInherit(_assembly.Root);
            _assemblyIdentity = new AssemblyIdentity(_assembly.Root);
            _trustInfo = new TrustInfo(_assembly.Root);
            _asmV3Application = new AsmV3Application(_assembly.Root);
            _dependentAssemblies = new DependentAssemblies(_assembly.Root);
            _compatibility = new Compatibility(_assembly.Root);
            _files = new AssemblyFile(_assembly.Root);
            Modified = false;
        }

        public override string ToString() {
            using (var memoryStream = new MemoryStream()) {
                using (
                    var xw = XmlWriter.Create(memoryStream,
                        new XmlWriterSettings
                        {ConformanceLevel = ConformanceLevel.Document, Encoding = new UTF8Encoding(false), OmitXmlDeclaration = false, Indent = true})) {
                    _assembly.WriteTo(xw);
                }
                return Encoding.UTF8.GetString(memoryStream.GetBuffer());
            }
        }

        public ExecutionLevel RequestedExecutionLevel {
            get { return _trustInfo.Level; }
            set {
                Modified = true;
                _trustInfo.Level = value;
            }
        }

        public bool UiAccess {
            get { return _trustInfo.UiAccess; }
            set {
                Modified = true;
                _trustInfo.UiAccess = value;
            }
        }

        public bool NoInherit {
            get { return _noInherit.Value; }
            set {
                Modified = true;
                _noInherit.Value = value;
            }
        }

        public bool DpiAware {
            get { return _asmV3Application.DpiAware; }
            set {
                Modified = true;
                _asmV3Application.DpiAware = value;
            }
        }

        public bool Win8Compatible {
            get { return _compatibility.Win8Compatibile; }
            set {
                Modified = true;
                _compatibility.Win8Compatibile = value;
            }
        }

        public bool VistaCompatible {
            get { return _compatibility.WinVistaCompatibile; }
            set {
                Modified = true;
                _compatibility.WinVistaCompatibile = value;
            }
        }

        public bool Win7Compatible {
            get { return _compatibility.Win7Compatibile; }
            set {
                Modified = true;
                _compatibility.Win7Compatibile = value;
            }
        }

        public IEnumerable<AssemblyReference> Dependencies {
            get { return _dependentAssemblies.Dependencies; }
        }

        public void AddDependency(string name, FourPartVersion version, Architecture arch, string publicKeyToken, string language = "*", AssemblyType assemblyType = AssemblyType.win32, BindingRedirect redirect = null) {
            Modified = true;
            _dependentAssemblies.AddDependency(name, version, arch, publicKeyToken, language, assemblyType, redirect);
        }

        public void AddDependency(AssemblyReference assemblyReference) {
            AddDependency(assemblyReference.Name, assemblyReference.Version, assemblyReference.Architecture, assemblyReference.PublicKeyToken,
                assemblyReference.Language, assemblyReference.AssemblyType, assemblyReference.BindingRedirect);
        }

        public void RemoveDependency(AssemblyReference assemblyReference) {
            Modified = true;
            _dependentAssemblies.RemoveDependency(assemblyReference.Name, assemblyReference.Version, assemblyReference.Architecture,
                assemblyReference.PublicKeyToken, assemblyReference.Language);
        }

        public void RemoveDependency(IEnumerable<AssemblyReference> dependencies) {
            if (!dependencies.IsNullOrEmpty()) {
                foreach (var dependency in dependencies.ToArray()) {
                    RemoveDependency(dependency);
                }
            }
        }

        public string AssemblyName {
            get { return _assemblyIdentity.IsActive ? _assemblyIdentity.Name : null; }
            set {
                Modified = true;
                _assemblyIdentity.Name = value;
            }
        }

        public FourPartVersion AssemblyVersion {
            get { return _assemblyIdentity.IsActive ? _assemblyIdentity.Version : 0; }
            set {
                Modified = true;
                _assemblyIdentity.Version = value;
            }
        }

        public Architecture AssemblyArchitecture {
            get { return _assemblyIdentity.IsActive ? _assemblyIdentity.Architecture : Architecture.Unknown; }
            set {
                Modified = true;
                _assemblyIdentity.Architecture = value;
            }
        }

        public string AssemblyPublicKeyToken {
            get { return _assemblyIdentity.IsActive ? _assemblyIdentity.PublicKeyToken : null; }
            set {
                Modified = true;
                _assemblyIdentity.PublicKeyToken = value;
            }
        }

        public string AssemblyLanguage {
            get { return _assemblyIdentity.IsActive ? _assemblyIdentity.Language : null; }
            set {
                Modified = true;
                _assemblyIdentity.Language = value;
            }
        }

        public AssemblyType AssemblyType {
            get {
                if (_assemblyIdentity.IsActive) {
                    return _assemblyIdentity.AssemblyType;
                }
                return AssemblyType.win32;
            }

            set { _assemblyIdentity.AssemblyType = value; }
        }
        

        public IEnumerable<KeyValuePair<string ,string>> AssemblyFiles {
            get { return _files.Files; }
        }

        public void AddFile(string filename, string SHA1Hash=null) {
            _files.AddFile(filename, SHA1Hash);
        }

        public void RemoveFile(string filename) {
            _files.RemoveFile(filename);
        }
    }

}