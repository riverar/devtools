using System;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Threading;
using CoApp.Toolkit.Extensions;

namespace QuickTool {
    using System.ComponentModel;
    using System.Drawing;
    using System.Net;

    public class QtAppContext : ApplicationContext {
        private static string[] domains = ".aero .asia .biz .cat .com .coop .edu .gov .info .int .jobs .mil .mobi .museum .name .net .org .pro .tel .travel .xxx .ac .ad .ae .af .ag .ai .al .am .an .ao .aq .ar .as .at .au .aw .ax .az .ba .bb .bd .be .bf .bg .bh .bi .bj .bm .bn .bo .br .bs .bt .bv .bw .by .bz .ca .cc .cd .cf .cg .ch .ci .ck .cl .cm .cn .co .cr .cu .cv .cx .cy .cz .de .dj .dk .dm .do .dz .ec .ee .eg .er .es .et .eu .fi .fj .fk .fm .fo .fr .ga .gb .gd .ge .gf .gg .gh .gi .gl .gm .gn .gp .gq .gr .gs .gt .gu .gw .gy .hk .hm .hn .hr .ht .hu .id .ie .il .im .in .io .iq .ir .is .it .je .jm .jo .jp .ke .kg .kh .ki .km .kn .kp .kr .kw .ky .kz .la .lb .lc .li .lk .lr .ls .lt .lu .lv .ly .ma .mc .md .me .mg .mh .mk .ml .mm .mn .mo .mp .mq .mr .ms .mt .mu .mv .mw .mx .my .mz .na .nc .ne .nf .ng .ni .nl .no .np .nr .nu .nz .om .pa .pe .pf .pg .ph .pk .pl .pm .pn .pr .ps .pt .pw .py .qa .re .ro .rs .ru .rw .sa .sb .sc .sd .se .sg .sh .si .sj .sk .sl .sm .sn .so .sr .st .su .sv .sy .sz .tc .td .tf .tg .th .tj .tk .tl .tm .tn .to .tp .tr .tt .tv .tw .tz .ua .ug .uk .us .uy .uz .va .vc .ve .vg .vi .vn .vu .wf .ws .ye .yt .za .zm .zw".Split(' ');

        

        private IContainer components;
        private MenuItem exitContextMenuItem;
        private Form settingsForm;
        private LanguageChooser languageChooserForm;
        private NotifyIcon notifyIcon;
        private ContextMenu notifyIconContextMenu;
        private MenuItem showContextMenuItem;
        private Dispatcher dispatcher;

        // private SystemHotkey TranslateUrlHotkey;
        private SystemHotkey quickUploaderHotkey;
        private SystemHotkey quickSourceHotkey;

        private ClipboardNotifier clipboardNotifier;
        private string currentMsg;

        public QtAppContext() {
            InitializeContext();
        }

        public static Keys CastToEnum(string name) {
            if (name.Contains("+")) {
                Keys result = Keys.None;
                foreach (var n in name.Split('+'))
                    result |= CastToEnum(n);
                return result;
            }
            if (Enum.IsDefined(typeof(Keys), name)) {
                return (Keys)Enum.Parse(typeof(Keys), name);
            }
            return Keys.None;
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

        private void InitializeContext() {
            components = new Container();
            notifyIconContextMenu = new ContextMenu();

            showContextMenuItem = new MenuItem { Index = 0, Text = "&Settings", DefaultItem = true };
            showContextMenuItem.Click += (x, y) => ShowSettingsForm();

            exitContextMenuItem = new MenuItem { Index = 1, Text = "&Exit" };
            exitContextMenuItem.Click += (x, y) => ExitThread();

            notifyIconContextMenu.MenuItems.AddRange(new[] { showContextMenuItem, exitContextMenuItem });

            notifyIcon = new NotifyIcon(components) {
                ContextMenu = notifyIconContextMenu,
                Icon = new Icon(typeof(QtAppContext), "App.ico"),
                Text = "QuickTool - clipboard enhancements",
                Visible = true
            };
            
            notifyIcon.DoubleClick += (x, y) => ShowSettingsForm();

            clipboardNotifier = new ClipboardNotifier();
            clipboardNotifier.Changed += (x, y) => ClipboardChanged();

            dispatcher = Dispatcher.CurrentDispatcher;

//            TranslateUrlHotkey = new SystemHotkey { Shortcut = Keys.Control | Keys.Alt | Keys.NumPad7 };
//            TranslateUrlHotkey.Pressed += ((x, y) => { MessageBox.Show("hi", "there"); });


            quickSourceHotkey = new SystemHotkey { Shortcut = CastToEnum(QuickSettings.Instance["quick-source-hotkey"] ?? "Control+Alt+NumPad6") };
            quickUploaderHotkey = new SystemHotkey { Shortcut = CastToEnum(QuickSettings.Instance["quick-uploader-hotkey"] ?? "Control+Alt+NumPad9") };

                    
            quickUploaderHotkey.Pressed += ((x, y) => { UploadClipboardImage(); });
            quickSourceHotkey.Pressed += ((x, y) => {
                if (languageChooserForm.Visible)
                    return;

                DataObject dataObject = (DataObject)Clipboard.GetDataObject();

                if (!dataObject.GetDataPresent(DataFormats.Text)) {
                    return;
                }

                languageChooserForm.clipSource = dataObject.GetText();

                languageChooserForm.Activate();
                languageChooserForm.Show();
            });

            languageChooserForm = new LanguageChooser() { Icon = notifyIcon.Icon };

            languageChooserForm.VisibleChanged += (x, y) => {
                if (!languageChooserForm.Visible && languageChooserForm.ok) {
                    UploadSourceFromClipboard(languageChooserForm.brushFile, languageChooserForm.brushName, languageChooserForm.clipSource);
                }
            };

            notifyIcon.BalloonTipClicked += (x, y) => {
                try {
                    var uri = new Uri(currentMsg);
                    Process.Start(uri.AbsoluteUri);
                } catch { }
            };
            settingsForm = new SettingsForm { Icon = notifyIcon.Icon };

            settingsForm.VisibleChanged += (x, y) => {
                if (!settingsForm.Visible) {
                    quickUploaderHotkey.Shortcut =
                        CastToEnum(QuickSettings.Instance["quick-uploader-hotkey"] ?? "Control+Alt+NumPad9");
                    quickSourceHotkey.Shortcut =
                        CastToEnum(QuickSettings.Instance["quick-source-hotkey"] ?? "Control+Alt+NumPad6");
                }
            };

            

        }

        public void ShowBalloonTip(string title, string text, ToolTipIcon toolTipIcon) {
            notifyIcon.ShowBalloonTip(1000, title, text, toolTipIcon);
            currentMsg = text;
            
        }

        private void ShowSettingsForm() {
            // settingsForm.Location = new Point(Screen.PrimaryScreen.WorkingArea.Width - settingsForm.Width, Screen.PrimaryScreen.WorkingArea.Height - settingsForm.Height);
            quickUploaderHotkey.Shortcut = Keys.None;
            quickSourceHotkey.Shortcut = Keys.None;

            settingsForm.Activate();
            settingsForm.Show();
        }

        private void UploadSourceFromClipboard(string brushFile, string brushName, string clipSource) {
var htmlTemplate = @"
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
</body></html>"; // {0} prefix directory , {1} == language , {2} Brush Suffix., {3} source code.
            var pfxPath = QuickSettings.Instance["syntaxhighlighter-prefix-path"] ?? "";
            var HTML = htmlTemplate.format(pfxPath, brushName, brushFile, clipSource.Replace("]]>","] ] >").Replace("</script>","</scr ipt>"));
            
            new Task(() => {

                FTP ftp = null;
                try {
                    ftp = new FTP {
                                      Server = QuickSettings.Instance["ftp-server"],
                                      User = QuickSettings.Instance["ftp-username"],
                                      Password = QuickSettings.Instance["ftp-password"]
                                  };
                    ftp.Connect();
                }
                catch {
                    Invoke(
                        () =>
                        ShowBalloonTip("Unable to upload file", "Couldn't log into ftp server", ToolTipIcon.Error));
                    return;
                }

                ftp.ChangeDir(QuickSettings.Instance["ftp-folder"]);
                var stream = new MemoryStream(Encoding.UTF8.GetBytes(HTML));
                stream.Seek(0, SeekOrigin.Begin);   

                var remoteFilename = Path.ChangeExtension(QuickSettings.Instance["image-filename-template"].FormatFilename(), "html");
                ftp.OpenUpload(stream, stream.Length, remoteFilename, false);
                ftp.DoUploadUntilComplete();
                stream.Close();
                stream.Dispose();

                Invoke(() => {
                    var finishedUrl = QuickSettings.Instance["image-finishedurl-template"].format(remoteFilename);
                    ShowBalloonTip("Text uploaded", finishedUrl, ToolTipIcon.Info);
                    Clipboard.SetDataObject(finishedUrl, true, 3, 100);
                });

            }).Start();
        }

        private void ClipboardChanged() {

            DataObject dataObject = (DataObject)Clipboard.GetDataObject();

            if (dataObject.ContainsImage()) {
                ShowBalloonTip("Helpful Hint", "Press ctrl-alt-numpad9 to upload image to FTP", ToolTipIcon.Info);
            }

            if (!dataObject.GetDataPresent(DataFormats.Text)) {
                return;
            }
            string data = dataObject.GetData(DataFormats.Text) as string;

            if (data.Contains("tinyurl.com") || data.Contains("@") || data.Contains("bit.ly") || data.Contains("j.mp") || data.Length < 16 ) {
                return;
            }
            Uri uri = null;
            try {
                uri = new Uri(data);
            }
            catch (Exception) {
                try {
                    uri = new Uri("http://" + data);
                }
                catch (Exception) {

                }
            }
            if (uri != null) {
                if (domains.Contains((uri.DnsSafeHost.Substring(uri.DnsSafeHost.LastIndexOf('.'))))) {
                    var bitly = new UriBuilder("http", "api.bit.ly", 80, "/v3/shorten");
                    bitly.Query = string.Format("format={0}&longUrl={1}&domain={2}&login={3}&apiKey={4}", "txt",
                                                Uri.EscapeDataString(uri.AbsoluteUri), "j.mp",
                                                QuickSettings.Instance["bit.ly-username"],
                                                QuickSettings.Instance["bit.ly-password"]);

                    var request = (HttpWebRequest)WebRequest.Create(bitly.Uri);
                    request.BeginGetResponse((x) => {
                        try {
                            var response = (HttpWebResponse)request.EndGetResponse(x);
                            var stream = response.GetResponseStream();
                            var buffer = new byte[8192];

                            stream.BeginRead(buffer, 0, 8192, y => {
                                try {
                                    int read = stream.EndRead(y);
                                    string newURL = Encoding.ASCII.GetString(buffer, 0, read);
                                    Invoke(() => {
                                        Clipboard.SetDataObject(newURL, true, 3, 100);
                                        ShowBalloonTip("URL Shrunk with Bit.ly", newURL, ToolTipIcon.Info);
                                     });
                                }
                                catch {
                                }
                            }, null);
                        }
                        catch {
                            ShowBalloonTip("Unable to get shortened URL", "Did you set your Bit.ly credentials?",
                                           ToolTipIcon.Error);
                        }
                    }, null);
                }
                return;

            }
        }

        private void UploadClipboardImage() {
            try {
                DataObject dataObject = Clipboard.GetDataObject() as DataObject;
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
                        if (Path.GetExtension(QuickSettings.Instance["image-filename-template"]).Equals(".jpg",StringComparison.CurrentCultureIgnoreCase))
                            img.Save(stream, ImageFormat.Jpeg);
                        else
                            img.Save(stream, ImageFormat.Png);

                        stream.Seek(0, SeekOrigin.Begin);
                    }
                }
                else {
                    ShowBalloonTip("Unable to upload file", "Clipboard does not contain image data or file(s)",
                                   ToolTipIcon.Error);
                    return;
                }

                new Task(() => {

                    FTP ftp = null;
                    try {
                        ftp = new FTP {
                            Server = QuickSettings.Instance["ftp-server"],
                            User = QuickSettings.Instance["ftp-username"],
                            Password = QuickSettings.Instance["ftp-password"]
                        };
                        ftp.Connect();
                    }
                    catch {
                        Invoke(
                            () =>
                            ShowBalloonTip("Unable to upload file", "Couldn't log into ftp server", ToolTipIcon.Error));
                        return;
                    }

                    ftp.ChangeDir(QuickSettings.Instance["ftp-folder"]);

                    if (stream != null) {
                        remoteFilename = QuickSettings.Instance["image-filename-template"].FormatFilename();
                        ftp.OpenUpload(stream, stream.Length, remoteFilename, false);
                        ftp.DoUploadUntilComplete();
                        stream.Close();
                        stream.Dispose();
                    }
                    if (filenames != null) {
                        string HTML =
                            @"<html><body>Files: <br/><table border='0' ><tr><th><u>File</u></th><th width=50></th><th><u>Size</u></th></tr>";
                        foreach (var file in filenames) {

                            if( Directory.Exists(file)) {
                                foreach(var subFile in Directory.GetFiles(file, "*", SearchOption.AllDirectories)) {
                                    var subPath = Path.GetDirectoryName(file).GetSubPath(subFile);
                                    var subFolder = Path.GetDirectoryName(subPath);
                                    foreach( var folder in subFolder.Split(new [] {'\\' },StringSplitOptions.RemoveEmptyEntries)) {
                                        ftp.ChangeDirMakeIfNeccesary(folder);
                                    }
                                    remoteFilename = Path.GetFileName(subFile);
                                    ftp.OpenUpload(subFile, remoteFilename, false);
                                    ftp.DoUploadUntilComplete();
                                    filesUploaded++;

                                    remoteFilename = Path.Combine( subFolder, Path.GetFileName(subFile)).Replace('\\','/');

                                    HTML +=
                                        @"<tr><td><a href=""{0}"" >{1}</a></td><td></td><td align='right'>{2:n0}</td></tr>".
                                            format(
                                                QuickSettings.Instance["image-finishedurl-template"].format(remoteFilename),
                                                remoteFilename, new FileInfo(subFile).Length);
                                    
                                    ftp.ChangeDir("/");
                                    ftp.ChangeDir(QuickSettings.Instance["ftp-folder"]);
                                }
                                
                            }
                            else 
                                try {
                                    remoteFilename = Path.GetFileName(file);
                                    ftp.OpenUpload(file, remoteFilename, false);
                                    ftp.DoUploadUntilComplete();
                                    filesUploaded++;

                                    HTML +=
                                        @"<tr><td><a href=""{0}"" >{1}</a></td><td></td><td align='right'>{2:n0}</td></tr>".
                                            format(
                                                QuickSettings.Instance["image-finishedurl-template"].format(remoteFilename),
                                                remoteFilename, new FileInfo(file).Length);
                                }
                                catch {
                                }
                        }
                        if (filesUploaded > 1) {
                            HTML += @"</table></body></html>";
                            remoteFilename =
                                Path.ChangeExtension(QuickSettings.Instance["image-filename-template"].FormatFilename(),
                                                     "html");

                            try {
                                stream = new MemoryStream(Encoding.UTF8.GetBytes(HTML));
                                ftp.OpenUpload(stream, stream.Length, remoteFilename, false);
                                ftp.DoUploadUntilComplete();
                                stream.Close();
                                stream.Dispose();
                            }
                            catch {
                            }
                        }
                    }
                    ftp.Disconnect();

                    Invoke(() => {
                        var finishedUrl = QuickSettings.Instance["image-finishedurl-template"].format(remoteFilename);
                        ShowBalloonTip("Image uploaded", finishedUrl, ToolTipIcon.Info);
                        Clipboard.SetDataObject(finishedUrl, true, 3, 100);
                    });
                }).Start();

            } catch( Exception exc ) {
                Invoke(() => ShowBalloonTip("Unexpected Error", exc.Message , ToolTipIcon.Info));
            }
        }

        [STAThread]
        private static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var context = new QtAppContext();
            Application.Run(context);
        }
    }
}
