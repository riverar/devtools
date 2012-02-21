//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Bootstrapper {
    using System;
    using System.Runtime.InteropServices;

    internal enum WinVerifyTrustResult : uint {
        Success = 0,
        ProviderUnknown = 0x800b0001, // The trust provider is not recognized on this system
        ActionUnknown = 0x800b0002, // The trust provider does not support the specified action
        SubjectFormUnknown = 0x800b0003, // The trust provider does not support the form specified for the subject
        SubjectNotTrusted = 0x800b0004, // The subject failed the specified verification action
        UntrustedRootCert = 0x800B0109 //A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider. 
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal class WinTrustFileInfo {
        private UInt32 StructSize = (UInt32) Marshal.SizeOf(typeof (WinTrustFileInfo));
        private IntPtr FilePath; // required, file name to be verified
        private IntPtr hFile = IntPtr.Zero; // optional, open handle to FilePath
        private IntPtr pgKnownSubject = IntPtr.Zero; // optional, subject type if it is known

        public WinTrustFileInfo(String _filePath) {
            FilePath = Marshal.StringToCoTaskMemAuto(_filePath);
        }

        ~WinTrustFileInfo() {
            Marshal.FreeCoTaskMem(FilePath);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal class WinTrustData {
        private UInt32 StructSize = (UInt32) Marshal.SizeOf(typeof (WinTrustData));
        private IntPtr PolicyCallbackData = IntPtr.Zero;
        private IntPtr SIPClientData = IntPtr.Zero;
        private uint UIChoice = 2;
        private uint RevocationChecks = 0;
        private uint UnionChoice = 1;
        private IntPtr FileInfoPtr;
        private uint StateAction = 0;
        private IntPtr StateData = IntPtr.Zero;
        private String URLReference;
        private uint ProvFlags = 0x00000040; // check revocation chain.
        private uint UIContext = 0;

        // constructor for silent WinTrustDataChoice.File check
        public WinTrustData(String filename) {
            var wtfiData = new WinTrustFileInfo(filename);
            FileInfoPtr = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof (WinTrustFileInfo)));
            Marshal.StructureToPtr(wtfiData, FileInfoPtr, false);
        }

        ~WinTrustData() {
            Marshal.FreeCoTaskMem(FileInfoPtr);
        }
    }
}