using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CoApp.Toolkit.Extensions;

namespace QuickTool {
    public partial class LanguageChooser : Form {
        public bool ok = true;
        
        public string brushName = "";
        public string brushFile = "";
        public string clipSource = "";

        private static Dictionary<string, string> languages = new Dictionary<string, string>() {
                {"AS3", "actionscript3" },
                {"Bash", "bash"},
                {"ColdFusion", "coldfusion" },
                {"CSharp", "csharp"},
                {"Cpp", "cpp"},
                {"Css", "css"},
                {"Delphi", "pascal"},
                {"Diff", "diff"},
                {"Erlang", "erlang"},
                {"Groovy", "groovy"},
                {"JScript", "js"},
                {"Java", "java"},
                {"JavaFX", "javafx"},
                {"Perl", "perl"},
                {"php", "Php"},
                {"Plain", "text"},
                {"PowerShell", "powershell" },
                {"Python", "python"},
                {"Ruby", "ruby"},
                {"Scala", "scala"},
                {"Sql", "sql"},
                {"Vb", "vb"},
                {"Xml", "xml"}
            };

        public LanguageChooser() {
            InitializeComponent();
            lbLanguages.Items.AddRange(languages.Keys.ToArray());
            lbLanguages.SelectedItem = QuickSettings.Instance["lastBrushFile"] ?? "CSharp";
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
            QuickSettings.Instance["lastBrushFile"] = brushFile;
            brushName = languages[brushFile];
            ok = true;
            Hide();
        }

        protected override bool ProcessDialogKey(Keys keyData) {
            if( keyData == Keys.Escape) {
                ok = false;
                brushFile = "";
                brushName = "";
                Hide();
                
            } else if (keyData == Keys.Enter) {
                Ok(this, null);
            }

            return base.ProcessDialogKey(keyData);
        }
    }
}
