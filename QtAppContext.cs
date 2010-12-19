using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace QuickTool {
    using System.ComponentModel;
    using System.Drawing;
    using System.Net;

    public class QtAppContext : ApplicationContext {
        private static string[] domains = ".aero .asia .biz .cat .com .coop .edu .gov .info .int .jobs .mil .mobi .museum .name .net .org .pro .tel .travel .xxx .ac .ad .ae .af .ag .ai .al .am .an .ao .aq .ar .as .at .au .aw .ax .az .ba .bb .bd .be .bf .bg .bh .bi .bj .bm .bn .bo .br .bs .bt .bv .bw .by .bz .ca .cc .cd .cf .cg .ch .ci .ck .cl .cm .cn .co .cr .cu .cv .cx .cy .cz .de .dj .dk .dm .do .dz .ec .ee .eg .er .es .et .eu .fi .fj .fk .fm .fo .fr .ga .gb .gd .ge .gf .gg .gh .gi .gl .gm .gn .gp .gq .gr .gs .gt .gu .gw .gy .hk .hm .hn .hr .ht .hu .id .ie .il .im .in .io .iq .ir .is .it .je .jm .jo .jp .ke .kg .kh .ki .km .kn .kp .kr .kw .ky .kz .la .lb .lc .li .lk .lr .ls .lt .lu .lv .ly .ma .mc .md .me .mg .mh .mk .ml .mm .mn .mo .mp .mq .mr .ms .mt .mu .mv .mw .mx .my .mz .na .nc .ne .nf .ng .ni .nl .no .np .nr .nu .nz .om .pa .pe .pf .pg .ph .pk .pl .pm .pn .pr .ps .pt .pw .py .qa .re .ro .rs .ru .rw .sa .sb .sc .sd .se .sg .sh .si .sj .sk .sl .sm .sn .so .sr .st .su .sv .sy .sz .tc .td .tf .tg .th .tj .tk .tl .tm .tn .to .tp .tr .tt .tv .tw .tz .ua .ug .uk .us .uy .uz .va .vc .ve .vg .vi .vn .vu .wf .ws .ye .yt .za .zm .zw".Split(' ');
        private IContainer components;
        private MenuItem exitContextMenuItem;
        private Form mainForm;
        private NotifyIcon notifyIcon;
        private ContextMenu notifyIconContextMenu;
        private MenuItem showContextMenuItem;

        private SystemHotkey TranslateUrlHotkey;
        private ClipboardNotifier clipboardNotifier;

        public QtAppContext() {
            InitializeContext();
        }

        protected override void Dispose(bool disposing) {
            if(disposing && (components != null)) {
                components.Dispose();
            }
        }

        protected override void ExitThreadCore() {
            if(mainForm != null) {
                mainForm.Close();
            }
            base.ExitThreadCore();
        }

        private void InitializeContext() {
            components = new Container();
            notifyIconContextMenu = new ContextMenu();

            showContextMenuItem = new MenuItem {Index = 0, Text = "&Show History", DefaultItem = true};
            showContextMenuItem.Click += (x, y) => ShowForm();

            exitContextMenuItem = new MenuItem {Index = 1, Text = "&Exit"};
            exitContextMenuItem.Click += (x, y) => ExitThread();

            notifyIconContextMenu.MenuItems.AddRange(new[] { showContextMenuItem, exitContextMenuItem });

            notifyIcon = new NotifyIcon(components) {
                ContextMenu = notifyIconContextMenu, Icon = new Icon(typeof(QtAppContext), "App.ico"), Text = DateTime.Now.ToLongDateString(), Visible = true
            };

            notifyIcon.DoubleClick +=  (x, y) => ShowForm();

            clipboardNotifier = new ClipboardNotifier();
            clipboardNotifier.Changed += (x, y) => ClipboardChanged();

            // TranslateUrlHotkey = new SystemHotkey {Shortcut = Keys.Control | Keys.Alt | Keys.NumPad7};
            // TranslateUrlHotkey.Pressed += ((x,y) => { MessageBox.Show("hi", "there"); });

        }

        public void ShowBalloonTip( string title, string text, ToolTipIcon toolTipIcon) {
            notifyIcon.ShowBalloonTip(1000, title, text,toolTipIcon);
        }

        private void ShowForm() {
            mainForm.Location = new Point(Screen.PrimaryScreen.WorkingArea.Width - mainForm.Width, Screen.PrimaryScreen.WorkingArea.Height - mainForm.Height);
            mainForm.Activate();
            mainForm.Show();
        }

        private void ClipboardChanged() {
            IDataObject dataObject = new DataObject();
            dataObject = Clipboard.GetDataObject();
            if(!dataObject.GetDataPresent(DataFormats.Text)) {
                return;
            }
            string data = dataObject.GetData(DataFormats.Text) as string;

            if(data.Contains("tinyurl.com") || data.Contains("bit.ly") || data.Contains("j.mp")) {
                return;
            }
            Uri uri = null;
            try {
                uri = new Uri(data);
            }
            catch(Exception) {
                try {
                    uri = new Uri("http://" + data);
                }
                catch(Exception) {

                }
            }
            if( uri != null) {
                if(domains.Contains((uri.DnsSafeHost.Substring(uri.DnsSafeHost.LastIndexOf('.'))))) {
                    //   $connectURL = 'http://api.bit.ly/v3/shorten?login='.$login.'&apiKey='.$appkey.'&uri='.urlencode($url).'&format='.$format;
                    //   return curl_get_result($connectURL)
                    
                    var bitly = new UriBuilder("http", "api.bit.ly", 80, "/v3/shorten");
                    bitly.Query = string.Format("format={0}&longUrl={1}&domain={2}&login={3}&apiKey={4}","txt", Uri.EscapeDataString(uri.AbsoluteUri), "j.mp", "gserack","R_03a0de7ed7140fe80cb44acef099ece0");

                    var request = (HttpWebRequest)WebRequest.Create(bitly.Uri);
                    request.BeginGetResponse((x) => {
                        try {
                            var response = (HttpWebResponse)request.EndGetResponse(x);
                            var stream = response.GetResponseStream();
                            var buffer = new byte[8192];

                            stream.BeginRead(buffer, 0, 8192, (y) => {
                                try {
                                    int read = stream.EndRead(y);
                                    string newURL = Encoding.ASCII.GetString(buffer, 0, read);
                                    // MessageBox.Show(newURL, "Short URL");
                                    System.Windows.Threading
                                    Clipboard.SetDataObject(newURL);
                                    ShowBalloonTip("New URL", newURL, ToolTipIcon.Info);
                                } catch { }
                            }, null);
                        } catch {}
                    },null);
                }


                return;

            }
        }

        [STAThread]
        private static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var context = new QtAppContext();
            context.mainForm = new Form1();
            context.mainForm.Icon = context.notifyIcon.Icon;
            Application.Run(context);
        }
    }
}
