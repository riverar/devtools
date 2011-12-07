﻿//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     ResourceLib Original Code from http://resourcelib.codeplex.com
//     Original Copyright (c) 2008-2009 Vestris Inc.
//     Changes Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
// MIT License
// You may freely use and distribute this software under the terms of the following license agreement.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
// documentation files (the "Software"), to deal in the Software without restriction, including without limitation 
// the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and 
// to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of 
// the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO 
// THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, 
// TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Developer.Toolkit.ResourceLib {
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using CoApp.Toolkit.Win32;

    /// <summary>
    ///   A container for the DIALOGTEMPLATEEX structure.
    /// </summary>
    public class DialogExTemplateControl : DialogTemplateControlBase {
        private DialogExItemTemplate _header;

        /// <summary>
        ///   X-coordinate, in dialog box units, of the upper-left corner of the dialog box.
        /// </summary>
        public override Int16 x {
            get { return _header.x; }
            set { _header.x = value; }
        }

        /// <summary>
        ///   Y-coordinate, in dialog box units, of the upper-left corner of the dialog box.
        /// </summary>
        public override Int16 y {
            get { return _header.y; }
            set { _header.y = value; }
        }

        /// <summary>
        ///   Width, in dialog box units, of the dialog box.
        /// </summary>
        public override Int16 cx {
            get { return _header.cx; }
            set { _header.cx = value; }
        }

        /// <summary>
        ///   Height, in dialog box units, of the dialog box.
        /// </summary>
        public override Int16 cy {
            get { return _header.cy; }
            set { _header.cy = value; }
        }

        /// <summary>
        ///   Dialog style.
        /// </summary>
        public override UInt32 Style {
            get { return _header.style; }
            set { _header.style = value; }
        }

        /// <summary>
        ///   Extended dialog style.
        /// </summary>
        public override UInt32 ExtendedStyle {
            get { return _header.exStyle; }
            set { _header.exStyle = value; }
        }

        /// <summary>
        ///   Control identifier.
        /// </summary>
        public Int32 Id {
            get { return _header.id; }
            set { _header.id = value; }
        }

        /// <summary>
        ///   Read the dialog control.
        /// </summary>
        /// <param name = "lpRes">Pointer to the beginning of the dialog structure.</param>
        internal override IntPtr Read(IntPtr lpRes) {
            _header = (DialogExItemTemplate) Marshal.PtrToStructure(lpRes, typeof (DialogExItemTemplate));

            lpRes = new IntPtr(lpRes.ToInt32() + Marshal.SizeOf(_header));
            return base.Read(lpRes);
        }

        /// <summary>
        ///   Write the dialog control to a binary stream.
        /// </summary>
        /// <param name = "w">Binary stream.</param>
        public override void Write(BinaryWriter w) {
            w.Write(_header.helpID);
            w.Write(_header.exStyle);
            w.Write(_header.style);
            w.Write(_header.x);
            w.Write(_header.y);
            w.Write(_header.cx);
            w.Write(_header.cy);
            w.Write(_header.id);
            base.Write(w);
        }

        /// <summary>
        ///   Return a string representation of the dialog control.
        /// </summary>
        /// <returns>A single line in the "CLASS name id, dimensions and styles' format.</returns>
        public override string ToString() {
            var sb = new StringBuilder();

            sb.AppendFormat("{0} \"{1}\" {2}, {3}, {4}, {5}, {6}, {7}, {8}", ControlClass, CaptionId, Id, ControlClass, x, y, cx, cy,
                DialogTemplateUtil.StyleToString<WindowStyles, StaticControlStyles>(Style, ExtendedStyle));

            switch (ControlClass) {
                case DialogItemClass.Button:
                    sb.AppendFormat("| {0}", (ButtonControlStyles) (Style & 0xFFFF));
                    break;
                case DialogItemClass.Edit:
                    sb.AppendFormat("| {0}", DialogTemplateUtil.StyleToString<EditControlStyles>(Style & 0xFFFF));
                    break;
            }

            return sb.ToString();
        }
    }
}