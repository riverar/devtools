//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011  Garrett Serack. All rights reserved.
// </copyright>
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
            get { return QuickSettings.BoolSetting["enable-audio-cues"]; }
        }

        private static bool AutoBitly {
            get { return QuickSettings.BoolSetting["enable-auto-bitly"]; }
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
                    Server = QuickSettings.StringSetting["ftp-server"],
                    User = QuickSettings.StringSetting["ftp-username"],
                    Password = QuickSettings.EncryptedStringSetting["ftp-password"]
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

            quickSourceHotkey = new SystemHotkey {Shortcut = QuickSettingsEnum<Keys>.Setting["quick-source-hotkey"]};
            quickUploaderHotkey = new SystemHotkey {Shortcut = QuickSettingsEnum<Keys>.Setting["quick-uploader-hotkey"]};
            bitlyHotkey = new SystemHotkey {Shortcut = QuickSettingsEnum<Keys>.Setting["manual-bitly-hotkey"]};

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
                    UploadSourceFromClipboard(languageChooserForm.brushFile, languageChooserForm.brushName, languageChooserForm.clipSource);
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
                    quickUploaderHotkey.Shortcut = QuickSettingsEnum<Keys>.Setting["quick-uploader-hotkey"];
                    quickSourceHotkey.Shortcut = QuickSettingsEnum<Keys>.Setting["quick-source-hotkey"];
                    bitlyHotkey.Shortcut = QuickSettingsEnum<Keys>.Setting["manual-bitly-hotkey"];
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

        private void UploadSourceFromClipboard(string brushFile, string brushName, string clipSource) {
            const string htmlTemplate =
                @"
<html><head>
<script type=""text/javascript"" src=""{0}scripts/shCore.js""></script>
<script type=""text/javascript"" src=""{0}scripts/shBrush{2}.js""></script>
<link href=""{0}styles/shCore.css"" rel=""stylesheet"" type=""text/css"" />
<link href=""{0}styles/shThemeDefault.css"" rel=""stylesheet"" type=""text/css"" />
</head><body>
<div style=""font-size:13px; margin:-5 0 0 -15; width:100%;""><script type=""syntaxhighlighter"" class=""brush: {1}""><![CDATA[
{3} ]]></script></div>
<script type=""text/javascript"">
SyntaxHighlighter.defaults['toolbar'] = false;
SyntaxHighlighter.all()
</script>
</body></html>";
            var pfxPath = QuickSettings.StringSetting["syntaxhighlighter-prefix-path"] ?? "";
            var html = htmlTemplate.format(pfxPath, brushName, brushFile,
                clipSource.Replace("]]>", "] ] >").Replace("</script>", "</scr ipt>"));

            CoTask.Factory.StartNew(() => {
                try {
                    var ftp = Connect();
                    if (ftp == null) {
                        return;
                    }

                    ftp.ChangeDir(QuickSettings.StringSetting["ftp-folder"]);
                    var remoteFilename =
                        Path.ChangeExtension(QuickSettings.StringSetting["image-filename-template"].FormatFilename(), "html");

                    using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(html))) {
                        stream.Seek(0, SeekOrigin.Begin);
                        ftp.UploadAndComplete(stream, stream.Length, remoteFilename, false);
                    }

                    Invoke(() => {
                        var finishedUrl = QuickSettings.StringSetting["image-finishedurl-template"].format(remoteFilename);
                        ShowBalloonTip("Text uploaded", finishedUrl, ToolTipIcon.Info);
                        try {
                            if (AudioCues) {
                                beep.Play();
                            }
                            Clipboard.SetDataObject(finishedUrl, true, 3, 100);
                        }
                        catch {
                            /* suppress  */
                        }
                    });
                }
                catch {
                    /* suppress  */
                }
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
                    "Press " + QuickSettings.StringSetting["quick-uploader-hotkey"] + " to upload image to FTP",
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
                        QuickSettings.StringSetting["bit.ly-username"],
                        QuickSettings.EncryptedStringSetting["bit.ly-password"]);

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
                        img.Save(stream, Path.GetExtension(QuickSettings.StringSetting["image-filename-template"])
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

                CoTask.Factory.StartNew(() => {
                    try {
                        var ftp = Connect();
                        if (ftp == null) {
                            return;
                        }

                        ftp.ChangeDir(QuickSettings.StringSetting["ftp-folder"]);

                        if (stream != null) {
                            remoteFilename = QuickSettings.StringSetting["image-filename-template"].FormatFilename();
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
                                                    QuickSettings.StringSetting["image-finishedurl-template"].format(remoteFilename),
                                                    remoteFilename, new FileInfo(subFile).Length);

                                        ftp.ChangeDir("/");
                                        ftp.ChangeDir(QuickSettings.StringSetting["ftp-folder"]);
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
                                                    QuickSettings.StringSetting["image-finishedurl-template"].format(remoteFilename),
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
                                    Path.ChangeExtension(QuickSettings.StringSetting["image-filename-template"].FormatFilename(),
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
                        var finishedUrl = QuickSettings.StringSetting["image-finishedurl-template"].format(remoteFilename);
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