//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyCompany("Outercurve Foundation")]
[assembly: AssemblyCopyright("Copyright (c) Garrett Serack, CoApp Contributors 2010-2011")]

// We no longer need to delay sign in order to strong name and sign the code before 
// we publish it, so now we will have just one set of  Version  lines, and no strong 
// naming until publishing.

[assembly: AssemblyVersion("1.1.2.1283")]
[assembly: AssemblyFileVersion("1.1.2.1283")]

[AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
internal class AssemblyBugtrackerAttribute : Attribute {
    public readonly string TrackerUrl;
    public AssemblyBugtrackerAttribute(string trackerURL) {
        TrackerUrl = trackerURL;
    }
    public override string ToString() {
        return TrackerUrl;
    }
}
