using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CryptoAlgorithm
{
    public class IniSetting
    {
        public static string SETTING_INI = "Setting.ini";

        [DllImport("kernel32.dll", EntryPoint = "GetPrivateProfileString")]
        private static extern int GetPrivateProfileString(string SectionName, string KeyName, string Default, StringBuilder Return_StringBuilder_Name, int Size, string FileName);

        [DllImport("kernel32.dll", EntryPoint = "WritePrivateProfileString")]
        private static extern int WritePrivateProfileString(string SectionName, string KeyName, string value, string FileName);

        public static void WriteGlobalStringSetting(string section, string key, string value)
        {
            string iniPath = Path.Combine(Directory.GetCurrentDirectory(), SETTING_INI);
            WritePrivateProfileString(section, key, value, iniPath);
        }

        public static string GetGlobalStringSetting(string section, string key, string defaultValue)
        {
            string iniPath = Path.Combine(Directory.GetCurrentDirectory(), SETTING_INI);
            var RetVal = new StringBuilder(255);
            GetPrivateProfileString(section, key, defaultValue, RetVal, 255, iniPath);
            return RetVal.ToString();
        }

        public static int GetGlobalIntSetting(string section, string key, int defaultValue)
        {
            int ret = defaultValue;
            var RetVal = GetGlobalStringSetting(section, key, defaultValue.ToString());
            int.TryParse(RetVal, out ret);
            return ret;
        }

        public static double GetGlobalDoubleSetting(string section, string key, double defaultValue)
        {
            double ret = defaultValue;
            var RetVal = GetGlobalStringSetting(section, key, defaultValue.ToString());
            double.TryParse(RetVal, out ret);
            return ret;
        }
    }
}
