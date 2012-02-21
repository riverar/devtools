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
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Windows.Media.Imaging;

    public class NativeResourceModule {
        private readonly SafeModuleHandle _moduleHandle;
        public NativeResourceModule( string filename ) {
            _moduleHandle = NativeMethods.LoadLibraryEx(filename, IntPtr.Zero, NativeMethods.LOAD_LIBRARY_AS_DATAFILE); 
            if( _moduleHandle.IsInvalid ) {
                throw new FileNotFoundException("Unable to find native resource module", filename);
            }
        }

        private MemoryStream GetBinaryResource(int resourceId) {
            var hRes = NativeMethods.FindResource(_moduleHandle, resourceId, "BINARY"); 
            if( hRes == IntPtr.Zero ) {
                throw new Exception(string.Format("Resource {0} not found", resourceId));
            }
            var size = NativeMethods.SizeofResource(_moduleHandle, hRes); 
            var pt = NativeMethods.LoadResource(_moduleHandle, hRes);
            var buffer = new byte[size];
            Marshal.Copy(pt, buffer, 0, (int)size);

            return new MemoryStream(buffer);
        }

        public BitmapImage GetBitmapImage(int resourceId) {
            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = GetBinaryResource(resourceId);
            image.EndInit();
            return image;
        }

        public String GetString(uint resourceId) {
            var sb = new StringBuilder(1024);
            if( NativeMethods.LoadString(_moduleHandle, resourceId , sb , 1024) > 0  ) {
                return sb.ToString();
            }
            return null;
        }
    }
}
