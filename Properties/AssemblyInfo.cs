//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010 Garrett Serack . All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("CoApp.CLI")]
[assembly: AssemblyDescription("CoApp command line utility")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("CoApp Project")]
[assembly: AssemblyProduct("CoApp Command Line Interface")]
[assembly: AssemblyCopyright("Copyright © Garrett Serack, CoApp Contributors 2010")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
#if SIGN_ASSEMBLY
// disable warning about using /keyfile instead of AssemblyKeyFile
#pragma warning disable 1699
[assembly: AssemblyKeyFileAttribute(@"..\coapp-signing\coapp-release.snk")]
#pragma warning restore 1699
#endif

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("12726103-fd13-46c3-bce5-48540532142e")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]

[assembly: AssemblyVersion("1.0.0.*")]
// by removing the following it defaults to the generated number above.
// [assembly: AssemblyFileVersion("1.0.0.0")]