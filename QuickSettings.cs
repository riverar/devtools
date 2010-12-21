using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Win32;

namespace QuickTool {
    public class QuickSettings {

        private static QuickSettings instance;
        private string assemblyname;
        private QuickSettings(string name) {
            assemblyname = name;
        }

        public static QuickSettings Instance {
            get { return instance ?? (instance = new QuickSettings(Assembly.GetEntryAssembly().GetName().ToString())); }
        }

        public string this[string settingName] {
            get {
                RegistryKey regkey = null;
                string result = null;
                try {
                    regkey = Registry.CurrentUser.CreateSubKey(@"Software\CoApp\" + assemblyname);

                    if (null == regkey)
                        return null;

                    result = regkey.GetValue(settingName, null) as string;
                }
                catch {
                }
                finally {
                    if (null != regkey)
                        regkey.Close();
                }
                return result;
            }
            set {
                RegistryKey regkey = null;
                try {
                    regkey = Registry.CurrentUser.CreateSubKey(@"Software\CoApp\" + assemblyname);

                    if (null == regkey)
                        return;

                    regkey.SetValue(settingName, value); 
                }
                catch {
                }
                finally {
                    if (null != regkey)
                        regkey.Close();
                }
            }
        }
    }

}
