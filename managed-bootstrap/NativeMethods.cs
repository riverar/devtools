using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoApp.Bootstrapper {
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;
    using Microsoft.Win32.SafeHandles;

    internal static class ReparsePointError {
        /// <summary>
        ///   The file or directory is not a reparse point.
        /// </summary>
        internal const int NotAReparsePoint = 4390;

        /// <summary>
        ///   The reparse point attribute cannot be set because it conflicts with an existing attribute.
        /// </summary>
        internal const int ReparseAttributeConflict = 4391;

        /// <summary>
        ///   The data present in the reparse point buffer is invalid.
        /// </summary>
        internal const int InvalidReparseData = 4392;

        /// <summary>
        ///   The tag present in the reparse point buffer is invalid.
        /// </summary>
        internal const int ReparseTagInvalid = 4393;

        /// <summary>
        ///   There is a mismatch between the tag specified in the request and the tag present in the reparse point.
        /// </summary>
        internal const int ReparseTagMismatch = 4394;
    }

    public enum ControlCodes : uint {
        SetReparsePoint = 0x000900A4, // Command to set the reparse point data block.
        GetReparsePoint = 0x000900A8, // Command to get the reparse point data block.
        DeleteReparsePoint = 0x000900AC // Command to delete the reparse point data base.
    }

    public enum KnownFolder {
        Desktop = 0x0000, // <desktop>
        CSIDL_INTERNET = 0x0001, // Internet Explorer (icon on desktop)
        Programs = 0x0002, // Start Menu\Programs
        CSIDL_CONTROLS = 0x0003, // My Computer\Control Panel
        CSIDL_PRINTERS = 0x0004, // My Computer\Printers
        Personal = 0x0005, // My Documents
        Favorites = 0x0006, // <user name>\Favorites
        Startup = 0x0007, // Start Menu\Programs\Startup
        Recent = 0x0008, // <user name>\Recent
        SendTo = 0x0009, // <user name>\SendTo
        CSIDL_BITBUCKET = 0x000a, // <desktop>\Recycle Bin
        StartMenu = 0x000b, // <user name>\Start Menu
        MyDocuments = 0x000c, // logical "My Documents" desktop icon
        MyMusic = 0x000d, // "My Music" folder
        MyVideo = 0x000e, // "My Videos" folder
        DesktopDirectory = 0x0010, // <user name>\Desktop
        MyComputer = 0x0011, // My Computer
        NetworkShortcuts = 0x0012, // Network Neighborhood (My Network Places)
        CSIDL_NETHOOD = 0x0013, // <user name>\nethood
        Fonts = 0x0014, // windows\fonts
        Templates = 0x0015,
        CommonStartMenu = 0x0016, // All Users\Start Menu
        CommonPrograms = 0X0017, // All Users\Start Menu\Programs
        CommonStartup = 0x0018, // All Users\Startup
        CommonDesktop = 0x0019, // All Users\Desktop
        ApplicationData = 0x001a, // <user name>\Application Data
        CSIDL_PRINTHOOD = 0x001b, // <user name>\PrintHood

        LocalApplicationData = 0x001c, // <user name>\Local Settings\Applicaiton Data (non roaming)

        CSIDL_ALTSTARTUP = 0x001d, // non localized startup
        CSIDL_COMMON_ALTSTARTUP = 0x001e, // non localized common startup
        CSIDL_COMMON_FAVORITES = 0x001f,

        InternetCache = 0x0020,
        Cookies = 0x0021,
        History = 0x0022,
        CommonApplicationData = 0x0023, // All Users\Application Data AKA \ProgramData
        Windows = 0x0024, // GetWindowsDirectory()
        System = 0x0025, // GetSystemDirectory()
        ProgramFiles = 0x0026, // C:\Program Files
        MyPictures = 0x0027, // C:\Program Files\My Pictures

        UserProfile = 0x0028, // USERPROFILE
        SystemX86 = 0x0029, // x86 system directory on RISC
        ProgramFilesX86 = 0x002a, // x86 C:\Program Files on RISC

        CommonProgramFiles = 0x002b, // C:\Program Files\Common

        CommonProgramFilesX86 = 0x002c, // x86 Program Files\Common on RISC
        CommonTemplates = 0x002d, // All Users\Templates

        CommonDocuments = 0x002e, // All Users\Documents
        CommonAdminTools = 0x002f, // All Users\Start Menu\Programs\Administrative Tools
        AdminTools = 0x0030, // <user name>\Start Menu\Programs\Administrative Tools

        CSIDL_CONNECTIONS = 0x0031, // Network and Dial-up Connections
        CommonMusic = 0x0035, // All Users\My Music
        CommonPictures = 0x0036, // All Users\My Pictures
        CommonVideos = 0x0037, // All Users\My Video

        CDBurning = 0x003b // USERPROFILE\Local Settings\Application Data\Microsoft\CD Burning
    }


    [Flags]
    public enum NativeFileAttributesAndFlags : uint {
        Readonly = 0x00000001,
        Hidden = 0x00000002,
        System = 0x00000004,
        Directory = 0x00000010,
        Archive = 0x00000020,
        Device = 0x00000040,
        Normal = 0x00000080,
        Temporary = 0x00000100,
        SparseFile = 0x00000200,
        ReparsePoint = 0x00000400,
        Compressed = 0x00000800,
        Offline = 0x00001000,
        NotContentIndexed = 0x00002000,
        Encrypted = 0x00004000,
        Write_Through = 0x80000000,
        Overlapped = 0x40000000,
        NoBuffering = 0x20000000,
        RandomAccess = 0x10000000,
        SequentialScan = 0x08000000,
        DeleteOnClose = 0x04000000,
        BackupSemantics = 0x02000000,
        PosixSemantics = 0x01000000,
        OpenReparsePoint = 0x00200000,
        OpenNoRecall = 0x00100000,
        FirstPipeInstance = 0x00080000
    }

    internal class NativeMethods {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern uint SearchPath(string lpPath, string lpFileName, string lpExtension, int nBufferLength,
            [MarshalAs(UnmanagedType.LPTStr)] StringBuilder lpBuffer, out IntPtr lpFilePart);

        [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false, CharSet = CharSet.Unicode)]
        internal static extern WinVerifyTrustResult WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid pgActionID, WinTrustData pWVTData);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        internal static extern uint MsiOpenDatabase(string szDatabasePath, IntPtr uiOpenMode, out int hDatabase);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        internal static extern uint MsiDatabaseOpenView(int hDatabase, string szQuery, out int hView);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        internal static extern uint MsiViewExecute(int hView, int hRecord);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        internal static extern uint MsiViewFetch(int hView, out int hRecord);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        internal static extern uint MsiRecordDataSize(int hRecord, uint iField);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        internal static extern uint MsiRecordReadStream(int hRecord, uint iField, byte[] szDataBuf, ref uint cbDataBuf);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        internal static extern uint MsiCloseHandle(int hAny);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        internal static extern uint MsiInstallProduct(string szPackagePath, string szCommandLine);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        internal static extern uint MsiSetInternalUI(uint dwUILevel, IntPtr phWnd);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        internal static extern SingleStep.NativeExternalUIHandler MsiSetExternalUI(
            [MarshalAs(UnmanagedType.FunctionPtr)] SingleStep.NativeExternalUIHandler puiHandler, uint dwMessageFilter, IntPtr pvContext);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        internal static extern uint MsiOpenPackageEx(string szPackagePath, uint dwOptions, out int hProduct);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        internal static extern uint MsiGetProperty(int hInstall, string szName, StringBuilder szValueBuf, ref uint cchValueBuf);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern int LoadString(SafeModuleHandle hInstance, uint uID, StringBuilder lpBuffer, int nBufferMax);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern SafeModuleHandle LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr FindResource(SafeModuleHandle moduleHandle, int resourceId, string resourceType);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr LoadResource(SafeModuleHandle moduleHandle, IntPtr hResInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int SizeofResource(SafeModuleHandle moduleHandle, IntPtr hResInfo);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool CloseHandle(IntPtr handle);

        [DllImport("shell32.dll")]
        public static extern bool SHGetSpecialFolderPath(IntPtr hwndOwner, [Out] StringBuilder pszPath, KnownFolder nFolder, bool create = false);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool DeviceIoControl(SafeFileHandle hDevice, ControlCodes dwIoControlCode, IntPtr InBuffer, int nInBufferSize, IntPtr OutBuffer,
            int nOutBufferSize, out int pBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern SafeFileHandle CreateFile(string name, NativeFileAccess access, FileShare share, IntPtr security, FileMode mode,
            NativeFileAttributesAndFlags flags, IntPtr template);


        [DllImport("kernel32.dll", EntryPoint = "CreateSymbolicLinkW", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static extern int CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, int dwFlags);


        public const int LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
    }

    public enum IoReparseTag : uint {
        MountPoint = 0xA0000003, //   Reparse point tag used to identify mount points and junction points.
        Symlink = 0xA000000C //   Reparse point tag used to identify symlinks
    }

    [Flags]
    public enum NativeFileAccess : uint {
        GenericRead = 0x80000000,
        GenericWrite = 0x40000000,
        GenericExecute = 0x20000000,
        GenericAll = 0x10000000,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ReparseData {
        /// <summary>
        ///   Reparse point tag.
        /// </summary>
        public IoReparseTag ReparseTag;

        /// <summary>
        ///   Size, in bytes, of the data after the Reserved member. This can be calculated by:
        ///   (4 * sizeof(ushort)) + SubstituteNameLength + PrintNameLength + 
        ///   (namesAreNullTerminated ? 2 * sizeof(char) : 0);
        /// </summary>
        public ushort ReparseDataLength;

        /// <summary>
        ///   Reserved. do not use.
        /// </summary>
        public ushort Reserved;

        /// <summary>
        ///   Offset, in bytes, of the substitute name string in the PathBuffer array.
        /// </summary>
        public ushort SubstituteNameOffset;

        /// <summary>
        ///   Length, in bytes, of the substitute name string. If this string is null-terminated,
        ///   SubstituteNameLength does not include space for the null character.
        /// </summary>
        public ushort SubstituteNameLength;

        /// <summary>
        ///   Offset, in bytes, of the print name string in the PathBuffer array.
        /// </summary>
        public ushort PrintNameOffset;

        /// <summary>
        ///   Length, in bytes, of the print name string. If this string is null-terminated,
        ///   PrintNameLength does not include space for the null character.
        /// </summary>
        public ushort PrintNameLength;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x3FF0)]
        public byte[] PathBuffer;
    }

    public static class Symlink {
        public static void MkDirectoryLink(string linkPath, string actualDirectoryPath) {
            if( !Directory.Exists(linkPath) ) {
                if( Environment.OSVersion.Version.Major > 5 ) {
                    NativeMethods.CreateSymbolicLink(linkPath, actualDirectoryPath, 1);
                } else {
                    ReparsePoint.CreateJunction(linkPath, actualDirectoryPath);
                }
            }
        }
       
    }

    /// <summary>
    ///   A low-level interface to mucking with reparse points on NTFS
    /// 
    ///   see: http://msdn.microsoft.com/en-us/library/cc232007(v=prot.10).aspx
    /// </summary>
    /// <remarks>
    /// </remarks>
    public class ReparsePoint {
        /// <summary>
        ///   This prefix indicates to NTFS that the path is to be treated as a non-interpreted
        ///   path in the virtual file system.
        /// </summary>
        private const string NonInterpretedPathPrefix = @"\??\";

        /// <summary>
        /// </summary>
        private static Regex UncPrefixRx = new Regex(@"\\\?\?\\UNC\\");

        /// <summary>
        /// </summary>
        private static Regex DrivePrefixRx = new Regex(@"\\\?\?\\[a-z,A-Z]\:\\");

        /// <summary>
        /// </summary>
        private static Regex VolumePrefixRx = new Regex(@"\\\?\?\\Volume");

        /// <summary>
        /// </summary>
        private ReparseData _reparseDataData;

        /// <summary>
        ///   Prevents a default instance of the <see cref = "ReparsePoint" /> class from being created.
        /// 
        ///   Populates the data from the buffer pointed to by the pointer.
        /// </summary>
        /// <param name = "buffer">The buffer.</param>
        /// <remarks>
        /// </remarks>
        private ReparsePoint(IntPtr buffer) {
            if (buffer == IntPtr.Zero) {
                throw new ArgumentNullException("buffer");
            }

            _reparseDataData = (ReparseData) Marshal.PtrToStructure(buffer, typeof (ReparseData));
        }

        /// <summary>
        ///   Gets a value indicating whether this instance is symlink or junction.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public bool IsSymlinkOrJunction {
            get {
                return (_reparseDataData.ReparseTag == IoReparseTag.MountPoint || _reparseDataData.ReparseTag == IoReparseTag.Symlink) &&
                    !IsMountPoint;
            }
        }

        /// <summary>
        ///   Gets a value indicating whether this instance is relative symlink.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public bool IsRelativeSymlink {
            get { return _reparseDataData.ReparseTag == IoReparseTag.Symlink && (_reparseDataData.PathBuffer[0] & 1) == 1; }
        }

        /// <summary>
        ///   Gets a value indicating whether this instance is mount point.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public bool IsMountPoint {
            get { return VolumePrefixRx.Match(SubstituteName).Success; }
        }

        /// <summary>
        ///   Gets the "print name" of the reparse point
        /// </summary>
        /// <remarks>
        /// </remarks>
        public string PrintName {
            get {
                var extraOffset = _reparseDataData.ReparseTag == IoReparseTag.Symlink ? 4 : 0;
                return _reparseDataData.PrintNameLength > 0
                    ? Encoding.Unicode.GetString(_reparseDataData.PathBuffer, _reparseDataData.PrintNameOffset + extraOffset,
                        _reparseDataData.PrintNameLength)
                    : string.Empty;
            }
        }

        /// <summary>
        ///   Gets the "substitute name" of the reparse point.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public string SubstituteName {
            get {
                var extraOffset = _reparseDataData.ReparseTag == IoReparseTag.Symlink ? 4 : 0;
                return _reparseDataData.SubstituteNameLength > 0
                    ? Encoding.Unicode.GetString(_reparseDataData.PathBuffer, _reparseDataData.SubstituteNameOffset + extraOffset,
                        _reparseDataData.SubstituteNameLength)
                    : string.Empty;
            }
        }

        /// <summary>
        ///   Gets the file handle to the reparse point.
        /// </summary>
        /// <param name = "reparsePoint">The reparse point.</param>
        /// <param name = "accessMode">The access mode.</param>
        /// <returns></returns>
        /// <remarks>
        /// </remarks>
        private static SafeFileHandle GetReparsePointHandle(string reparsePoint, NativeFileAccess accessMode) {
            var reparsePointHandle = NativeMethods.CreateFile(reparsePoint, accessMode, FileShare.Read | FileShare.Write | FileShare.Delete,
                IntPtr.Zero,
                FileMode.Open, NativeFileAttributesAndFlags.BackupSemantics | NativeFileAttributesAndFlags.OpenReparsePoint, IntPtr.Zero);

            if (Marshal.GetLastWin32Error() != 0) {
                throw new IOException("Unable to open reparse point.", Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
            }

            return reparsePointHandle;
        }

        /// <summary>
        ///   Determines whether a given path is a reparse point
        /// </summary>
        /// <param name = "path">The path.</param>
        /// <returns><c>true</c> if [is reparse point] [the specified path]; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// </remarks>
        public static bool IsReparsePoint(string path) {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }

        /// <summary>
        /// Translates paths starting with \??\ to regular paths.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string NormalizePath(string path) {
            if (path.StartsWith(NonInterpretedPathPrefix)) {
                if (UncPrefixRx.Match(path).Success) {
                    path = UncPrefixRx.Replace(path, @"\\");
                }

                if (DrivePrefixRx.Match(path).Success) {
                    path = path.Replace(NonInterpretedPathPrefix, "");
                }
            }
            if (path.EndsWith("\\")) {
                var couldBeFilePath = path.Substring(0, path.Length - 1);
                if (File.Exists(couldBeFilePath)) {
                    path = couldBeFilePath;
                }
            }

            return path;
        }

        /// <summary>
        ///   Gets the actual path of a reparse point
        /// </summary>
        /// <param name = "linkPath">The link path.</param>
        /// <returns></returns>
        /// <remarks>
        /// </remarks>
        public static string GetActualPath(string linkPath) {
            if (!IsReparsePoint(linkPath)) {
                // if it's not a reparse point, return the path given.
                return linkPath;
            }

            var reparsePoint = Open(linkPath);
            var target = NormalizePath(reparsePoint.SubstituteName);

            if (reparsePoint.IsRelativeSymlink) {
                target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(linkPath), target));
            }

            return target;
        }

        /// <summary>
        ///   Opens the specified path as a reparse point.
        /// 
        ///   throws if it's not a reparse point.
        /// </summary>
        /// <param name = "path">The path.</param>
        /// <returns></returns>
        /// <remarks>
        /// </remarks>
        public static ReparsePoint Open(string path) {
            if (!IsReparsePoint(path)) {
                throw new IOException("Path is not reparse point");
            }

            using (var handle = GetReparsePointHandle(path, NativeFileAccess.GenericRead)) {
                if (handle == null) {
                    throw new IOException("Unable to get information about reparse point.");
                }

                var outBufferSize = Marshal.SizeOf(typeof (ReparseData));
                var outBuffer = Marshal.AllocHGlobal(outBufferSize);

                try {
                    int bytesReturned;
                    var result = NativeMethods.DeviceIoControl(handle, ControlCodes.GetReparsePoint, IntPtr.Zero, 0, outBuffer, outBufferSize,
                        out bytesReturned,
                        IntPtr.Zero);

                    if (!result) {
                        var error = Marshal.GetLastWin32Error();
                        if (error == ReparsePointError.NotAReparsePoint) {
                            throw new IOException("Path is not a reparse point.",
                                Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
                        }

                        throw new IOException("Unable to get information about reparse point.",
                            Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
                    }
                    return new ReparsePoint(outBuffer);
                }
                finally {
                    Marshal.FreeHGlobal(outBuffer);
                }
            }
        }

        /// <summary>
        ///   Creates the junction.
        /// </summary>
        /// <param name = "junctionPath">The junction path.</param>
        /// <param name = "targetDirectory">The target directory.</param>
        /// <returns></returns>
        /// <remarks>
        /// </remarks>
        public static ReparsePoint CreateJunction(string junctionPath, string targetDirectory) {
            junctionPath = Path.GetFullPath(junctionPath);
            targetDirectory = Path.GetFullPath(targetDirectory);

            if (!Directory.Exists(targetDirectory)) {
                throw new IOException("Target path does not exist or is not a directory.");
            }

            if (Directory.Exists(junctionPath) || File.Exists(junctionPath)) {
                throw new IOException("Junction path already exists.");
            }

            Directory.CreateDirectory(junctionPath);

            using (var handle = GetReparsePointHandle(junctionPath, NativeFileAccess.GenericWrite)) {
                var substituteName = Encoding.Unicode.GetBytes(NonInterpretedPathPrefix + targetDirectory);
                var printName = Encoding.Unicode.GetBytes(targetDirectory);

                var reparseDataBuffer = new ReparseData {
                    ReparseTag = IoReparseTag.MountPoint,
                    SubstituteNameOffset = 0,
                    SubstituteNameLength = (ushort) substituteName.Length,
                    PrintNameOffset = (ushort) (substituteName.Length + 2),
                    PrintNameLength = (ushort) printName.Length,
                    PathBuffer = new byte[0x3ff0],
                };

                reparseDataBuffer.ReparseDataLength = (ushort) (reparseDataBuffer.PrintNameLength + reparseDataBuffer.PrintNameOffset + 10);

                Array.Copy(substituteName, reparseDataBuffer.PathBuffer, substituteName.Length);
                Array.Copy(printName, 0, reparseDataBuffer.PathBuffer, reparseDataBuffer.PrintNameOffset, printName.Length);

                var inBufferSize = Marshal.SizeOf(reparseDataBuffer);
                var inBuffer = Marshal.AllocHGlobal(inBufferSize);

                try {
                    Marshal.StructureToPtr(reparseDataBuffer, inBuffer, false);

                    int bytesReturned;
                    var result = NativeMethods.DeviceIoControl(handle, ControlCodes.SetReparsePoint, inBuffer,
                        reparseDataBuffer.ReparseDataLength + 8, IntPtr.Zero,
                        0, out bytesReturned, IntPtr.Zero);

                    if (!result) {
                        Directory.Delete(junctionPath);
                        throw new IOException("Unable to create junction point.",
                            Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
                    }

                    return Open(junctionPath);
                }
                finally {
                    Marshal.FreeHGlobal(inBuffer);
                }
            }
        }
    }
}
