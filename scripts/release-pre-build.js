// Include js.js
with(new ActiveXObject("Scripting.FileSystemObject"))for(var x in p=(".;js;scripts;"+WScript.scriptfullname.replace(/(.*\\)(.*)/g,"$1")+";"+new ActiveXObject("WScript.Shell").Environment("PROCESS")("PATH")).split(";"))if(FileExists(j=BuildPath(p[x],"js.js"))){eval(OpenTextFile(j).ReadAll());break}
Use("CoApp");

// on a release build, we need to revert the binary directory to the last checked out version
cd(CoApp.$SOLUTIONBINARIES());

for(var each in set = files( CoApp.$SOLUTIONBINARIES(), /.*/ ) ) {
    erase(set[each]);
}

print("Reverting binary files using git");
// use git to revert the binaries directory
print($$("cmd.exe /c git reset --hard HEAD" ));