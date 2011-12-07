// Include js.js
with(new ActiveXObject("Scripting.FileSystemObject"))for(var x in p=(".;js;scripts;"+WScript.scriptfullname.replace(/(.*\\)(.*)/g,"$1")+";"+new ActiveXObject("WScript.Shell").Environment("PROCESS")("PATH")).split(";"))if(FileExists(j=BuildPath(p[x],"js.js"))){eval(OpenTextFile(j).ReadAll());break}
Use("CoApp");

if (folderExists(CoApp.$SOLUTIONDIR("..\\signing"))) {

    CoApp.StrongNameBinary([
        CoApp.$RELEASEDIR("autopackage.exe"),
        CoApp.$RELEASEDIR("azure.exe"),
        CoApp.$RELEASEDIR("coapp.cci.dll"),
        CoApp.$RELEASEDIR("coapp.developer.toolkit.dll"),
        CoApp.$RELEASEDIR("ptk.exe"),
        CoApp.$RELEASEDIR("quicktool.exe"),
        CoApp.$RELEASEDIR("scan.exe"),
        CoApp.$RELEASEDIR("simplesigner.exe"),
        CoApp.$RELEASEDIR("toolscanner.exe"),
        CoApp.$RELEASEDIR("mkRepo.exe")
     ]);
}
/// Every time we do a release-sign, we increment the build number.
var rx, major, minor, build, revision, filename;

/// ---- Developer Tools Pacakge --------------------------------------------------------------------------------------------
filename = CoApp.$SOLUTIONSOURCE("CoApp.Devtools.AssemblyStrongName.cs");

if (newTxt = CoApp.LoadTextFile(filename)) {
    rx = /\[assembly: AssemblyVersion\("(.*)\.(.*)\.(.*)\.(.*)"\)\]/ig.exec(newTxt); // Get Assembly Version
    
    major = parseInt(RegExp.$1.Trim());
    minor = parseInt(RegExp.$2.Trim());
    build = parseInt(RegExp.$3.Trim());
    revision = parseInt(RegExp.$4.Trim())+1;

    if( major < 1 )
        throw  "FAILURE (1)";
    
    newTxt = newTxt.replace( /\[assembly: AssemblyVersion.*/ig , '[assembly: AssemblyVersion("'+major+'.'+minor+'.'+build+'.'+revision+'")]' );
    newTxt = newTxt.replace( /\[assembly: AssemblyFileVersion.*/ig , '[assembly: AssemblyFileVersion("'+major+'.'+minor+'.'+build+'.'+revision+'")]' );
   
    WScript.echo("Incrementing Version Attributes in "+filename);
    CoApp.SaveTextFile(filename, newTxt);
}