namespace CoApp.Bootstrapper {
    using System;
    using System.Text;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;

    public static class Logger {
        private static EventLog _eventLog;
        private static readonly string Source;
        private static bool _ready;
        public static bool Errors { get; set; }
        public static bool Warnings { get; set; }
        public static bool Messages { get; set; }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        internal static extern void OutputDebugString(string message);

        static Logger() {
            Errors = true;
            Warnings = true;
            Messages = true;

            Source = "managed-bootstrap";
            if( String.IsNullOrEmpty(Source)) {
                Source = "CoApp (misc)";
            }
            // 
            try {
                if (!EventLog.SourceExists(Source)) {
                    EventLog.CreateEventSource(Source, "CoApp");
                }
                _eventLog = new EventLog("CoApp", ".", Source);

                Task.Factory.StartNew(() => {
                    while (!EventLog.SourceExists(Source)) {
                        Thread.Sleep(20);
                    }
                    _ready = true;
                });
            } catch {
                // if the process isn't elevated, we're not going to be able to create the event log to use the event logging.
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Writes an entry of the specified type with the
        ///       user-defined <paramref name="eventID"/> and <paramref name="category"/> to the event log, and appends binary data to 
        ///       the message. The Event Viewer does not interpret this data; it
        ///       displays raw data only in a combined hexadecimal and text format. 
        ///    </para> 
        /// </devdoc>
        private static void WriteEntry(string message, EventLogEntryType type = EventLogEntryType.Information, int eventID = 0, short category = 0, byte[] rawData = null) {
            if (!_ready) {
                Task.Factory.StartNew(() => {
                    for (var i = 0; i < 100 && !_ready; i++) {
                        Thread.Sleep(50);
                    }
                    // go ahead and try, but don't whine if this gets dropped. 
                    try {
                        _eventLog.WriteEntry(message, type, eventID, category, rawData);
                    } catch {

                    }
                });
            } else {
                _eventLog.WriteEntry(message, type, eventID, category, rawData);
            }

            // we're gonna output this to dbgview too for now.
            if (eventID == 0 && category == 0) {
                OutputDebugString(string.Format("«{0}/{1}»-{2}", Source, type, message.Replace("\r\n", "\r\n»")));
            } else {
                OutputDebugString(string.Format("«{0}/{1}»({2}/{3})-{4}", Source, type, eventID, category, message.Replace("\r\n", "\r\n»")));
            }
            
            if( rawData != null && rawData.Length > 0  ) {
                var rd = Encoding.UTF8.GetString(rawData);
                if( !string.IsNullOrEmpty(rd) && rd.Length < 2048 ) {
                    OutputDebugString("   »RawData:" + rd);
                } else {
                    OutputDebugString("   »RawData is [] bytes" + rawData.Length);
                }
            }
        } 

        public static void Message(string message, params object[] args) {
            if (Messages) {
                WriteEntry(string.Format(message, args));
            }
        }

        public static void MessageWithData(string message, string data, params object[] args) {
            if (Messages) {
                WriteEntry(string.Format(message, args),rawData : Encoding.UTF8.GetBytes(data));
            }
        }

        public static void Warning(string message, params object[] args) {
            if (Warnings) {
                WriteEntry(string.Format(message, args), EventLogEntryType.Warning);
            }
        }

        public static void WarningWithData(string message, string data, params object[] args) {
            if (Warnings) {
                WriteEntry(string.Format(message, args), EventLogEntryType.Warning, rawData: Encoding.UTF8.GetBytes(data));
            }
        }

        public static void Warning(Exception exception) {
            if (Warnings) {
                if (exception.InnerException != null) {
                    WriteEntry(string.Format("{0}/{1} - {2}", exception.GetType(), exception.InnerException.GetType(), exception.Message), EventLogEntryType.Warning, 0, 0, Encoding.UTF8.GetBytes(exception.StackTrace));
                } else {
                    WriteEntry(string.Format("{0} - {1}", exception.GetType(), exception.Message), EventLogEntryType.Warning, 0, 0, Encoding.UTF8.GetBytes(exception.StackTrace));
                }
            }
        }
       
        public static void Error(string message, params object[] args) {
            if (Errors) {
                WriteEntry(string.Format(message,(args)), EventLogEntryType.Error);
            }
        }

        public static void ErrorWithData(string message, string data, params object[] args) {
            if (Errors) {
                WriteEntry(string.Format(message,args ), EventLogEntryType.Error, rawData: Encoding.UTF8.GetBytes(data));
            }
        }

        public static void Error(Exception exception) {
            if (Errors) {
                if (exception.InnerException != null) {
                    WriteEntry(string.Format("{0}/{1} - {2}", exception.GetType(), exception.InnerException.GetType(), exception.Message), EventLogEntryType.Error, 0, 0, Encoding.UTF8.GetBytes(exception.StackTrace));
                } else {
                    WriteEntry(string.Format("{0} - {1}",exception.GetType(), exception.Message), EventLogEntryType.Error, 0, 0, Encoding.UTF8.GetBytes(exception.StackTrace));
                }
            }
        }
    }
}
