using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("CoApp.CCI")]
[assembly: AssemblyDescription("Common Compiler Infrastructure (Microsoft CCI")]
#if DEBUG
[assembly: AssemblyConfiguration("DEBUG - Built from SVN (https://cciast.svn.codeplex.com/svn) Changeset #65497")]
#else
[assembly: AssemblyConfiguration("RELEASE - Built from SVN (https://cciast.svn.codeplex.com/svn) Changeset #65497")]
#endif
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("CoApp.CCI")]
[assembly: AssemblyCopyright("Microsoft.CCI (http://cciast.codeplex.com/) Copyright © Microsoft 2011")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("7617aa58-d012-40fb-8ff9-67116b786280")]

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
[assembly: AssemblyVersion("2.0.8.17659")]
[assembly: AssemblyFileVersion("2.0.8.17659")]

/*
 * Instructions for merging with upstream SVN 

 * In a new location, check out the whole source tree.
 * svn co https://cciast.svn.codeplex.com/svn
  
 * Using a merge tool, pull changes from Sources -> Ast
 * (ignore: common/ bin/ build/ obj/ .svn/ .git\ *.cd *.csproj AssemblyInfo.cs)
 *
 *       merge %SVNROOT%\Sources %COAPPCCI%\Ast
 * 
 * then pull changes from Metadata\Sources -> Metadata
 * 
 *       merge %SVNROOT%\Metadata\Sources %COAPPCCI%\Metadata
 
 * Be careful of the changes I mention http://ccimetadata.codeplex.com/discussions/273705 
*/