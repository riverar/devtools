

namespace QuickTool {
    using System;
    using System.ComponentModel;
    using System.Windows.Forms;

    public class ClipboardNotifier : Component {
        public event EventHandler Changed;
        private IntPtr nextWindow;

         public ClipboardNotifier(IContainer container) : this() {
             container.Add(this);
         }

         public ClipboardNotifier() {
             if (!DesignMode) {
                 NativeWindowWithEvent.Instance.ProcessMessage+=MessageEvent;
                 nextWindow = User32.SetClipboardViewer(NativeWindowWithEvent.Instance.Handle);
             }
         }
 
         protected override void Dispose(bool disposing){
             NativeWindowWithEvent.Instance.ProcessMessage-=MessageEvent;
             User32.ChangeClipboardChain(NativeWindowWithEvent.Instance.Handle, nextWindow);
             base.Dispose(disposing);
         }

         protected void MessageEvent(object sender, ref Message m, ref bool handled) {   //Handle WM_Hotkey event
             if(handled)
                 return;

             switch( (Msgs)m.Msg ) {
                 case Msgs.WM_DRAWCLIPBOARD:
                    handled = true;
 
                    if(Changed != null)
                        Changed(this, EventArgs.Empty);
                 
                    User32.SendMessage(nextWindow, m.Msg, m.WParam, m.LParam);
                    
                    break;

                 case Msgs.WM_CHANGECBCHAIN:
                    if(m.WParam == nextWindow) {
                        nextWindow = m.LParam;
                    }
                    else {
                        User32.SendMessage(nextWindow, m.Msg, m.WParam, m.LParam);
                    }
                    break;
             }
         }
    }
}
