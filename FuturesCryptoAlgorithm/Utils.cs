using Binance.Net.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoAlgorithm
{
    public class Utils
    {
        private static FrmMain frmMain = null;
        private static string LOG_FILEPATH = "log.txt";

        private static List<string> lstFileLogQueue = new List<string>();
        public static bool bWorking = true;
        private static object fileLocker = new object();

        public static int SYMBOL_COUNT = 10;


        public struct GlobalSettings
        {
            public string apiKey;
            public string secretKey;
            public bool useTestNet;

            public bool allowTrade;
            public bool autoQuantity;
        }

        public struct SymbolSetting
        {
            public string product;
            public int timeFrameIndex;
            public decimal quantity;
            public int supportTimeFrameIndex;
            public int buyUpTicks;
            public int sellDownTicks;
            public decimal gainProfit;
            public decimal openPrice;
            public bool chartOnly;
            public int leverage;
            public int maLongPeriod;
            public int maShortPeriod;
            public int maSupportPeriod;
            public int maVolumePeriod;
            public int maPriceTimeLimit;
            public int maVolumeTimeLimit;
        }

        public static GlobalSettings global_settings = new GlobalSettings();
        private static List<SymbolSetting> lstSymbolSettings = new List<SymbolSetting>();

        public static void setMainForm(FrmMain form)
        {
            frmMain = form;
            new Thread(writeThread).Start();
        }

        public static void addLog(string message)
        {
            string time = DateTime.Now.ToString("MM/dd HH:mm:ss");
            string msgWithTime = time + ": " + message + "\r\n";
            Console.WriteLine(msgWithTime);

            frmMain.addFormLog(msgWithTime);

            lock (fileLocker)
            {
                lstFileLogQueue.Add(msgWithTime);
            }
        }

        public static void writeThread()
        {
            while (bWorking)
            {
                Thread.Sleep(10);
                string msgWithTime = "";
                lock (fileLocker)
                {
                    if (lstFileLogQueue.Count > 0)
                    {
                        msgWithTime = lstFileLogQueue[0];
                        lstFileLogQueue.RemoveAt(0);
                    }
                }
                if (msgWithTime.Length == 0)
                    continue;

                try
                {
                    File.AppendAllText(Path.Combine(Directory.GetCurrentDirectory(), LOG_FILEPATH), msgWithTime);
                }
                catch (Exception ex)
                {
                    lock (fileLocker)
                    {
                        lstFileLogQueue.Clear();
                    }
                    if (bWorking)
                        frmMain.addFormLog(ex.Message);
                }
            }
        }

        public static void LoadSettings()
        {
            global_settings.useTestNet = IniSetting.GetGlobalIntSetting("Setting", "TestNet", 0) == 1;
            global_settings.apiKey = IniSetting.GetGlobalStringSetting("Setting", "ApiKey", "");
            global_settings.secretKey = IniSetting.GetGlobalStringSetting("Setting", "SecretKey", "");
            global_settings.allowTrade = IniSetting.GetGlobalIntSetting("Setting", "AllowTrade", 0) == 1;
            global_settings.autoQuantity = IniSetting.GetGlobalIntSetting("Setting", "AutoQuantity", 0) == 1;

            for (int i = 0; i < SYMBOL_COUNT * 2; i++)
            {
                string section = "";
                if (i >= SYMBOL_COUNT)
                    section = $"TradeSymbol_Margin{i - SYMBOL_COUNT + 1}";
                else
                    section = $"TradeSymbol_Spot{i + 1}";

                SymbolSetting setting = new SymbolSetting();

                setting.product = IniSetting.GetGlobalStringSetting(section, "Product", "");
                setting.timeFrameIndex = IniSetting.GetGlobalIntSetting(section, "TimeFrameIndex", 6);
                setting.quantity = (decimal)IniSetting.GetGlobalDoubleSetting(section, "Quantity", 1000);
                setting.supportTimeFrameIndex = IniSetting.GetGlobalIntSetting(section, "SupportTimeFrameIndex", 7);
                setting.maLongPeriod = IniSetting.GetGlobalIntSetting(section, "MALongPeriod", 21);
                setting.maShortPeriod = IniSetting.GetGlobalIntSetting(section, "MAShortPeriod", 13);
                setting.maSupportPeriod = IniSetting.GetGlobalIntSetting(section, "MASupportPeriod", 200);
                setting.maVolumePeriod = IniSetting.GetGlobalIntSetting(section, "MAVolumePeriod", 14);
                setting.maPriceTimeLimit = IniSetting.GetGlobalIntSetting(section, "MAPriceTimeLimit", 3);
                setting.maVolumeTimeLimit = IniSetting.GetGlobalIntSetting(section, "MAVolumeTimeLimit", 5);
                setting.buyUpTicks = IniSetting.GetGlobalIntSetting(section, "BuyUpTicks", 2);
                setting.sellDownTicks = IniSetting.GetGlobalIntSetting(section, "SellDownTicks", 1);
                setting.gainProfit = (decimal)IniSetting.GetGlobalDoubleSetting(section, "GainProfit", 3);
                setting.openPrice = (decimal)IniSetting.GetGlobalDoubleSetting(section, "OpenPrice", 0);
                setting.chartOnly = IniSetting.GetGlobalIntSetting(section, "ChartOnly", 0) == 1;
                setting.leverage = IniSetting.GetGlobalIntSetting(section, "Leverage", 20);

                lstSymbolSettings.Add(setting);
            }
        }

        public static void SaveSettings()
        {
            IniSetting.WriteGlobalStringSetting("Setting", "TestNet", global_settings.useTestNet ? "1" : "0");
            IniSetting.WriteGlobalStringSetting("Setting", "ApiKey", global_settings.apiKey);
            IniSetting.WriteGlobalStringSetting("Setting", "SecretKey", global_settings.secretKey);
            IniSetting.WriteGlobalStringSetting("Setting", "AllowTrade", global_settings.allowTrade ? "1" : "0");
            IniSetting.WriteGlobalStringSetting("Setting", "AutoQuantity", global_settings.autoQuantity ? "1" : "0");

            for (int i = 0; i < SYMBOL_COUNT * 2; i++)
            {
                string section = "";
                if (i >= SYMBOL_COUNT)
                    section = $"TradeSymbol_Margin{i - SYMBOL_COUNT + 1}";
                else
                    section = $"TradeSymbol_Spot{i + 1}";

                SymbolSetting setting = lstSymbolSettings[i];

                IniSetting.WriteGlobalStringSetting(section, "Product", setting.product);
                IniSetting.WriteGlobalStringSetting(section, "TimeFrameIndex", setting.timeFrameIndex.ToString());
                IniSetting.WriteGlobalStringSetting(section, "Quantity", setting.quantity.ToString());
                IniSetting.WriteGlobalStringSetting(section, "SupportTimeFrameIndex", setting.supportTimeFrameIndex.ToString());
                IniSetting.WriteGlobalStringSetting(section, "MALongPeriod", setting.maLongPeriod.ToString());
                IniSetting.WriteGlobalStringSetting(section, "MAShortPeriod", setting.maShortPeriod.ToString());
                IniSetting.WriteGlobalStringSetting(section, "MASupportPeriod", setting.maSupportPeriod.ToString());
                IniSetting.WriteGlobalStringSetting(section, "MAVolumePeriod", setting.maVolumePeriod.ToString());
                IniSetting.WriteGlobalStringSetting(section, "MAPriceTimeLimit", setting.maPriceTimeLimit.ToString());
                IniSetting.WriteGlobalStringSetting(section, "MAVolumeTimeLimit", setting.maVolumeTimeLimit.ToString());
                IniSetting.WriteGlobalStringSetting(section, "BuyUpTicks", setting.buyUpTicks.ToString());
                IniSetting.WriteGlobalStringSetting(section, "SellDownTicks", setting.sellDownTicks.ToString());
                IniSetting.WriteGlobalStringSetting(section, "GainProfit", setting.gainProfit.ToString());
                IniSetting.WriteGlobalStringSetting(section, "OpenPrice", setting.openPrice.ToString());
                IniSetting.WriteGlobalStringSetting(section, "ChartOnly", setting.chartOnly ? "1" : "0");
                IniSetting.WriteGlobalStringSetting(section, "Leverage", setting.leverage.ToString());
            }
        }
        
        public static SymbolSetting GetSymbolSetting(int index)
        {
            return lstSymbolSettings[index];
        }

        public static void SetSymbolSetting(int index, SymbolSetting setting)
        {
            lstSymbolSettings[index] = setting;
        }

        public static int GetSymbolIndex(string symbol)
        {
            for (int i = 0; i < 0 + SYMBOL_COUNT; i++)
            {
                if (lstSymbolSettings[i].product == symbol)
                    return i;
            }
            return -1;
        }

        public static void SetSymbolSetting(string symbol, SymbolSetting setting)
        {
            for (int i = 0; i < SYMBOL_COUNT; i++)
            {
                if (lstSymbolSettings[i].product == symbol)
                    lstSymbolSettings[i] = setting;
            }
        }

        public static SymbolSetting GetSymbolSetting(string symbol)
        {
            for (int i = 0; i < SYMBOL_COUNT; i++)
            {
                if (lstSymbolSettings[i].product == symbol)
                    return lstSymbolSettings[i];
            }
            return new SymbolSetting();
        }

        public static KlineInterval GetKlineInterval(int index)
        {
            KlineInterval[] intervals = new KlineInterval[] {
                KlineInterval.OneMinute,
                KlineInterval.ThreeMinutes,
                KlineInterval.FiveMinutes,
                KlineInterval.FifteenMinutes,
                KlineInterval.ThirtyMinutes,
                KlineInterval.OneHour,
                KlineInterval.TwoHour,
                KlineInterval.FourHour,
                KlineInterval.SixHour,
                KlineInterval.TwelveHour,
                KlineInterval.OneDay,
                KlineInterval.ThreeDay,
                KlineInterval.OneWeek
            };

            if (index < 0 || index >= intervals.Length)
                return KlineInterval.TwoHour;

            return intervals[index];
        }

        public static string GetDecimalString(decimal? dVal)
        {
            if (dVal == null)
            {
                return "";
            }
            string str = dVal.ToString();
            if (str.Contains(".") == false)
                return str;

            str = str.TrimEnd("0".ToCharArray());
            if (str.EndsWith("."))
                str = str.Remove(str.Length - 1);
            return str;
        }

        public static string ToKMB(decimal num)
        {
            if (num > 999999999 || num < -999999999)
            {
                return num.ToString("0,,,.###B", CultureInfo.InvariantCulture);
            }
            else
            if (num > 999999 || num < -999999)
            {
                return num.ToString("0,,.##M", CultureInfo.InvariantCulture);
            }
            else
            if (num > 999 || num < -999)
            {
                return num.ToString("0,.#K", CultureInfo.InvariantCulture);
            }
            else
            {
                return num.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
