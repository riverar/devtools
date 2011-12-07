//-----------------------------------------------------------------------
// functions for the release-sign-* scripts.

var CoApp = {
    $SIMPLESIGNER: function () {
        return this.$SOLUTIONTOOLS("\\simplesigner.exe");
    },

    $SOLUTIONDIR: function (path) {
        return this.GetRelativePath("\\..\\" + (path || ""));
    },

    $SOLUTIONTOOLS: function (path) {
        return this.$SOLUTIONDIR("\\tools\\" + (path || ""));
    },

    $SOLUTIONSOURCE: function (path) {
        return this.$SOLUTIONDIR("\\source\\" + (path || ""));
    },

    $SOLUTIONBINARIES: function (path) {
        return this.$SOLUTIONDIR("\\binaries\\" + (path || ""));
    },

    $RELEASEDIR: function (path) {
        return this.$SOLUTIONDIR("\\output\\any\\release\\bin\\" + (path || ""));
    },

    $DEBUGDIR: function (path) {
        return this.$SOLUTIONDIR("\\output\\any\\debug\\bin\\" + (path || ""));
    },

    $SIBLINGDEBUGDIR: function (siblingrepo,path) {
        return this.$SOLUTIONDIR("..\\"+siblingrepo+"\\output\\any\\debug\\bin\\" + (path || ""));
    },

    
    $COAPPDIR: function (path) {
        return this.$SOLUTIONDIR("\\..\\" + (path || ""));
    },

    LoadTextFile: function (filename) {
        if (exists(filename)) {
            return ReadAll(filename);
        }
        print("Cannot find file [{0}]", filename);
        return null;
    },

    GetRelativePath: function (filename, rootLocation) {
        rootLocation = rootLocation || fullpath($$.WScript.ScriptFullName);
        return fullpath(rootLocation.substring(0, rootLocation.lastIndexOf("\\")) + filename);
    },

    SaveTextFile: function (filename, text) {
        var f = $$.fso.OpenTextFile(filename, 2, true);
        f.Write(text.Trim() + "\r\n");
        f.Close();
    },

    SignBinary: function (binaries) {
        var filenames = (typeof (binaries) == typeof ([])) ? binaries : [binaries];

        var list = "";
        
        for (var i = 0; i < filenames.length; i++) {
            var filename = filenames[i];
            if (!exists(filename)) {
                return Assert.Fail("Can't sign binary '{0}' --does not exist", filename);
            }
            
            print("Siging {0}", filename);
            list = list + '"' + filename + '" ';
        }
        
        
        print('"{0}" --sign-only {1}', this.$SIMPLESIGNER(), list);
        print($$('"{0}" --sign-only {1}', this.$SIMPLESIGNER(), list));

        if ($ERRORLEVEL) {
            for (var each in $StdOut) {
                print($StdOut[each]);
            }
            return Assert.Fail("Failed signing binary ..");
        } else {
            print("   [SUCCESS]");
        }
    },

    StrongNameBinary: function (binaries) {
        var filenames = (typeof (binaries) == typeof ([])) ? binaries : [binaries];

        var list = "";
        
        for (var i = 0; i < filenames.length; i++) {
            var filename = filenames[i];
            if (!exists(filename)) {
                return Assert.Fail("Can't strong-name binary '{0}' --does not exist", filename);
            }
            print("Strong Naming {0}", filename);
            list = list + '"' + filename + '" ';
        }
        
        $$('"{0}" {1}', this.$SIMPLESIGNER(), list);
        if ($ERRORLEVEL) {
            for (var each in $StdOut) {
                print($StdOut[each]);
            }
            return Assert.Fail("Failed strong-naming binary");
        } else {
            print("   [SUCCESS]");    
        }
    },

    CopyFiles: function (files, destinationFolder) {
        var filenames = (typeof (files) == typeof ([])) ? files : [files];
        for (var i = 0; i < filenames.length; i++) {

            copy(filenames[i], destinationFolder);
            print("Copied '{0}' ==> '{1}'", filenames[i], destinationFolder);
        }
    }
};


CoApp;