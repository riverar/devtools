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
    using Microsoft.Win32.SafeHandles;
    using System.Runtime.InteropServices;

    /// <summary>
    ///  SafeHandleBase implements ReleaseHandle method for all our Safe Handle classes.
    /// 
    /// The purpose of the safe handle class is to get away from having IntPtr objects for handles 
    /// coming back from Kernel APIs, and instead provide a type-safe wrapper that prohibits the 
    /// accidental use of one handle type where another should be.
    /// 
    /// We create a common base class so that the release semantics are implemented the same.
    /// </summary>
     public class AutoSafeHandle : SafeHandleZeroOrMinusOneIsInvalid {
        protected AutoSafeHandle() : base (true) {}

        protected AutoSafeHandle(IntPtr handle) : base(true) {
            SetHandle(handle);
        }

        /// <summary>
        /// When overridden in a derived class, executes the code required to free the handle.
        /// </summary>
        /// <returns>
        /// true if the handle is released successfully; otherwise, in the event of a catastrophic failure, false. In this case, it generates a releaseHandleFailed MDA Managed Debugging Assistant.
        /// </returns>
        override protected bool ReleaseHandle() {
            return NativeMethods.CloseHandle(handle);
        }
    }

    public sealed class SafeModuleHandle : SafeHandleZeroOrMinusOneIsInvalid {
        internal static SafeModuleHandle InvalidHandle = new SafeModuleHandle(IntPtr.Zero);
        public SafeModuleHandle() : base (true) {}

        public SafeModuleHandle(IntPtr handle) : base(true) {
            SetHandle(handle);
        }

        /// <summary>
        /// When overridden in a derived class, executes the code required to free the handle.
        /// </summary>
        /// <returns>
        /// true if the handle is released successfully; otherwise, in the event of a catastrophic failure, false. In this case, it generates a releaseHandleFailed MDA Managed Debugging Assistant.
        /// </returns>
        override protected bool ReleaseHandle() {
            return true;
        }
    }
}
