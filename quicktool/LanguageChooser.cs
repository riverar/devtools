//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011  Garrett Serack. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

using CoApp.Toolkit.Configuration;

namespace QuickTool {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Forms;

    public partial class LanguageChooser : Form {
        public bool ok = true;

        public string brushName = "";
        public string brushFile = "";
        public string clipSource = "";
        public int theme;

        private static readonly Dictionary<string, string> languages = new Dictionary<string, string> {
            {"ABAP","abap"},
            {"ActionScript","as"},
            {"ActionScript 3","as3"},
            {"Ada","ada"},
            {"ANTLR","antlr"},
            {"ANTLR With ActionScript Target","antlr-as"},
            {"ANTLR With C# Target","antlr-csharp"},
            {"ANTLR With CPP Target","antlr-cpp"},
            {"ANTLR With Java Target","antlr-java"},
            {"ANTLR With ObjectiveC Target","antlr-objc"},
            {"ANTLR With Perl Target","antlr-perl"},
            {"ANTLR With Python Target","antlr-python"},
            {"ANTLR With Ruby Target","antlr-ruby"},
            {"ApacheConf","apacheconf"},
            {"AppleScript","applescript"},
            {"aspx-cs","aspx-cs"},
            {"aspx-vb","aspx-vb"},
            {"Asymptote","asy"},
            {"Bash","bash"},
            {"Bash Session","console"},
            {"Batchfile","bat"},
            {"BBCode","bbcode"},
            {"Befunge","befunge"},
            {"Boo","boo"},
            {"Brainfuck","brainfuck"},
            {"C","c"},
            {"C#", "csharp"},
            {"C++","cpp"},
            {"c-objdump","c-objdump"},
            {"cfstatement","cfs"},
            {"Cheetah","cheetah"},
            {"Clojure","clojure"},
            {"CMake","cmake"},
            {"CoffeeScript","coffee-script"},
            {"Coldufsion HTML","cfm"},
            {"Common Lisp","common-lisp"},
            {"cpp-objdump","cpp-objdump"},
            {"CSS","css"},
            {"CSS+Django/Jinja","css+django"},
            {"CSS+Genshi Text","css+genshitext"},
            {"CSS+Mako","css+mako"},
            {"CSS+Myghty","css+myghty"},
            {"CSS+PHP","css+php"},
            {"CSS+Ruby","css+erb"},
            {"CSS+Smarty","css+smarty"},
            {"Cython","cython"},
            {"D","d"},
            {"d-objdump","d-objdump"},
            {"Darcs Patch","dpatch"},
            {"Debian Control file","control"},
            {"Debian Sourcelist","sourceslist"},
            {"Delphi","delphi"},
            {"Diff","diff"},
            {"Django/Jinja","django"},
            {"Dylan","dylan"},
            {"Embedded Ragel","ragel-em"},
            {"ERB","erb"},
            {"Erlang","erlang"},
            {"Erlang erl session","erl"},
            {"Evoque","evoque"},
            {"Felix","felix"},
            {"Fortran","fortran"},
            {"GAS","gas"},
            {"Genshi","genshi"},
            {"Genshi Text","genshitext"},
            {"Gettext Catalog","pot"},
            {"Gherkin","Cucumber"},
            {"GLSL","glsl"},
            {"Gnuplot","gnuplot"},
            {"Go","go"},
            {"Groff","groff"},
            {"Haml","haml"},
            {"Haskell","haskell"},
            {"haXe","hx"},
            {"HTML","html"},
            {"HTML+Cheetah","html+cheetah"},
            {"HTML+Django/Jinja","html+django"},
            {"HTML+Evoque","html+evoque"},
            {"HTML+Genshi","html+genshi"},
            {"HTML+Mako","html+mako"},
            {"HTML+Myghty","html+myghty"},
            {"HTML+PHP","html+php"},
            {"HTML+Smarty","html+smarty"},
            {"INI","ini"},
            {"Io","io"},
            {"IRC logs","irc"},
            {"Java","java"},
            {"Java Server Page","jsp"},
            {"JavaScript","js"},
            {"JavaScript+Cheetah","js+cheetah"},
            {"JavaScript+Django/Jinja","js+django"},
            {"JavaScript+Genshi Text","js+genshitext"},
            {"JavaScript+Mako","js+mako"},
            {"JavaScript+Myghty","js+myghty"},
            {"JavaScript+PHP","js+php"},
            {"JavaScript+Ruby","js+erb"},
            {"JavaScript+Smarty","js+smarty"},
            {"Lighttpd configuration file","lighty"},
            {"Literate Haskell","lhs"},
            {"LLVM","llvm"},
            {"Logtalk","logtalk"},
            {"Lua","lua"},
            {"Makefile","make"},
            {"Makefile (basemake)","basemake"},
            {"Mako","mako"},
            {"Matlab","matlab"},
            {"Matlab session","matlabsession"},
            {"MiniD","minid"},
            {"Modelica","modelica"},
            {"Modula-2","modula2"},
            {"MoinMoin/Trac Wiki markup","trac-wiki"},
            {"MOOCode","moocode"},
            {"MuPAD","mupad"},
            {"MXML","mxml"},
            {"Myghty","myghty"},
            {"MySQL","mysql"},
            {"NASM","nasm"},
            {"Newspeak","newspeak"},
            {"Nginx configuration file","nginx"},
            {"NumPy","numpy"},
            {"objdump","objdump"},
            {"Objective-C","objective-c"},
            {"Objective-J","objective-j"},
            {"OCaml","ocaml"},
            {"Ooc","ooc"},
            {"Perl","perl"},
            {"PHP","php"},
            {"POVRay","pov"},
            {"Prolog","prolog"},
            {"Python","python"},
            {"Python 3","python3"},
            {"Python 3.0 Traceback","py3tb"},
            {"Python console session","pycon"},
            {"Python Traceback","pytb"},
            {"Raw token data","raw"},
            {"RConsole","rconsole"},
            {"REBOL","rebol"},
            {"Redcode","redcode"},
            {"reStructuredText","rst"},
            {"RHTML","rhtml"},
            {"Ruby","rb"},
            {"Ruby irb session","rbcon"},
            {"S","splus"},
            {"Sass","sass"},
            {"Scala","scala"},
            {"Scheme","scheme"},
            {"Smalltalk","smalltalk"},
            {"Smarty","smarty"},
            {"SQL","sql"},
            {"sqlite3con","sqlite3"},
            {"SquidConf","squidconf"},
            {"Tcl","tcl"},
            {"Tcsh","tcsh"},
            {"TeX","tex"},
            {"Text only","text"},
            {"Vala","vala"},
            {"VB.net","vb.net"},
            {"VimL","vim"},
            {"XML","xml"},
            {"XML+Cheetah","xml+cheetah"},
            {"XML+Django/Jinja","xml+django"},
            {"XML+Evoque","xml+evoque"},
            {"XML+Mako","xml+mako"},
            {"XML+Myghty","xml+myghty"},
            {"XML+PHP","xml+php"},
            {"XML+Ruby","xml+erb"},
            {"XML+Smarty","xml+smarty"},
            {"XSLT","xslt"},
            {"YAML","yaml"},
        };

        public LanguageChooser() {
            InitializeComponent();
            lbLanguages.Items.AddRange(languages.Keys.ToArray());
            cbTheme.SelectedIndex = RegistryView.ApplicationUser["#lastTheme"].IntValue;
            lbLanguages.SelectedItem = RegistryView.ApplicationUser["#lastBrushFile"].StringValue ?? "C#";
            btnOK.Click += Ok;
            lbLanguages.DoubleClick += Ok;

            btnCancel.Click += (x, y) => {
                ok = false;
                brushFile = "";
                brushName = "";

                Hide();
            };

            FormClosing += (x, y) => {
                y.Cancel = true;
                brushFile = "";
                brushName = "";
                ok = false;
                Hide();
            };

            TopMost = true;
        }

        private void Ok(object sender, EventArgs e) {
            brushFile = lbLanguages.SelectedItem.ToString();
            RegistryView.ApplicationUser["#lastBrushFile"].StringValue = brushFile;
            RegistryView.ApplicationUser["#lastTheme"].IntValue = cbTheme.SelectedIndex;
            theme = cbTheme.SelectedIndex;
            brushName = languages[brushFile];
            ok = true;
            Hide();
        }

        protected override bool ProcessDialogKey(Keys keyData) {
            if (keyData == Keys.Escape) {
                ok = false;
                brushFile = "";
                brushName = "";
                Hide();
            }
            else if (keyData == Keys.Enter) {
                Ok(this, null);
            }

            return base.ProcessDialogKey(keyData);
        }
    }
}