//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010 Garrett Serack . All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Autopackage")]
[assembly: AssemblyDescription("CoApp Autopackage utility")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyProduct("CoApp Autopackage")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: AssemblyBugtracker("https://github.com/coapp/autopackage/issues")]
[assembly: NeutralResourcesLanguage("en")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("12726103-fd13-46c3-bce5-949aab32142e")]
#if DEBUG

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Test.CoApp.Autopackage")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("AutopackageTestProject")]
#endif