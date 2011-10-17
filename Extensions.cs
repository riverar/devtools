namespace CoApp.Autopackage {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml;
    using System.Xml.Serialization;
    using Toolkit.Extensions;
    using Toolkit.Scripting.Languages.PropertySheet;

    public static class Extensions {
        public static dynamic GetProperty(this IEnumerable<Rule> rules, string rulename, string propertyName) {
            return (from rule in rules where rule.Name == rulename && rule.HasProperty(propertyName) select rule[propertyName]).FirstOrDefault();
        }

        public static dynamic GetProperty(this IEnumerable<Rule> rules, string propertyName) {
            return (from rule in rules where rule.HasProperty(propertyName) select rule[propertyName]).FirstOrDefault();
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

        public static string DisposeWhenDone( this string filename ) {
            AutopackageMain.DisposableFilenames.Add(filename);
            return filename;
        }

        public static string InTempFolder( this string filename ) {
            var p = Path.Combine(Path.GetTempPath(), filename); 
            if (File.Exists(p)) {
                p.TryHardToDeleteFile();
            }
            
            AutopackageMain.DisposableFilenames.Add(p);

            return p;
        }
    }
}