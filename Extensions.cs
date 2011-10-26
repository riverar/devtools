namespace CoApp.Autopackage {
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Xml;
    using System.Xml.Linq;
    using System.Xml.Serialization;
    using Toolkit.Configuration;
    using Toolkit.Engine.Model;
    using Toolkit.Extensions;
    using Toolkit.Network;
    using Toolkit.Scripting.Languages.PropertySheet;
    using Toolkit.Text.Sgml;

    public static class Extensions {
        public static dynamic GetProperty(this IEnumerable<Rule> rules, string rulename, string propertyName) {
            return (from rule in rules where rule.Name == rulename && rule.HasProperty(propertyName) select rule[propertyName]).FirstOrDefault();
        }

        public static dynamic GetProperty(this IEnumerable<Rule> rules, string propertyName) {
            return (from rule in rules where rule.HasProperty(propertyName) select rule[propertyName]).FirstOrDefault();
        }

        public static string GetPropertyValue(this IEnumerable<Rule> rules, string propertyName) {
            return (from rule in rules where rule.HasProperty(propertyName) select rule[propertyName].Value).FirstOrDefault();
        }

        public static IEnumerable<string> GetPropertyValues(this IEnumerable<Rule> rules, string propertyName) {
            return (rules.Where(rule => rule.HasProperty(propertyName)).SelectMany(rule => rule[propertyName].Values));
        }


        public static IEnumerable<Rule> GetRulesByName(this IEnumerable<Rule> rules, string rulename) {
            return rules.Where(each => each.Name == rulename).ToArray();
        }

        public static IEnumerable<Rule> GetRulesById(this IEnumerable<Rule> rules, string ruleId) {
            return rules.Where(each => each.Id == ruleId).ToArray();
        }

        public static IEnumerable<Rule> GetRulesByClass(this IEnumerable<Rule> rules, string classId) {
            return rules.Where(each => each.Class == classId).ToArray();
        }

        public static IEnumerable<Rule> GetRulesByParameter(this IEnumerable<Rule> rules, string parameter) {
            return rules.Where(each => each.Parameter == parameter).ToArray();
        }

        public static IEnumerable<FileEntry> GetMinimalPaths(this IEnumerable<FileEntry> paths) {
            if (paths.Count() < 2) {
                return paths.Select(each => new FileEntry (each.SourcePath, Path.GetFileName(each.DestinationPath)));
            }

            // horribly inefficient, but I'm too lazy to think this thru clearly right now
            var squished = paths.Select(each => each.DestinationPath + "?" + each.SourcePath);
            squished = squished.GetMinimalPaths();
            return squished.Select(
                each => {
                    var pair = each.Split('?');
                    return new FileEntry (pair[1], pair[0]);
                });
        }

        public static void With<T>(this T item, Action<T> action) {
            action(item);
        }

        public static string ComponentId(this Guid guid) {
            return "component_" + guid.ToString().MakeSafeDirectoryId();
        }

        public static string ManifestId(this Guid guid) {
            return "manifest_" + guid.ToString().MakeSafeDirectoryId();
        }

        

        public static string LiteralOrFileText(this string textOrFile) {
            if( !string.IsNullOrEmpty(textOrFile) ) {
                if( File.Exists(textOrFile)) {
                    return File.ReadAllText(textOrFile);
                }
            }
            return textOrFile;
        }

        public static string GetDescription(this LicenseId value) {
            var descriptionAttribute = (value.GetType().GetField(value.ToString()).GetCustomAttributes(typeof(DescriptionAttribute), false) as DescriptionAttribute[]).First();
            return descriptionAttribute == null ? value.ToString() : descriptionAttribute.Description;
        }

        public static Uri GetUrl(this LicenseId value) {
            var locationAttribute = (value.GetType().GetField(value.ToString()).GetCustomAttributes(typeof(LocationAttribute), false) as LocationAttribute[]).First();
            return locationAttribute == null ? null : locationAttribute.Url.ToUri();
        }

        public static string GetText(this LicenseId value) {
            var locationAttribute = (value.GetType().GetField(value.ToString()).GetCustomAttributes(typeof(LocationAttribute), false) as LocationAttribute[]).First();
            var uri = locationAttribute == null ? null : locationAttribute.Url.ToUri();
            if( uri != null ) {
                var text = RegistryView.ApplicationUser["Licenses", uri.AbsoluteUri].StringValue;
                if( !string.IsNullOrEmpty(text)) {
                    return text;
                }
                var localFile = Path.GetTempFileName() + "-license-txt".MarkFileTemporary();
                if(uri.IsFile) {
                    localFile = uri.AbsoluteUri.CanonicalizePath();
                    if( !File.Exists(localFile)) {
                        AutopackageMessages.Invoke.Warning(MessageCode.BadLicenseLocation, null, "Unable to retrieve the license for {0} from {1}", value,
                            localFile);
                        return null;
                    }
                } else {
                    var rf = RemoteFile.GetRemoteFile(uri, localFile);
                    rf.Get().Wait();
                    if( !File.Exists(localFile)) {
                        AutopackageMessages.Invoke.Warning(MessageCode.BadLicenseLocation, null, "Unable to retrieve the license for {0} from {1}", value,
                            uri.AbsolutePath);
                        return null;
                    }
                }

                
                text = File.ReadAllText(localFile);

                if( text.IndexOf("content clear-block") > -1) {
                    // this is off the opensource.org site
                    XDocument doc;
                    var reader = new SgmlReader();
                    reader.DocType = "HTML";
                    reader.IgnoreDtd = true;
                    using (reader.InputStream = File.OpenText(localFile)) {
                        doc = XDocument.Load(reader);
                    }
                    var div = doc.Elements().Where(each => each.Name == "div" && each.Attributes("class").FirstOrDefault().Value == "content clear-block").FirstOrDefault();
                    if( div != null && div.HasElements) {
                        text = div.Elements().Aggregate("", (current, n) => current + n.ToString());
                    }
                    //if( nl.Count > 0 ) {
                    //   text = nl[0].Cast<XmlNode>().Aggregate("", (current, n) => current + n.OuterXml);
                    //}
                }

                if( string.IsNullOrEmpty(text)) {
                    return null;
                }

                RegistryView.ApplicationUser["Licenses", uri.AbsoluteUri].StringValue = text;
                return text;
            }

            return null;
        }
    }
}