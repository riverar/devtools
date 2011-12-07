//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoApp.Autopackage {
    using System.IO;
    using Toolkit.Extensions;
    using Toolkit.Scripting.Languages.PropertySheet;
    

    public class FileEntry {
        public FileEntry() {
            
        }

        public FileEntry( FileEntry fe ) {
            SourcePath = fe.SourcePath;
            DestinationPath = fe.DestinationPath;
        }

        public FileEntry( string source, string dest ) {
            SourcePath = source;
            DestinationPath = dest;
        }

        public string SourcePath { get; private set; }
        public string DestinationPath { get; private set; }
    }

    public enum TrimPath {
        none,
        minimal,
        all
    }

    public class FileList {
        static FileList() {
            FileLists = new List<FileList>();
        }

        public static List<FileList> FileLists { get; private set; }
        public string Name { get; set; }

        public FileEntry[] FileEntries = new FileEntry[] { };
        private readonly bool _isReady;
        private readonly dynamic _rule;

        private FileList(string name, dynamic rule, IEnumerable<Rule> fileRules) {
            Name = name;
            _isReady = false;
            _rule = rule;
            FileLists.Add(this);
            
            var root = (_rule.root.Value  as string ?? Environment.CurrentDirectory);

            // run thru the list of includes, pick up files.
            var entries = ProcessIncludes(this, _rule, name, "include", fileRules, root);

            // next run thru the list of excludes to remove files that we've got in our list
            entries = ProcessExcludes(entries, this, _rule, name, fileRules, root);

            // trim the paths in our list
            entries = TrimPaths(entries, _rule );

            // add the destination to the front of the paths.
            entries = SetDestinationDirectory(entries, _rule);

            FileEntries = (entries as IEnumerable<FileEntry>).ToArray();

            _isReady = true;
        }

        private static IEnumerable<FileEntry> SetDestinationDirectory(IEnumerable<FileEntry> fileEntries, dynamic rule) {
            var destination = rule.destination.Value as string ?? string.Empty;

            if (!string.IsNullOrEmpty(destination)) {
                return fileEntries.Select(each => new FileEntry( each.SourcePath, Path.Combine(destination, each.DestinationPath)));
            }
            return fileEntries;
        }

        private static IEnumerable<FileEntry> TrimPaths(IEnumerable<FileEntry> fileEntries, dynamic rule) {
            var trimPath = rule.trimPath.Value as string ?? TrimPath.none.ToString();

            var trim = TrimPath.none;

            if (!Enum.TryParse(trimPath, true, out trim)) {
                AutopackageMessages.Invoke.Warning(MessageCode.TrimPathOptionInvalid, rule.trimPath, "trim-path option '{0}' not valid, assuming 'none'", trimPath);
            }

            switch (trim) {
                case TrimPath.all:
                    return fileEntries.Select(each => new FileEntry (each.SourcePath,  Path.GetFileName(each.DestinationPath))).ToList();
                    
                case TrimPath.minimal:
                    return fileEntries.GetMinimalPaths().ToList();
                    
            }
            return fileEntries;
        }

        private static IEnumerable<FileEntry> ProcessExcludes(IEnumerable<FileEntry> fileEntries, FileList thisInstance, dynamic rule, string name, IEnumerable<Rule> fileRules, string root) {
            var excludes = rule.exclude.Values;

            foreach (string exclude in excludes) {
                // first check to see if the exclude is another file list
                if (fileRules.GetRulesByParameter(exclude).Any()) {
                    var excludedList = GetFileList(exclude, fileRules);
                    if (excludedList == null) {
                        AutopackageMessages.Invoke.Error(
                            MessageCode.DependentFileListUnavailable, rule.SourceLocation,
                            "File list '{0}' depends on file list '{1}' which is not availible.", name, exclude);
                        continue;
                    }
                    if (excludedList == thisInstance) {
                        // already complained about circular reference.
                        continue;
                    }

                    // get just the file names
                    var excludedFilePaths = excludedList.FileEntries.Select(each => each.SourcePath);

                    // remove any files in our list that are in the list we're given
                    fileEntries = fileEntries.Where(fileEntry => !excludedFilePaths.Contains(fileEntry.SourcePath));
                    continue;
                }

                // otherwise, see if the the exclude string is a match for anything in the fileset.
                fileEntries = fileEntries.Where(each => !each.SourcePath.NewIsWildcardMatch(exclude, true, root));
            }
            return fileEntries;
        }

        internal static IEnumerable<FileEntry> ProcessIncludes(FileList thisInstance, dynamic rule, string name,IEnumerable<Rule> fileRules, string root) {
            return ProcessIncludes(thisInstance, rule, name, "include", fileRules, root);
        }

        internal static IEnumerable<FileEntry> ProcessIncludes(FileList thisInstance, dynamic rule, string name, string includePropertyName, IEnumerable<Rule> fileRules, string root) {
            var fileEntries = Enumerable.Empty<FileEntry>();
            var includes = rule.include.Values;

            foreach (string include in includes) {
                // first check to see if the include is another file list
                if (fileRules.GetRulesByParameter(include).Any()) {
                    // there is one? Great. Add that to the list of our files 
                    var inheritedList = GetFileList(include, fileRules);

                    if (inheritedList == null) {
                        AutopackageMessages.Invoke.Error(
                            MessageCode.DependentFileListUnavailable, rule.SourceLocation,
                            "File list '{0}' depends on file list '{1}' which is not availible.", name, include);
                        continue;
                    }

                    if (inheritedList == thisInstance) {
                        // already complained about circular reference.
                        continue;
                    }

                    fileEntries = fileEntries.Union(inheritedList.FileEntries.Select(each => new FileEntry(each)));
                    continue;
                }

                // it's not a reference include. lets see if we can pick up some files with it.
                var foundFiles = root.FindFilesSmarter(include).ToArray();

                if (!foundFiles.Any()) {
                    AutopackageMessages.Invoke.Warning(
                        MessageCode.IncludeFileReferenceMatchesZeroFiles, rule.include.SourceLocation,
                        "File include reference '{0}' matches zero files in path '{1}'", include, root);
                }
                fileEntries = fileEntries.Union(foundFiles.Select(each => new FileEntry (each, root.GetSubPath(each))));
            }

            return fileEntries;
        }

        public static FileList GetFileList(string name, IEnumerable<Rule> fileRules) {
            var result = FileLists.Where(each => each.Name == name).FirstOrDefault();
            if( result != null && !result._isReady) {
                // circular reference
                AutopackageMessages.Invoke.Error(MessageCode.CircularFileReference, result._rule.SourceLocation, "Circular file reference. '{0}' has a file include reference that includes itself.", name);
                return result;
            }

            var rules = fileRules.GetRulesByParameter(name);
            switch( rules.Count()) {
                case 0: 
                    AutopackageMessages.Invoke.Error(MessageCode.UnknownFileList, null, "Unknown file list '{0}'.", name);
                    return null;

                case 1: 
                    // just right
                    break;

                default:
                    if (!AutopackageMain.Override) {
                        AutopackageMessages.Invoke.Error(
                            MessageCode.MultipleFileLists, fileRules.First().SourceLocation, "Multiple file lists with name '{0}'.", name);
                        return null;
                    }

                    AutopackageMessages.Invoke.Warning(MessageCode.MultipleFileLists, fileRules.First().SourceLocation, "Multiple file lists with name '{0}', using last specified one.", name);
                    break;
            }

            return new FileList(name, rules.First(), fileRules);
        }
    }
}
