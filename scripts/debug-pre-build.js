// Include js.js
with(new ActiveXObject("Scripting.FileSystemObject"))for(var x in p=(".;js;scripts;"+WScript.scriptfullname.replace(/(.*\\)(.*)/g,"$1")+";"+new ActiveXObject("WScript.Shell").Environment("PROCESS")("PATH")).split(";"))if(FileExists(j=BuildPath(p[x],"js.js"))){eval(OpenTextFile(j).ReadAll());break}
Use("CoApp");

var files = [ 
    CoApp.$SIBLINGDEBUGDIR("coapp","CoApp.Toolkit.dll"),
    CoApp.$SIBLINGDEBUGDIR("coapp","CoApp.Toolkit.Debug.dll"),
    CoApp.$SIBLINGDEBUGDIR("coapp","CoApp.Toolkit.Engine.Client.dll")
];

// on a debug build, we need to copy the latest built debug binaries from the coapp core folder
CoApp.CopyFiles(files , CoApp.$SOLUTIONBINARIES());

