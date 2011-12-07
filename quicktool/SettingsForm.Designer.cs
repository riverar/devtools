namespace QuickTool {
    partial class SettingsForm {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if(disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label2 = new System.Windows.Forms.Label();
            this.bitlyApiKey = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.bitlyUserId = new System.Windows.Forms.TextBox();
            this.buttonOK = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label10 = new System.Windows.Forms.Label();
            this.syntaxhighlighterPrefixPath = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.imageFilename = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.ftpFolder = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.httpUrlTemplate = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.ftpPassword = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.ftpUsername = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.ftpServer = new System.Windows.Forms.TextBox();
            this.cbAudioCues = new System.Windows.Forms.CheckBox();
            this.Hotkeys = new System.Windows.Forms.GroupBox();
            this.btnSetManualBitlyHotkey = new System.Windows.Forms.Button();
            this.manualBitlylabel = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.btnSetSourceUploaderHotkey = new System.Windows.Forms.Button();
            this.label9 = new System.Windows.Forms.Label();
            this.quickSourceHotkeyLabel = new System.Windows.Forms.Label();
            this.btnSetQuickUploaderHotkey = new System.Windows.Forms.Button();
            this.label11 = new System.Windows.Forms.Label();
            this.quickUploaderHotkeyLabel = new System.Windows.Forms.Label();
            this.labelx = new System.Windows.Forms.Label();
            this.cbAutoBitly = new System.Windows.Forms.CheckBox();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.Hotkeys.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.bitlyApiKey);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.bitlyUserId);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(290, 104);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Bit.ly ";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(7, 59);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(45, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "API Key";
            // 
            // bitlyApiKey
            // 
            this.bitlyApiKey.Location = new System.Drawing.Point(7, 75);
            this.bitlyApiKey.Name = "bitlyApiKey";
            this.bitlyApiKey.Size = new System.Drawing.Size(277, 20);
            this.bitlyApiKey.TabIndex = 2;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(7, 20);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(43, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "User ID";
            // 
            // bitlyUserId
            // 
            this.bitlyUserId.Location = new System.Drawing.Point(7, 36);
            this.bitlyUserId.Name = "bitlyUserId";
            this.bitlyUserId.Size = new System.Drawing.Size(277, 20);
            this.bitlyUserId.TabIndex = 0;
            // 
            // buttonOK
            // 
            this.buttonOK.Location = new System.Drawing.Point(522, 475);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new System.Drawing.Size(75, 23);
            this.buttonOK.TabIndex = 1;
            this.buttonOK.Text = "OK";
            this.buttonOK.UseVisualStyleBackColor = true;
            // 
            // buttonCancel
            // 
            this.buttonCancel.Location = new System.Drawing.Point(441, 475);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonCancel.TabIndex = 2;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.label10);
            this.groupBox2.Controls.Add(this.syntaxhighlighterPrefixPath);
            this.groupBox2.Controls.Add(this.label8);
            this.groupBox2.Controls.Add(this.imageFilename);
            this.groupBox2.Controls.Add(this.label7);
            this.groupBox2.Controls.Add(this.ftpFolder);
            this.groupBox2.Controls.Add(this.label6);
            this.groupBox2.Controls.Add(this.httpUrlTemplate);
            this.groupBox2.Controls.Add(this.label5);
            this.groupBox2.Controls.Add(this.ftpPassword);
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Controls.Add(this.ftpUsername);
            this.groupBox2.Controls.Add(this.label4);
            this.groupBox2.Controls.Add(this.ftpServer);
            this.groupBox2.Location = new System.Drawing.Point(12, 122);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(290, 300);
            this.groupBox2.TabIndex = 3;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "FTP Uploading";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(7, 254);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(143, 13);
            this.label10.TabIndex = 13;
            this.label10.Text = "SyntaxHighlighter Prefix Path";
            // 
            // syntaxhighlighterPrefixPath
            // 
            this.syntaxhighlighterPrefixPath.Location = new System.Drawing.Point(7, 270);
            this.syntaxhighlighterPrefixPath.Name = "syntaxhighlighterPrefixPath";
            this.syntaxhighlighterPrefixPath.Size = new System.Drawing.Size(277, 20);
            this.syntaxhighlighterPrefixPath.TabIndex = 12;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(7, 176);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(86, 13);
            this.label8.TabIndex = 11;
            this.label8.Text = "Filename Pattern";
            // 
            // imageFilename
            // 
            this.imageFilename.Location = new System.Drawing.Point(7, 192);
            this.imageFilename.Name = "imageFilename";
            this.imageFilename.Size = new System.Drawing.Size(277, 20);
            this.imageFilename.TabIndex = 10;
            this.imageFilename.Text = "{date}-{time}-{counter}.png";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(7, 137);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(103, 13);
            this.label7.TabIndex = 9;
            this.label7.Text = "Folder to upload into";
            // 
            // ftpFolder
            // 
            this.ftpFolder.Location = new System.Drawing.Point(7, 153);
            this.ftpFolder.Name = "ftpFolder";
            this.ftpFolder.Size = new System.Drawing.Size(277, 20);
            this.ftpFolder.TabIndex = 8;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(7, 215);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(94, 13);
            this.label6.TabIndex = 7;
            this.label6.Text = "HTTP Url To Build";
            // 
            // httpUrlTemplate
            // 
            this.httpUrlTemplate.Location = new System.Drawing.Point(7, 231);
            this.httpUrlTemplate.Name = "httpUrlTemplate";
            this.httpUrlTemplate.Size = new System.Drawing.Size(277, 20);
            this.httpUrlTemplate.TabIndex = 6;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(6, 98);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(53, 13);
            this.label5.TabIndex = 5;
            this.label5.Text = "Password";
            // 
            // ftpPassword
            // 
            this.ftpPassword.Location = new System.Drawing.Point(6, 114);
            this.ftpPassword.Name = "ftpPassword";
            this.ftpPassword.Size = new System.Drawing.Size(277, 20);
            this.ftpPassword.TabIndex = 4;
            this.ftpPassword.UseSystemPasswordChar = true;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(7, 59);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(55, 13);
            this.label3.TabIndex = 3;
            this.label3.Text = "Username";
            // 
            // ftpUsername
            // 
            this.ftpUsername.Location = new System.Drawing.Point(7, 75);
            this.ftpUsername.Name = "ftpUsername";
            this.ftpUsername.Size = new System.Drawing.Size(277, 20);
            this.ftpUsername.TabIndex = 2;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(7, 20);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(61, 13);
            this.label4.TabIndex = 1;
            this.label4.Text = "FTP Server";
            // 
            // ftpServer
            // 
            this.ftpServer.Location = new System.Drawing.Point(7, 36);
            this.ftpServer.Name = "ftpServer";
            this.ftpServer.Size = new System.Drawing.Size(277, 20);
            this.ftpServer.TabIndex = 0;
            // 
            // cbAudioCues
            // 
            this.cbAudioCues.AutoSize = true;
            this.cbAudioCues.Location = new System.Drawing.Point(317, 255);
            this.cbAudioCues.Name = "cbAudioCues";
            this.cbAudioCues.Size = new System.Drawing.Size(116, 17);
            this.cbAudioCues.TabIndex = 4;
            this.cbAudioCues.Text = "Enable Audio Cues";
            this.cbAudioCues.UseVisualStyleBackColor = true;
            // 
            // Hotkeys
            // 
            this.Hotkeys.Controls.Add(this.btnSetManualBitlyHotkey);
            this.Hotkeys.Controls.Add(this.manualBitlylabel);
            this.Hotkeys.Controls.Add(this.label13);
            this.Hotkeys.Controls.Add(this.btnSetSourceUploaderHotkey);
            this.Hotkeys.Controls.Add(this.label9);
            this.Hotkeys.Controls.Add(this.quickSourceHotkeyLabel);
            this.Hotkeys.Controls.Add(this.btnSetQuickUploaderHotkey);
            this.Hotkeys.Controls.Add(this.label11);
            this.Hotkeys.Controls.Add(this.quickUploaderHotkeyLabel);
            this.Hotkeys.Controls.Add(this.labelx);
            this.Hotkeys.Location = new System.Drawing.Point(308, 12);
            this.Hotkeys.Name = "Hotkeys";
            this.Hotkeys.Size = new System.Drawing.Size(290, 182);
            this.Hotkeys.TabIndex = 4;
            this.Hotkeys.TabStop = false;
            this.Hotkeys.Text = "Hotkeys";
            // 
            // btnSetManualBitlyHotkey
            // 
            this.btnSetManualBitlyHotkey.Location = new System.Drawing.Point(209, 110);
            this.btnSetManualBitlyHotkey.Name = "btnSetManualBitlyHotkey";
            this.btnSetManualBitlyHotkey.Size = new System.Drawing.Size(75, 23);
            this.btnSetManualBitlyHotkey.TabIndex = 10;
            this.btnSetManualBitlyHotkey.Text = "Set";
            this.btnSetManualBitlyHotkey.UseVisualStyleBackColor = true;
            // 
            // manualBitlylabel
            // 
            this.manualBitlylabel.AutoSize = true;
            this.manualBitlylabel.Location = new System.Drawing.Point(31, 115);
            this.manualBitlylabel.Name = "manualBitlylabel";
            this.manualBitlylabel.Size = new System.Drawing.Size(30, 13);
            this.manualBitlylabel.TabIndex = 9;
            this.manualBitlylabel.Text = "xxxx ";
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(6, 96);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(64, 13);
            this.label13.TabIndex = 8;
            this.label13.Text = "Manual Bitly";
            // 
            // btnSetSourceUploaderHotkey
            // 
            this.btnSetSourceUploaderHotkey.Location = new System.Drawing.Point(209, 73);
            this.btnSetSourceUploaderHotkey.Name = "btnSetSourceUploaderHotkey";
            this.btnSetSourceUploaderHotkey.Size = new System.Drawing.Size(75, 23);
            this.btnSetSourceUploaderHotkey.TabIndex = 7;
            this.btnSetSourceUploaderHotkey.Text = "Set";
            this.btnSetSourceUploaderHotkey.UseVisualStyleBackColor = true;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(6, 159);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(277, 13);
            this.label9.TabIndex = 5;
            this.label9.Text = "Note: Hotkeys are disabled while settings window is open";
            // 
            // quickSourceHotkeyLabel
            // 
            this.quickSourceHotkeyLabel.AutoSize = true;
            this.quickSourceHotkeyLabel.Location = new System.Drawing.Point(31, 78);
            this.quickSourceHotkeyLabel.Name = "quickSourceHotkeyLabel";
            this.quickSourceHotkeyLabel.Size = new System.Drawing.Size(30, 13);
            this.quickSourceHotkeyLabel.TabIndex = 6;
            this.quickSourceHotkeyLabel.Text = "xxxx ";
            // 
            // btnSetQuickUploaderHotkey
            // 
            this.btnSetQuickUploaderHotkey.Location = new System.Drawing.Point(209, 34);
            this.btnSetQuickUploaderHotkey.Name = "btnSetQuickUploaderHotkey";
            this.btnSetQuickUploaderHotkey.Size = new System.Drawing.Size(75, 23);
            this.btnSetQuickUploaderHotkey.TabIndex = 4;
            this.btnSetQuickUploaderHotkey.Text = "Set";
            this.btnSetQuickUploaderHotkey.UseVisualStyleBackColor = true;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(6, 59);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(87, 13);
            this.label11.TabIndex = 5;
            this.label11.Text = "Source Uploader";
            // 
            // quickUploaderHotkeyLabel
            // 
            this.quickUploaderHotkeyLabel.AutoSize = true;
            this.quickUploaderHotkeyLabel.Location = new System.Drawing.Point(31, 39);
            this.quickUploaderHotkeyLabel.Name = "quickUploaderHotkeyLabel";
            this.quickUploaderHotkeyLabel.Size = new System.Drawing.Size(30, 13);
            this.quickUploaderHotkeyLabel.TabIndex = 3;
            this.quickUploaderHotkeyLabel.Text = "xxxx ";
            // 
            // labelx
            // 
            this.labelx.AutoSize = true;
            this.labelx.Location = new System.Drawing.Point(6, 20);
            this.labelx.Name = "labelx";
            this.labelx.Size = new System.Drawing.Size(78, 13);
            this.labelx.TabIndex = 2;
            this.labelx.Text = "QuickUploader";
            // 
            // cbAutoBitly
            // 
            this.cbAutoBitly.AutoSize = true;
            this.cbAutoBitly.Location = new System.Drawing.Point(317, 232);
            this.cbAutoBitly.Name = "cbAutoBitly";
            this.cbAutoBitly.Size = new System.Drawing.Size(215, 17);
            this.cbAutoBitly.TabIndex = 5;
            this.cbAutoBitly.Text = "Automatically Shorten URLs in clipboard";
            this.cbAutoBitly.UseVisualStyleBackColor = true;
            // 
            // SettingsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(609, 510);
            this.Controls.Add(this.cbAutoBitly);
            this.Controls.Add(this.Hotkeys);
            this.Controls.Add(this.cbAudioCues);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonOK);
            this.Controls.Add(this.groupBox1);
            this.Name = "SettingsForm";
            this.Text = "QuickTool Settings";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.Hotkeys.ResumeLayout(false);
            this.Hotkeys.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox bitlyApiKey;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox bitlyUserId;
        private System.Windows.Forms.Button buttonOK;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox ftpUsername;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox ftpServer;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox imageFilename;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox ftpFolder;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox httpUrlTemplate;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox ftpPassword;
        private System.Windows.Forms.CheckBox cbAudioCues;
        private System.Windows.Forms.GroupBox Hotkeys;
        private System.Windows.Forms.Button btnSetQuickUploaderHotkey;
        private System.Windows.Forms.Label quickUploaderHotkeyLabel;
        private System.Windows.Forms.Label labelx;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Button btnSetSourceUploaderHotkey;
        private System.Windows.Forms.Label quickSourceHotkeyLabel;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TextBox syntaxhighlighterPrefixPath;
        private System.Windows.Forms.Button btnSetManualBitlyHotkey;
        private System.Windows.Forms.Label manualBitlylabel;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.CheckBox cbAutoBitly;
    }
}

