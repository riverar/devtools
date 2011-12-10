//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011  Garrett Serack. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace QuickTool {
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;
    using System.Media;
    using System.Net;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using System.Windows.Threading;
    using CoApp.Toolkit.Configuration;
    using CoApp.Toolkit.Extensions;
    using CoApp.Toolkit.Network;
    using CoApp.Toolkit.Tasks;
    using CoApp.Toolkit.Win32;

    public class QtAppContext : ApplicationContext {
        private static readonly string[] Themes = {

@".highlight  { 
	background:none repeat scroll 0 0 #000;
	padding:0.4em;
	margin-bottom:0.6em;
	overflow:auto;
	-moz-border-radius:6px;
	-webkit-border-radius:6px;
	border-radius:6px;
	font-family: Inconsolata, Consolas, Courier;
	}

.highlight .hll { background-color: #ffffcc }
.highlight  { background: #ffffff; }
.highlight .c { color: #008000 } /* Comment */
.highlight .err { border: 1px solid #FF0000 } /* Error */
.highlight .k { color: #0000ff } /* Keyword */
.highlight .cm { color: #008000 } /* Comment.Multiline */
.highlight .cp { color: #0000ff } /* Comment.Preproc */
.highlight .c1 { color: #008000 } /* Comment.Single */
.highlight .cs { color: #008000 } /* Comment.Special */
.highlight .ge { font-style: italic } /* Generic.Emph */
.highlight .gh { font-weight: bold } /* Generic.Heading */
.highlight .gp { font-weight: bold } /* Generic.Prompt */
.highlight .gs { font-weight: bold } /* Generic.Strong */
.highlight .gu { font-weight: bold } /* Generic.Subheading */
.highlight .kc { color: #0000ff } /* Keyword.Constant */
.highlight .kd { color: #0000ff } /* Keyword.Declaration */
.highlight .kn { color: #0000ff } /* Keyword.Namespace */
.highlight .kp { color: #0000ff } /* Keyword.Pseudo */
.highlight .kr { color: #0000ff } /* Keyword.Reserved */
.highlight .kt { color: #2b91af } /* Keyword.Type */
.highlight .s { color: #a31515 } /* Literal.String */
.highlight .nc { color: #2b91af } /* Name.Class */
.highlight .ow { color: #0000ff } /* Operator.Word */
.highlight .sb { color: #a31515 } /* Literal.String.Backtick */
.highlight .sc { color: #a31515 } /* Literal.String.Char */
.highlight .sd { color: #a31515 } /* Literal.String.Doc */
.highlight .s2 { color: #a31515 } /* Literal.String.Double */
.highlight .se { color: #a31515 } /* Literal.String.Escape */
.highlight .sh { color: #a31515 } /* Literal.String.Heredoc */
.highlight .si { color: #a31515 } /* Literal.String.Interpol */
.highlight .sx { color: #a31515 } /* Literal.String.Other */
.highlight .sr { color: #a31515 } /* Literal.String.Regex */
.highlight .s1 { color: #a31515 } /* Literal.String.Single */
.highlight .ss { color: #a31515 } /* Literal.String.Symbol */",
               
@".highlight  { 
	background:none repeat scroll 0 0 #000;
	padding:0.4em;
	margin-bottom:0.6em;
	overflow:auto;
	-moz-border-radius:6px;
	-webkit-border-radius:6px;
	border-radius:6px;
	font-family: Inconsolata, Consolas, Courier;
	}


.highlight .hll { background-color: #ffffcc }
.highlight  { background: #ffffff; }
.highlight .c { color: #888888 } /* Comment */
.highlight .err { color: #a61717; background-color: #e3d2d2 } /* Error */
.highlight .k { color: #008800; font-weight: bold } /* Keyword */
.highlight .cm { color: #888888 } /* Comment.Multiline */
.highlight .cp { color: #cc0000; font-weight: bold } /* Comment.Preproc */
.highlight .c1 { color: #888888 } /* Comment.Single */
.highlight .cs { color: #cc0000; font-weight: bold; background-color: #fff0f0 } /* Comment.Special */
.highlight .gd { color: #000000; background-color: #ffdddd } /* Generic.Deleted */
.highlight .ge { font-style: italic } /* Generic.Emph */
.highlight .gr { color: #aa0000 } /* Generic.Error */
.highlight .gh { color: #303030 } /* Generic.Heading */
.highlight .gi { color: #000000; background-color: #ddffdd } /* Generic.Inserted */
.highlight .go { color: #888888 } /* Generic.Output */
.highlight .gp { color: #555555 } /* Generic.Prompt */
.highlight .gs { font-weight: bold } /* Generic.Strong */
.highlight .gu { color: #606060 } /* Generic.Subheading */
.highlight .gt { color: #aa0000 } /* Generic.Traceback */
.highlight .kc { color: #008800; font-weight: bold } /* Keyword.Constant */
.highlight .kd { color: #008800; font-weight: bold } /* Keyword.Declaration */
.highlight .kn { color: #008800; font-weight: bold } /* Keyword.Namespace */
.highlight .kp { color: #008800 } /* Keyword.Pseudo */
.highlight .kr { color: #008800; font-weight: bold } /* Keyword.Reserved */
.highlight .kt { color: #888888; font-weight: bold } /* Keyword.Type */
.highlight .m { color: #0000DD; font-weight: bold } /* Literal.Number */
.highlight .s { color: #dd2200; background-color: #fff0f0 } /* Literal.String */
.highlight .na { color: #336699 } /* Name.Attribute */
.highlight .nb { color: #003388 } /* Name.Builtin */
.highlight .nc { color: #bb0066; font-weight: bold } /* Name.Class */
.highlight .no { color: #003366; font-weight: bold } /* Name.Constant */
.highlight .nd { color: #555555 } /* Name.Decorator */
.highlight .ne { color: #bb0066; font-weight: bold } /* Name.Exception */
.highlight .nf { color: #0066bb; font-weight: bold } /* Name.Function */
.highlight .nl { color: #336699; font-style: italic } /* Name.Label */
.highlight .nn { color: #bb0066; font-weight: bold } /* Name.Namespace */
.highlight .py { color: #336699; font-weight: bold } /* Name.Property */
.highlight .nt { color: #bb0066; font-weight: bold } /* Name.Tag */
.highlight .nv { color: #336699 } /* Name.Variable */
.highlight .ow { color: #008800 } /* Operator.Word */
.highlight .w { color: #bbbbbb } /* Text.Whitespace */
.highlight .mf { color: #0000DD; font-weight: bold } /* Literal.Number.Float */
.highlight .mh { color: #0000DD; font-weight: bold } /* Literal.Number.Hex */
.highlight .mi { color: #0000DD; font-weight: bold } /* Literal.Number.Integer */
.highlight .mo { color: #0000DD; font-weight: bold } /* Literal.Number.Oct */
.highlight .sb { color: #dd2200; background-color: #fff0f0 } /* Literal.String.Backtick */
.highlight .sc { color: #dd2200; background-color: #fff0f0 } /* Literal.String.Char */
.highlight .sd { color: #dd2200; background-color: #fff0f0 } /* Literal.String.Doc */
.highlight .s2 { color: #dd2200; background-color: #fff0f0 } /* Literal.String.Double */
.highlight .se { color: #0044dd; background-color: #fff0f0 } /* Literal.String.Escape */
.highlight .sh { color: #dd2200; background-color: #fff0f0 } /* Literal.String.Heredoc */
.highlight .si { color: #3333bb; background-color: #fff0f0 } /* Literal.String.Interpol */
.highlight .sx { color: #22bb22; background-color: #f0fff0 } /* Literal.String.Other */
.highlight .sr { color: #008800; background-color: #fff0ff } /* Literal.String.Regex */
.highlight .s1 { color: #dd2200; background-color: #fff0f0 } /* Literal.String.Single */
.highlight .ss { color: #aa6600; background-color: #fff0f0 } /* Literal.String.Symbol */
.highlight .bp { color: #003388 } /* Name.Builtin.Pseudo */
.highlight .vc { color: #336699 } /* Name.Variable.Class */
.highlight .vg { color: #dd7700 } /* Name.Variable.Global */
.highlight .vi { color: #3333bb } /* Name.Variable.Instance */
.highlight .il { color: #0000DD; font-weight: bold } /* Literal.Number.Integer.Long */",
 
@".highlight  { 
	background:none repeat scroll 0 0 #000;
	padding:0.4em;
	margin-bottom:0.6em;
	overflow:auto;
	-moz-border-radius:6px;
	-webkit-border-radius:6px;
	border-radius:6px;
	font-family: Inconsolata, Consolas, Courier;
	}


.highlight .hll { background-color: #49483e }
.highlight  { background: #272822; color: #f8f8f2 }
.highlight .c { color: #75715e } /* Comment */
.highlight .err { color: #960050; background-color: #1e0010 } /* Error */
.highlight .k { color: #66d9ef } /* Keyword */
.highlight .l { color: #ae81ff } /* Literal */
.highlight .n { color: #f8f8f2 } /* Name */
.highlight .o { color: #f92672 } /* Operator */
.highlight .p { color: #f8f8f2 } /* Punctuation */
.highlight .cm { color: #75715e } /* Comment.Multiline */
.highlight .cp { color: #75715e } /* Comment.Preproc */
.highlight .c1 { color: #75715e } /* Comment.Single */
.highlight .cs { color: #75715e } /* Comment.Special */
.highlight .ge { font-style: italic } /* Generic.Emph */
.highlight .gs { font-weight: bold } /* Generic.Strong */
.highlight .kc { color: #66d9ef } /* Keyword.Constant */
.highlight .kd { color: #66d9ef } /* Keyword.Declaration */
.highlight .kn { color: #f92672 } /* Keyword.Namespace */
.highlight .kp { color: #66d9ef } /* Keyword.Pseudo */
.highlight .kr { color: #66d9ef } /* Keyword.Reserved */
.highlight .kt { color: #66d9ef } /* Keyword.Type */
.highlight .ld { color: #e6db74 } /* Literal.Date */
.highlight .m { color: #ae81ff } /* Literal.Number */
.highlight .s { color: #e6db74 } /* Literal.String */
.highlight .na { color: #a6e22e } /* Name.Attribute */
.highlight .nb { color: #f8f8f2 } /* Name.Builtin */
.highlight .nc { color: #a6e22e } /* Name.Class */
.highlight .no { color: #66d9ef } /* Name.Constant */
.highlight .nd { color: #a6e22e } /* Name.Decorator */
.highlight .ni { color: #f8f8f2 } /* Name.Entity */
.highlight .ne { color: #a6e22e } /* Name.Exception */
.highlight .nf { color: #a6e22e } /* Name.Function */
.highlight .nl { color: #f8f8f2 } /* Name.Label */
.highlight .nn { color: #f8f8f2 } /* Name.Namespace */
.highlight .nx { color: #a6e22e } /* Name.Other */
.highlight .py { color: #f8f8f2 } /* Name.Property */
.highlight .nt { color: #f92672 } /* Name.Tag */
.highlight .nv { color: #f8f8f2 } /* Name.Variable */
.highlight .ow { color: #f92672 } /* Operator.Word */
.highlight .w { color: #f8f8f2 } /* Text.Whitespace */
.highlight .mf { color: #ae81ff } /* Literal.Number.Float */
.highlight .mh { color: #ae81ff } /* Literal.Number.Hex */
.highlight .mi { color: #ae81ff } /* Literal.Number.Integer */
.highlight .mo { color: #ae81ff } /* Literal.Number.Oct */
.highlight .sb { color: #e6db74 } /* Literal.String.Backtick */
.highlight .sc { color: #e6db74 } /* Literal.String.Char */
.highlight .sd { color: #e6db74 } /* Literal.String.Doc */
.highlight .s2 { color: #e6db74 } /* Literal.String.Double */
.highlight .se { color: #ae81ff } /* Literal.String.Escape */
.highlight .sh { color: #e6db74 } /* Literal.String.Heredoc */
.highlight .si { color: #e6db74 } /* Literal.String.Interpol */
.highlight .sx { color: #e6db74 } /* Literal.String.Other */
.highlight .sr { color: #e6db74 } /* Literal.String.Regex */
.highlight .s1 { color: #e6db74 } /* Literal.String.Single */
.highlight .ss { color: #e6db74 } /* Literal.String.Symbol */
.highlight .bp { color: #f8f8f2 } /* Name.Builtin.Pseudo */
.highlight .vc { color: #f8f8f2 } /* Name.Variable.Class */
.highlight .vg { color: #f8f8f2 } /* Name.Variable.Global */
.highlight .vi { color: #f8f8f2 } /* Name.Variable.Instance */
.highlight .il { color: #ae81ff } /* Literal.Number.Integer.Long */",


@".highlight  { 
	background:none repeat scroll 0 0 #000;
	padding:0.4em;
	margin-bottom:0.6em;
	overflow:auto;
	-moz-border-radius:6px;
	-webkit-border-radius:6px;
	border-radius:6px;
	font-family: Inconsolata, Consolas, Courier;
	}

.highlight .hll{background-color:#e6e1dc;}
.highlight .c{color:#bc9458;xfont-style:italic;}
.highlight .err{color:#ffc66d;}
.highlight .k{color:#F60;}
.highlight .n{color:#f0f0f0;}
.highlight .o{color:#f0f0f0;}
.highlight .p{color:#f0f0f0;}
.highlight .cm{color:#bc9458;xfont-style:italic;}
.highlight .cp{color:#bc9458;xfont-style:italic;}
.highlight .c1{color:#bc9458;xfont-style:italic;}
.highlight .cs{color:#bc9458;xfont-style:italic;}
.highlight .kc{color:#cc7833;}
.highlight .kd{color:#cc7833;}
.highlight .kn{color:#cc7833;}
.highlight .kp{color:#cc7833;}
.highlight .kr{color:#da4939;}
.highlight .kt{color:#5a647e;}
.highlight .m{color:#a5c261;}
.highlight .s{color:#a5c261;}
.highlight .na{color:#da4939;}
.highlight .nb{color:#FC0;}
.highlight .nc{color:#ffc66d;}
.highlight .no{color:#6d9cbe;}
.highlight .nd{color:#da4939;}
.highlight .ni{color:#f0f0f0;}
.highlight .ne{color:#f0f0f0;}
.highlight .nf{color:#FC0;}
.highlight .nl{color:#f0f0f0;}
.highlight .nn{color:#f0f0f0;}
.highlight .nx{color:#f0f0f0;}
.highlight .py{color:#f0f0f0;}
.highlight .nt{color:#e8bf6a;}
.highlight .nv{color:#f0f0f0;}
.highlight .ow{color:#cc7833;}
.highlight .w{color:#f0f0f0;}
.highlight .mf{color:#a5c261;}
.highlight .mh{color:#a5c261;}
.highlight .mi{color:#a5c261;}
.highlight .mo{color:#a5c261;}
.highlight .sb{color:#a5c261;}
.highlight .sc{color:#a5c261;}
.highlight .sd{color:#a5c261;}
.highlight .s2{color:#6F0;}
.highlight .se{color:#da4939;}
.highlight .sh{color:#a5c261;}
.highlight .si{color:#a5c261;}
.highlight .sx{color:#FFF;}
.highlight .sr{color:#a5c261;}
.highlight .s1{color:#a5c261;}
.highlight .ss{color:#399;}
.highlight .bp{color:#f0f0f0;}
.highlight .vc{color:#f0f0f0;}
.highlight .vg{color:#f0f0f0;}
.highlight .vi{color:#f0f0f0;}
.highlight .il{color:#a5c261;}
s
"
            };
            

        private static readonly string[] Domains =
            ".aero .asia .biz .cat .com .coop .edu .gov .info .int .jobs .mil .mobi .museum .name .net .org .pro .tel .travel .xxx .ac .ad .ae .af .ag .ai .al .am .an .ao .aq .ar .as .at .au .aw .ax .az .ba .bb .bd .be .bf .bg .bh .bi .bj .bm .bn .bo .br .bs .bt .bv .bw .by .bz .ca .cc .cd .cf .cg .ch .ci .ck .cl .cm .cn .co .cr .cu .cv .cx .cy .cz .de .dj .dk .dm .do .dz .ec .ee .eg .er .es .et .eu .fi .fj .fk .fm .fo .fr .ga .gb .gd .ge .gf .gg .gh .gi .gl .gm .gn .gp .gq .gr .gs .gt .gu .gw .gy .hk .hm .hn .hr .ht .hu .id .ie .il .im .in .io .iq .ir .is .it .je .jm .jo .jp .ke .kg .kh .ki .km .kn .kp .kr .kw .ky .kz .la .lb .lc .li .lk .lr .ls .lt .lu .lv .ly .ma .mc .md .me .mg .mh .mk .ml .mm .mn .mo .mp .mq .mr .ms .mt .mu .mv .mw .mx .my .mz .na .nc .ne .nf .ng .ni .nl .no .np .nr .nu .nz .om .pa .pe .pf .pg .ph .pk .pl .pm .pn .pr .ps .pt .pw .py .qa .re .ro .rs .ru .rw .sa .sb .sc .sd .se .sg .sh .si .sj .sk .sl .sm .sn .so .sr .st .su .sv .sy .sz .tc .td .tf .tg .th .tj .tk .tl .tm .tn .to .tp .tr .tt .tv .tw .tz .ua .ug .uk .us .uy .uz .va .vc .ve .vg .vi .vn .vu .wf .ws .ye .yt .za .zm .zw"
                .Split(' ');

        private IContainer components;
        private MenuItem exitContextMenuItem;
        private SettingsForm settingsForm;
        private LanguageChooser languageChooserForm;
        private NotifyIcon notifyIcon;
        private ContextMenu notifyIconContextMenu;
        private MenuItem showContextMenuItem;
        private Dispatcher dispatcher;

        private SystemHotkey quickUploaderHotkey;
        private SystemHotkey quickSourceHotkey;
        private SystemHotkey bitlyHotkey;

        private ClipboardNotifier clipboardNotifier;
        private string currentMsg;
        private SoundPlayer beep;
        private SoundPlayer smallBeep;
        private SoundPlayer errorBeep;
        private readonly Dictionary<string, string> bitlyCache = new Dictionary<string, string>();

        private static bool AudioCues {
            get { return RegistryView.ApplicationUser["#enable-audio-cues"].BoolValue; }
        }

        private static bool AutoBitly {
            get { return RegistryView.ApplicationUser["#enable-auto-bitly"].BoolValue; }
        }

        public QtAppContext() {
            InitializeContext();
        }

        protected void Invoke(Action action) {
            dispatcher.Invoke(action);
        }

        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
        }

        protected override void ExitThreadCore() {
            if (settingsForm != null) {
                settingsForm.Close();
            }
            if (languageChooserForm != null) {
                languageChooserForm.Close();
            }
            base.ExitThreadCore();
        }

        private FTP Connect() {
            try {
                var ftp = new FTP {
                    Server = RegistryView.ApplicationUser["#ftp-server"].StringValue,
                    User = RegistryView.ApplicationUser["#ftp-username"].StringValue,
                    Password = RegistryView.ApplicationUser["#ftp-password"].EncryptedStringValue
                };
                ftp.Connect();
                return ftp;
            }
            catch {
                Invoke(() => {
                    if (AudioCues) {
                        errorBeep.Play();
                    }
                    ShowBalloonTip("Unable to upload file", "Couldn't log into ftp server", ToolTipIcon.Error);
                });

                return null;
            }
        }

        private void InitializeContext() {
            components = new Container();
            notifyIconContextMenu = new ContextMenu();

            showContextMenuItem = new MenuItem {Index = 0, Text = "&Settings", DefaultItem = true};
            showContextMenuItem.Click += (x, y) => ShowSettingsForm();

            exitContextMenuItem = new MenuItem {Index = 1, Text = "&Exit"};
            exitContextMenuItem.Click += (x, y) => ExitThread();

            notifyIconContextMenu.MenuItems.AddRange(new[] {showContextMenuItem, exitContextMenuItem});

            notifyIcon = new NotifyIcon(components) {
                ContextMenu = notifyIconContextMenu,
                Icon = new Icon(typeof (QtAppContext), "App.ico"),
                Text = "QuickTool - clipboard enhancements",
                Visible = true
            };

            notifyIcon.DoubleClick += (x, y) => ShowSettingsForm();

            clipboardNotifier = new ClipboardNotifier();
            clipboardNotifier.Changed += (x, y) => ClipboardChanged();

            dispatcher = Dispatcher.CurrentDispatcher;

            quickSourceHotkey = new SystemHotkey { Shortcut = RegistryView.ApplicationUser["#quick-source-hotkey"].GetEnumValue<Keys>() };
            quickUploaderHotkey = new SystemHotkey { Shortcut = RegistryView.ApplicationUser["#quick-uploader-hotkey"].GetEnumValue<Keys>() };
            bitlyHotkey = new SystemHotkey { Shortcut = RegistryView.ApplicationUser["#manual-bitly-hotkey"].GetEnumValue<Keys>() };

            quickUploaderHotkey.Pressed += ((x, y) => UploadClipboardContents());
            bitlyHotkey.Pressed += ((x, y) => {
                var dataObject = (DataObject) Clipboard.GetDataObject();
                if (dataObject != null && dataObject.GetDataPresent(DataFormats.Text)) {
                    BitlyUrl(dataObject.GetData(DataFormats.Text) as string);
                }
            });

            quickSourceHotkey.Pressed += ((x, y) => {
                if (languageChooserForm.Visible) {
                    return;
                }

                var dataObject = (DataObject) Clipboard.GetDataObject();

                if (dataObject != null && dataObject.GetDataPresent(DataFormats.Text)) {
                    languageChooserForm.clipSource = dataObject.GetText();
                    languageChooserForm.Activate();
                    languageChooserForm.Show();
                }
            });

            languageChooserForm = new LanguageChooser {Icon = notifyIcon.Icon};

            languageChooserForm.VisibleChanged += (x, y) => {
                if (!languageChooserForm.Visible && languageChooserForm.ok) {
                    var t = languageChooserForm.theme;
                    if( t > Themes.Length ) {
                        t = 0;
                    }
                    UploadSourceFromClipboard(t, languageChooserForm.brushName, languageChooserForm.clipSource);
                }
            };

            notifyIcon.BalloonTipClicked += (x, y) => {
                try {
                    var uri = new Uri(currentMsg);
                    Process.Start(uri.AbsoluteUri);
                }
                catch {
                }
            };
            settingsForm = new SettingsForm {Icon = notifyIcon.Icon};

            settingsForm.VisibleChanged += (x, y) => {
                if (!settingsForm.Visible) {
                    quickUploaderHotkey.Shortcut = RegistryView.ApplicationUser["#quick-uploader-hotkey"].GetEnumValue<Keys>();
                    quickSourceHotkey.Shortcut = RegistryView.ApplicationUser["#quick-source-hotkey"].GetEnumValue<Keys>();
                    bitlyHotkey.Shortcut = RegistryView.ApplicationUser["#manual-bitly-hotkey"].GetEnumValue<Keys>();
                }
            };

            var a = Assembly.GetExecutingAssembly();
            beep = new SoundPlayer(a.GetManifestResourceStream("QuickTool.sounds.NewBeep.wav"));
            smallBeep = new SoundPlayer(a.GetManifestResourceStream("QuickTool.sounds.SmallBeep.wav"));
            errorBeep = new SoundPlayer(a.GetManifestResourceStream("QuickTool.sounds.Error.wav"));
        }

        public void ShowBalloonTip(string title, string text, ToolTipIcon toolTipIcon) {
            notifyIcon.ShowBalloonTip(1000, title, text, toolTipIcon);
            currentMsg = text;
        }

        private void ShowSettingsForm() {
            // settingsForm.Location = new Point(Screen.PrimaryScreen.WorkingArea.Width - settingsForm.Width, Screen.PrimaryScreen.WorkingArea.Height - settingsForm.Height);
            quickUploaderHotkey.Shortcut = Keys.None;
            quickSourceHotkey.Shortcut = Keys.None;
            bitlyHotkey.Shortcut = Keys.None;

            settingsForm.Activate();
            settingsForm.Show();
        }

        private void UploadSourceFromClipboard(int theme, string brushName, string clipSource) {

            const string htmlTemplate = "<html><head>\r\n<style>\r\n{0}\r\n</style></head>\r\n<body>\r\n{1}\r\n</body></html>";
            // <link href=""http://coapp.org/styles/syntax.css"" rel=""stylesheet"" type=""text/css"">
            Task.Factory.StartNew(() => {

                // make call to http://pygments.appspot.com to with lang=brushName and code=$code to get html
                var request = (HttpWebRequest) WebRequest.Create("http://pygments.appspot.com");
                request.Method = "POST";
                
                request.BeginGetRequestStream( xx => {
                    var postStream = request.EndGetRequestStream(xx);
                    var postArray = Encoding.UTF8.GetBytes("lang={0}&code={1}".format(brushName.UrlEncode(), clipSource.UrlEncode()));
                    postStream.Write(postArray, 0, postArray.Length );
                    postStream.Close();

                    request.BeginGetResponse( x => {
                        try {
                            var response = (HttpWebResponse)request.EndGetResponse(x);
                            using (var stream = response.GetResponseStream()) {
                                using (var sr = new StreamReader(stream, System.Text.Encoding.GetEncoding("utf-8"))) {
                                    var html = htmlTemplate.format(Themes[theme],sr.ReadToEnd());

                                    try {
                                        var ftp = Connect();
                                        if (ftp == null) {
                                            return;
                                        }

                                        ftp.ChangeDir(RegistryView.ApplicationUser["#ftp-folder"].StringValue);
                                        var remoteFilename =
                                            Path.ChangeExtension(RegistryView.ApplicationUser["#image-filename-template"].StringValue.FormatFilename(), "html");

                                        using (var stream2 = new MemoryStream(Encoding.UTF8.GetBytes(html))) {
                                            stream2.Seek(0, SeekOrigin.Begin);
                                            ftp.UploadAndComplete(stream2, stream2.Length, remoteFilename, false);
                                        }

                                        Invoke(
                                            () => {
                                                var finishedUrl = RegistryView.ApplicationUser["#image-finishedurl-template"].StringValue.format(remoteFilename);
                                                ShowBalloonTip("Text uploaded", finishedUrl, ToolTipIcon.Info);
                                                try {
                                                    if (AudioCues) {
                                                        beep.Play();
                                                    }
                                                    Clipboard.SetDataObject(finishedUrl, true, 3, 100);
                                                } catch {
                                                    /* suppress  */
                                                }
                                            });
                                    } catch {
                                        /* suppress  */
                                    }

                                }
                            }
                        } catch {
                            if (AudioCues) {
                                errorBeep.Play();
                            }
                            ShowBalloonTip("Unable to get shortened URL", "Did you set your Bit.ly credentials?",
                                ToolTipIcon.Error);
                        }
                        }, request);

                }, request);
            });
        }

        private string lastData;

        private void ClipboardChanged() {
            var dataObject = (DataObject) Clipboard.GetDataObject();

            if (dataObject == null) {
                return;
            }

            if (dataObject.ContainsImage()) {
                ShowBalloonTip("Helpful Hint",
                    "Press " + RegistryView.ApplicationUser["#quick-uploader-hotkey"].StringValue + " to upload image to FTP",
                    ToolTipIcon.Info);
            }

            if (!dataObject.GetDataPresent(DataFormats.Text)) {
                return;
            }
            var data = dataObject.GetData(DataFormats.Text) as string;
            if (lastData == data) {
                return;
            }

            lastData = data;
            if (AutoBitly) {
                BitlyUrl(data);
            }
        }

        private void BitlyUrl(string data) {
            lastData = data;
            if (data.Contains("tinyurl.com") || data.Contains("@") || data.Contains("bit.ly") || data.Contains("j.mp") || data.Length < 16) {
                return;
            }

            Uri uri = null;
            try {
                uri = new Uri(data);
            }
            catch (Exception) {
                try {
                    if (data.Contains(".")) {
                        uri = new Uri("http://" + data);
                    }
                }
                catch {
                    /* suppress */
                }
            }
            if (uri != null) {
                if (bitlyCache.ContainsKey(uri.AbsoluteUri)) {
                    Clipboard.SetDataObject(bitlyCache[uri.AbsoluteUri], true, 3, 100);
                    if (AudioCues) {
                        smallBeep.Play();
                    }
                    return;
                }

                if (Domains.Contains((uri.DnsSafeHost.Substring(uri.DnsSafeHost.LastIndexOf('.'))))) {
                    var bitly = new UriBuilder("http", "api.bit.ly", 80, "/v3/shorten");
                    bitly.Query = string.Format("format={0}&longUrl={1}&domain={2}&login={3}&apiKey={4}", "txt",
                        Uri.EscapeDataString(uri.AbsoluteUri), "j.mp",
                        RegistryView.ApplicationUser["#bit.ly-username"].StringValue,
                        RegistryView.ApplicationUser["#bit.ly-password"].EncryptedStringValue);

                    var request = (HttpWebRequest) WebRequest.Create(bitly.Uri);
                    request.BeginGetResponse(x => {
                        try {
                            var response = (HttpWebResponse) request.EndGetResponse(x);
                            var stream = response.GetResponseStream();
                            var buffer = new byte[8192];

                            stream.BeginRead(buffer, 0, 8192, y => {
                                try {
                                    int read = stream.EndRead(y);
                                    string newUrl = Encoding.ASCII.GetString(buffer, 0, read).Trim();
                                    if (!bitlyCache.ContainsKey(uri.AbsoluteUri)) {
                                        bitlyCache.Add(uri.AbsoluteUri, newUrl);
                                    }
                                    Invoke(() => {
                                        Clipboard.SetDataObject(newUrl, true, 3, 100);
                                        ShowBalloonTip("URL Shrunk with Bit.ly", newUrl, ToolTipIcon.Info);
                                        if (AudioCues) {
                                            smallBeep.Play();
                                        }
                                    });
                                }
                                catch {
                                    /* suppress */
                                }
                            }, null);
                        }
                        catch {
                            if (AudioCues) {
                                errorBeep.Play();
                            }
                            ShowBalloonTip("Unable to get shortened URL", "Did you set your Bit.ly credentials?",
                                ToolTipIcon.Error);
                        }
                    }, null);
                }
                return;
            }
        }

        private void UploadClipboardContents() {
            try {
                var dataObject = Clipboard.GetDataObject() as DataObject;
                if (dataObject == null) {
                    return;
                }

                Stream stream = null;
                IEnumerable<string> filenames = null;
                string remoteFilename = string.Empty;
                int filesUploaded = 0;

                if (dataObject.ContainsText()) {
                    var clipText = dataObject.GetText();
                    if (File.Exists(clipText)) {
                        filenames = new List<string> {clipText};
                    }
                    else {
                        if (AudioCues) {
                            errorBeep.Play();
                        }
                        ShowBalloonTip("Unable to upload file", "Clipboard does not contain image data or file(s)",
                            ToolTipIcon.Error);
                        return;
                    }
                }
                else if (dataObject.GetDataPresent(DataFormats.FileDrop)) {
                    filenames = (IEnumerable<string>) dataObject.GetData(DataFormats.FileDrop);
                }
                else if (dataObject.ContainsImage()) {
                    using (var img = dataObject.GetImage()) {
                        stream = new MemoryStream();
                        img.Save(stream, Path.GetExtension(RegistryView.ApplicationUser["#image-filename-template"].StringValue)
                            .Equals(".jpg", StringComparison.CurrentCultureIgnoreCase)
                            ? ImageFormat.Jpeg
                            : ImageFormat.Png);

                        stream.Seek(0, SeekOrigin.Begin);
                    }
                }
                else {
                    ShowBalloonTip("Unable to upload file", "Clipboard does not contain image data or file(s)",
                        ToolTipIcon.Error);
                    return;
                }

                Task.Factory.StartNew(() => {
                    try {
                        var ftp = Connect();
                        if (ftp == null) {
                            return;
                        }

                        ftp.ChangeDir(RegistryView.ApplicationUser["#ftp-folder"].StringValue);

                        if (stream != null) {
                            remoteFilename = RegistryView.ApplicationUser["#image-filename-template"].StringValue.FormatFilename();
                            ftp.OpenUpload(stream, stream.Length, remoteFilename, false);
                            ftp.DoUploadUntilComplete();
                            stream.Close();
                            stream.Dispose();
                        }
                        if (filenames != null) {
                            string html =
                                @"<html><body>Files: <br/><table border='0' ><tr><th><u>File</u></th><th width=50></th><th><u>Size</u></th></tr>";
                            foreach (var file in filenames) {
                                if (Directory.Exists(file)) {
                                    foreach (var subFile in Directory.GetFiles(file, "*", SearchOption.AllDirectories)) {
                                        var subPath = Path.GetDirectoryName(file).GetSubPath(subFile);
                                        var subFolder = Path.GetDirectoryName(subPath);
                                        foreach (var folder in subFolder.Split(new[] {'\\'}, StringSplitOptions.RemoveEmptyEntries)) {
                                            ftp.ChangeDirMakeIfNeccesary(folder);
                                        }
                                        remoteFilename = Path.GetFileName(subFile);
                                        ftp.OpenUpload(subFile, remoteFilename, false);
                                        ftp.DoUploadUntilComplete();
                                        filesUploaded++;

                                        remoteFilename = Path.Combine(subFolder, Path.GetFileName(subFile)).Replace('\\', '/');

                                        html +=
                                            @"<tr><td><a href=""{0}"" >{1}</a></td><td></td><td align='right'>{2:n0}</td></tr>".
                                                format(
                                                     RegistryView.ApplicationUser["#image-finishedurl-template"].StringValue.format(remoteFilename),
                                                    remoteFilename, new FileInfo(subFile).Length);

                                        ftp.ChangeDir("/");
                                        ftp.ChangeDir(RegistryView.ApplicationUser["#ftp-folder"].StringValue);
                                    }
                                }
                                else {
                                    try {
                                        remoteFilename = Path.GetFileName(file);
                                        ftp.OpenUpload(file, remoteFilename, false);
                                        ftp.DoUploadUntilComplete();
                                        filesUploaded++;

                                        html +=
                                            @"<tr><td><a href=""{0}"" >{1}</a></td><td></td><td align='right'>{2:n0}</td></tr>".
                                                format(
                                                     RegistryView.ApplicationUser["#image-finishedurl-template"].StringValue.format(remoteFilename),
                                                    remoteFilename, new FileInfo(file).Length);
                                    }
                                    catch {
                                        /* suppress */
                                    }
                                }
                            }
                            if (filesUploaded > 1) {
                                html += @"</table></body></html>";
                                remoteFilename =
                                    Path.ChangeExtension(RegistryView.ApplicationUser["#image-filename-template"].StringValue.FormatFilename(),
                                        "html");

                                try {
                                    stream = new MemoryStream(Encoding.UTF8.GetBytes(html));
                                    ftp.OpenUpload(stream, stream.Length, remoteFilename, false);
                                    ftp.DoUploadUntilComplete();
                                    stream.Close();
                                    stream.Dispose();
                                }
                                catch {
                                    /* suppress */
                                }
                            }
                        }
                        ftp.Disconnect();
                    }
                    catch {
                        /* suppress */
                    }
                    Invoke(() => {
                        var finishedUrl = RegistryView.ApplicationUser["#image-finishedurl-template"].StringValue.format(remoteFilename);
                        ShowBalloonTip("Image uploaded", finishedUrl, ToolTipIcon.Info);
                        try {
                            Clipboard.SetDataObject(finishedUrl, true, 3, 100);
                        }
                        catch {
                            /* suppress */
                        }
                    });
                });
            }
            catch (Exception exc) {
                if (AudioCues) {
                    errorBeep.Play();
                }
                Invoke(() => ShowBalloonTip("Unexpected Error", exc.Message, ToolTipIcon.Info));
            }
        }

        [STAThread]
        private static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new QtAppContext());
        }
    }
}