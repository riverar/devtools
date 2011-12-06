using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoApp.Autopackage {
    using Toolkit.Exceptions;
    using Toolkit.Utility;

    internal static class Tools {
        internal static bool ShowTools;
        internal static ProcessUtility AssemblyLinker;
        internal static ProcessUtility ManifestTool;
        internal static ProcessUtility MakeCatalog;
        internal static ProcessUtility WixCompiler;
        internal static ProcessUtility WixLinker;

        internal static void LocateCommandlineTools() {
            WixCompiler = new ProcessUtility(ProgramFinder.ProgramFilesAndDotNet.ScanForFile("candle.exe"));
            WixLinker = new ProcessUtility(ProgramFinder.ProgramFilesAndDotNet.ScanForFile("light.exe"));
            ManifestTool = new ProcessUtility(ProgramFinder.ProgramFilesAndDotNet.ScanForFile("mt.exe"));
            MakeCatalog = new ProcessUtility(ProgramFinder.ProgramFilesAndDotNet.ScanForFile("makecat.exe"));
            AssemblyLinker = new ProcessUtility(ProgramFinder.ProgramFilesAndDotNet.ScanForFile("al.exe"));

            if (WixCompiler.Executable == null) {
                throw new ConsoleException("Unable to find 'candle.exe' from WiX 3.5 installation.");
            }

            if (WixLinker.Executable == null) {
                throw new ConsoleException("Unable to find 'light.exe' from WiX 3.5 installation.");
            }

            if (ManifestTool.Executable == null) {
                throw new ConsoleException("Unable to find 'mt.exe' from Windows SDK.");
            }

            if (MakeCatalog.Executable == null) {
                throw new ConsoleException("Unable to find 'makecat.exe' from Windows SDK.");
            }

            if (AssemblyLinker.Executable == null) {
                throw new ConsoleException("Unable to find 'al.exe' from Windows SDK.");
            }

            if (ShowTools) {
                Console.WriteLine("Tools:");
                Console.WriteLine("Wix/Candle.exe : {0}", WixCompiler.Executable);
                Console.WriteLine("Wix/Light.exe : {0}", WixLinker.Executable);
                Console.WriteLine("SDK/mt.exe : {0}", ManifestTool.Executable);
                Console.WriteLine("SDK/makecat.exe : {0}", MakeCatalog.Executable);
                Console.WriteLine("SDK/al.exe : {0}", AssemblyLinker.Executable);
            }
        }
    }
}
