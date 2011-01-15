//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011  Garrett Serack. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace QuickTool {
    using System;
    using System.Reflection;
    using System.Windows.Forms;
    using CoApp.Toolkit.Extensions;
    using Microsoft.Win32;

    public class SettingsStringIndexer {
        public string this[string settingName] {
            get { return QuickSettings.instance[settingName] as string ?? string.Empty; }
            set { QuickSettings.instance[settingName] = (value ?? string.Empty); }
        }
    }

    public class SettingsEncryptedStringIndexer {
        public string this[string settingName] {
            get {
                var bytes = QuickSettings.instance[settingName] as byte[];
                return bytes.Unprotect();
            }
            set {
                QuickSettings.instance[settingName] = value.Protect();
            }
        }
    }


    public class SettingsBooleanIndexer {
        public bool this[string settingName] {
            get {
                object value = QuickSettings.instance[settingName] as string ?? string.Empty;
                return (value.ToString().IsTrue() || value.ToString().Equals("1"));
            }
            set { QuickSettings.instance[settingName] = (value ? "true" : "false"); }
        }
    }

    public class SettingsIntIndexer {
        public int this[string settingName] {
            get {
                int result = 0;
                object value = QuickSettings.instance[settingName];

                if (value is int) {
                    return (int) value;
                }

                if (value is string) {
                    Int32.TryParse(value as string, out result);
                }

                return result;
            }
            set { QuickSettings.instance[settingName] = value; }
        }
    }

    public class SettingsLongIndexer {
        public long this[string settingName] {
            get {
                long result = 0;
                object value = QuickSettings.instance[settingName];

                if (value is long || value is int) {
                    return (long) value;
                }

                if (value is string) {
                    Int64.TryParse(value as string, out result);
                }

                return result;
            }
            set { QuickSettings.instance[settingName] = value; }
        }
    }

    public class SettingsEnumIndexer {
        public static Keys CastToKeysEnum(string name) {
            if (name.Contains("+")) {
                Keys result = Keys.None;
                foreach (var n in name.Split('+')) {
                    result |= CastToKeysEnum(n);
                }
                return result;
            }
            if (Enum.IsDefined(typeof (Keys), name)) {
                return (Keys) Enum.Parse(typeof (Keys), name);
            }
            return Keys.None;
        }
    }

    public class QuickSettings {
        internal static QuickSettings instance = new QuickSettings(Assembly.GetEntryAssembly().GetName().ToString());
        internal string assemblyname;

        internal QuickSettings(string name) {
            assemblyname = name;
        }

        internal object this[string settingName] {
            get {
                RegistryKey regkey = null;
                try {
                    regkey = Registry.CurrentUser.CreateSubKey(@"Software\CoApp\" + assemblyname);

                    if (null == regkey) {
                        return null;
                    }

                    return regkey.GetValue(settingName, null);
                }
                catch {
                }
                finally {
                    if (null != regkey) {
                        regkey.Close();
                    }
                }
                return null;
            }
            set {
                RegistryKey regkey = null;
                try {
                    regkey = Registry.CurrentUser.CreateSubKey(@"Software\CoApp\" + assemblyname);

                    if (null == regkey) {
                        return;
                    }

                    if (value is long) {
                        regkey.SetValue(settingName, value, RegistryValueKind.QWord);
                    }
                    else {
                        regkey.SetValue(settingName, value);
                    }
                }
                catch {
                }
                finally {
                    if (null != regkey) {
                        regkey.Close();
                    }
                }
            }
        }

        public static SettingsStringIndexer StringSetting = new SettingsStringIndexer();
        public static SettingsBooleanIndexer BoolSetting = new SettingsBooleanIndexer();
        public static SettingsIntIndexer IntSetting = new SettingsIntIndexer();
        public static SettingsLongIndexer LongSetting = new SettingsLongIndexer();
        public static SettingsEncryptedStringIndexer EncryptedStringSetting = new SettingsEncryptedStringIndexer();
    }

    public class QuickSettingsEnum<T> where T : struct, IComparable, IFormattable, IConvertible {
        public static QuickSettingsEnum<T> Setting = new QuickSettingsEnum<T>();

        public static T ParseEnum(string value, T defaultValue = default(T)) {
            if (Enum.IsDefined(typeof (T), value)) {
                return (T) Enum.Parse(typeof (T), value, true);
            }
            return defaultValue;
        }

        public static T CastToEnum(string value) {
            if (value.Contains("+")) {
                var values = value.Split('+');
                Type numberType = Enum.GetUnderlyingType(typeof (T));
                if (numberType.Equals(typeof (int))) {
                    int newResult = 0;
                    foreach (var val in values) {
                        newResult |= (int) (Object) ParseEnum(val);
                    }
                    return (T) (Object) newResult;
                }
            }
            return ParseEnum(value);
        }

        public static string CastToString(T value) {
            return Enum.Format(typeof (T), value, "G").Replace(", ", "+");
        }

        public T this[string settingName] {
            get { return CastToEnum(QuickSettings.StringSetting[settingName]); }
            set { QuickSettings.StringSetting[settingName] = CastToString(value); }
        }
    }
}