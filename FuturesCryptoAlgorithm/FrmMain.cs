using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Futures.FuturesData;
using Binance.Net.Objects.Futures.MarketData;
using Binance.Net.Objects.Futures.MarketStream;
using Binance.Net.Objects.Futures.UserStream;
using Binance.Net.Objects.Spot;
using Binance.Net.Objects.Spot.UserStream;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Logging;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace CryptoAlgorithm
{
    public partial class FrmMain : Form
    {
        private BinanceClient clientFuture = null;

        private BinanceSocketClient socketClientFuture = null;

        private string userStreamListenKey = "";

        private int gWorkingStatus = -1;
        private bool bLoadingForm = false;
        private bool bClosing = false;

        private string gErrMsg = "";
        private bool gErrorOccured = false;

        private DateTime gServerTime = DateTime.MinValue;

        private DateTime gSpotUpdateTime = DateTime.MinValue;
                
        private DateTime gLastChartTime = DateTime.MinValue;
        private int BAR_COUNT = 40;
        
        
        private object fileLocker = new object();
        private static List<string> lstTradeLogQueue = new List<string>();

        private DateTime gdtKeepAliveTime = DateTime.MinValue;

        private DateTime gSpotOrderStarted = DateTime.MinValue;
        private DateTime gFutureOrderStarted = DateTime.MinValue;

        private DateTime gSpotOrderFilled = DateTime.MinValue;
        private DateTime gFutureOrderFilled = DateTime.MinValue;

        private string STATUS_CHARTONLY = "CHARTONLY";
        private string STATUS_WAITSIGNAL = "Wait signal";

        private string[] TIMEFRAME_STRINGS = new string[] { "1M", "3M", "5M", "15M", "30M", "1H", "2H", "4H", "6H", "12H", "1D", "3D", "1W" };
        private bool gOrdering_Spot = false;
        
        private struct BidAskPrice
        {
            public decimal Bid;
            public decimal Ask;
        }

        // for margin and spot
        public struct BalanceItem
        {
            public string Asset;
            public decimal Balance;
        }
        private struct SettingsUI
        {
            public SettingsUI(TextBox product, ComboBox time, TextBox quantity, ComboBox supportTime,TextBox malongperiod, TextBox mashortperiod, TextBox masupportperiod, TextBox mavolumeperiod, 
                TextBox mapricetimelimit, TextBox mavolumetimelimit,
                TextBox gainpercent, CheckBox chartonly, Button update,
                Button sell, Button buy, Button cancel, TextBox leverage)
            {
                txtProduct = product;
                cmbTimeFrame = time;
                txtQuantity = quantity;
                cmbSupportTimeFrame = supportTime;
                txtMALongPeriod = malongperiod;
                txtMAShortPeriod = mashortperiod;
                txtMASupportPeriod = masupportperiod;
                txtMAVolumePeriod = mavolumeperiod;
                txtMAPriceTimelimit = mapricetimelimit;
                txtMAVolumeTimelimit = mavolumetimelimit;
                txtGainPercent = gainpercent;
                chkChartOnly = chartonly;
                btnUpdate = update;
                btnSell = sell;
                btnBuy = buy;
                btnCancel = cancel;
                txtLeverage = leverage;
            }

            public TextBox txtProduct;
            public ComboBox cmbTimeFrame;
            public TextBox txtQuantity;
            public ComboBox cmbSupportTimeFrame;
            public TextBox txtMALongPeriod;
            public TextBox txtMAShortPeriod;
            public TextBox txtMASupportPeriod;
            public TextBox txtMAVolumePeriod;
            public TextBox txtMAPriceTimelimit;
            public TextBox txtMAVolumeTimelimit;
            public TextBox txtGainPercent;
            public CheckBox chkChartOnly;
            public TextBox txtLeverage;

            public Button btnUpdate;
            public Button btnSell;
            public Button btnBuy;
            public Button btnCancel;
        }

        // ---- data
        private BinanceFuturesUsdtExchangeInfo gExchangeInfo;
        private List<BalanceItem> glstBalances = new List<BalanceItem>();
        private bool gBalanceUpdated = false;

        private Dictionary<string, List<IBinanceKline>> gdicKlines = new Dictionary<string, List<IBinanceKline>>();
        private Dictionary<string, List<IBinanceKline>> gdicSupportKlines = new Dictionary<string, List<IBinanceKline>>();
        private Dictionary<string, List<decimal>> gdicMALonglines = new Dictionary<string, List<decimal>>();
        private Dictionary<string, List<decimal>> gdicMAShortlines = new Dictionary<string, List<decimal>>();
        private Dictionary<string, List<decimal>> gdicMASupportlines = new Dictionary<string, List<decimal>>();
        private Dictionary<string, List<decimal>> gdicMAVolumelines = new Dictionary<string, List<decimal>>();
        private Dictionary<string, List<decimal>> gdicVollines = new Dictionary<string, List<decimal>>();
        private Dictionary<string, decimal> gdicSupportValues = new Dictionary<string, decimal>();
        private Dictionary<string, bool> gdicKlineUpdated = new Dictionary<string, bool>();
        private Dictionary<string, BidAskPrice> gdicPrices = new Dictionary<string, BidAskPrice>();

        private Dictionary<string, List<bool>> gdicMASignalCheck = new Dictionary<string, List<bool>>();
        private Dictionary<string, List<bool>> gdicMASupportSignalCheck = new Dictionary<string, List<bool>>();
        private Dictionary<string, List<bool>> gdicVolumeSignalCheck = new Dictionary<string, List<bool>>();
        private List<BinanceFuturesAccountPosition> glstPositions = new List<BinanceFuturesAccountPosition>();
        private bool gPositionUpdated = false;

        private List<BinanceFuturesOrder> glstOpenOrders = new List<BinanceFuturesOrder>();
        private bool gOrdersUpdated = false;

        private Dictionary<string, string> gdicStatus = new Dictionary<string, string>();
        private Dictionary<string, string> gdicPositionProfit = new Dictionary<string, string>();

        private Dictionary<string, DateTime> gdicLastOrderTime = new Dictionary<string, DateTime>();

        private Dictionary<string, bool> gdicWaitingBalance_Margin = new Dictionary<string, bool>();
        private Dictionary<string, bool> gdicNewCandle = new Dictionary<string, bool>();
        private Dictionary<string, bool> gdicOrderReceived = new Dictionary<string, bool>();     // true when receives after place order

        // ---- array
        Chart[] arrCharts;
        Label[] arrPriceLabels;
        Label[] arrVolumeLabels;
        Label[] arrPositionProfitLabels;
        Label[] arrSupportValueLabels;

        SettingsUI[] arrSettingsUI;

        public FrmMain()
        {
            InitializeComponent();
        }

        private void FrmMain_Load(object sender, EventArgs e)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            Utils.setMainForm(this);

            arrCharts = new Chart[] { chartRate1, chartRate2, chartRate3, chartRate4, chartRate5, chartRate6, chartRate7, chartRate8, chartRate9, chartRate10 };
            arrPriceLabels = new Label[] { labelPrice1, labelPrice2, labelPrice3, labelPrice4, labelPrice5, labelPrice6, labelPrice7, labelPrice8, labelPrice9, labelPrice10 };
            arrVolumeLabels = new Label[] { labelVolume1, labelVolume2, labelVolume3, labelVolume4, labelVolume5, labelVolume6, labelVolume7, labelVolume8, labelVolume9, labelVolume10 };
            arrSupportValueLabels = new Label[] { labelSupport1, labelSupport2, labelSupport3, labelSupport4, labelSupport5, labelSupport6, labelSupport7, labelSupport8, labelSupport9, labelSupport10 };
            arrPositionProfitLabels = new Label[] { labelPositionProfit1, labelPositionProfit2, labelPositionProfit3, labelPositionProfit4, labelPositionProfit5, labelPositionProfit6, labelPositionProfit7, labelPositionProfit8, labelPositionProfit9, labelPositionProfit10 };

            List<SettingsUI> lstUIs = new List<SettingsUI>();
            lstUIs.Add(new SettingsUI(txtProduct1, cmbTimeFrame1, txtQuantity1, cmbSupportTimeFrame1, txtMALongPeriod1, txtMAShortPeriod1, txtMASupportPeriod1, txtMAVolumePeriod1, txtMAPriceTimeLimit1, txtMAVolumeTimeLimit1, txtGainPercent1, chkChartOnly1, btnUpdateSetting1, btnSellMarket1, btnBuyMarket1, btnCancelAll1, txtLeverage1));
            lstUIs.Add(new SettingsUI(txtProduct2, cmbTimeFrame2, txtQuantity2, cmbSupportTimeFrame2, txtMALongPeriod2, txtMAShortPeriod2, txtMASupportPeriod2, txtMAVolumePeriod2, txtMAPriceTimeLimit2, txtMAVolumeTimeLimit2, txtGainPercent2, chkChartOnly2, btnUpdateSetting2, btnSellMarket2, btnBuyMarket2, btnCancelAll2, txtLeverage2));
            lstUIs.Add(new SettingsUI(txtProduct3, cmbTimeFrame3, txtQuantity3, cmbSupportTimeFrame3, txtMALongPeriod3, txtMAShortPeriod3, txtMASupportPeriod3, txtMAVolumePeriod3, txtMAPriceTimeLimit3, txtMAVolumeTimeLimit3, txtGainPercent3, chkChartOnly3, btnUpdateSetting3, btnSellMarket3, btnBuyMarket3, btnCancelAll3, txtLeverage3));
            lstUIs.Add(new SettingsUI(txtProduct4, cmbTimeFrame4, txtQuantity4, cmbSupportTimeFrame4, txtMALongPeriod4, txtMAShortPeriod4, txtMASupportPeriod4, txtMAVolumePeriod4, txtMAPriceTimeLimit4, txtMAVolumeTimeLimit4, txtGainPercent4, chkChartOnly4, btnUpdateSetting4, btnSellMarket4, btnBuyMarket4, btnCancelAll4, txtLeverage4));
            lstUIs.Add(new SettingsUI(txtProduct5, cmbTimeFrame5, txtQuantity5, cmbSupportTimeFrame5, txtMALongPeriod5, txtMAShortPeriod5, txtMASupportPeriod5, txtMAVolumePeriod5, txtMAPriceTimeLimit5, txtMAVolumeTimeLimit5, txtGainPercent5, chkChartOnly5, btnUpdateSetting5, btnSellMarket5, btnBuyMarket5, btnCancelAll5, txtLeverage5));
            lstUIs.Add(new SettingsUI(txtProduct6, cmbTimeFrame6, txtQuantity6, cmbSupportTimeFrame6, txtMALongPeriod6, txtMAShortPeriod6, txtMASupportPeriod6, txtMAVolumePeriod6, txtMAPriceTimeLimit6, txtMAVolumeTimeLimit6, txtGainPercent6, chkChartOnly6, btnUpdateSetting6, btnSellMarket6, btnBuyMarket6, btnCancelAll6, txtLeverage6));
            lstUIs.Add(new SettingsUI(txtProduct7, cmbTimeFrame7, txtQuantity7, cmbSupportTimeFrame7, txtMALongPeriod7, txtMAShortPeriod7, txtMASupportPeriod7, txtMAVolumePeriod7, txtMAPriceTimeLimit7, txtMAVolumeTimeLimit7, txtGainPercent7, chkChartOnly7, btnUpdateSetting7, btnSellMarket7, btnBuyMarket7, btnCancelAll7, txtLeverage7));
            lstUIs.Add(new SettingsUI(txtProduct8, cmbTimeFrame8, txtQuantity8, cmbSupportTimeFrame8, txtMALongPeriod8, txtMAShortPeriod8, txtMASupportPeriod8, txtMAVolumePeriod8, txtMAPriceTimeLimit8, txtMAVolumeTimeLimit8, txtGainPercent8, chkChartOnly8, btnUpdateSetting8, btnSellMarket8, btnBuyMarket8, btnCancelAll8, txtLeverage8));
            lstUIs.Add(new SettingsUI(txtProduct9, cmbTimeFrame9, txtQuantity9, cmbSupportTimeFrame9, txtMALongPeriod9, txtMAShortPeriod9, txtMASupportPeriod9, txtMAVolumePeriod9, txtMAPriceTimeLimit9, txtMAVolumeTimeLimit9, txtGainPercent9, chkChartOnly9, btnUpdateSetting9, btnSellMarket9, btnBuyMarket9, btnCancelAll9, txtLeverage9));
            lstUIs.Add(new SettingsUI(txtProduct10, cmbTimeFrame10, txtQuantity10, cmbSupportTimeFrame10, txtMALongPeriod10, txtMAShortPeriod10, txtMASupportPeriod10, txtMAVolumePeriod10, txtMAPriceTimeLimit10, txtMAVolumeTimeLimit10, txtGainPercent10, chkChartOnly10, btnUpdateSetting10, btnSellMarket10, btnBuyMarket10, btnCancelAll10, txtLeverage10));


            arrSettingsUI = lstUIs.ToArray();
            
            bLoadingForm = true;
            LoadGlobalSettings();
            bLoadingForm = false;
        }

        private delegate void delegateAddFormLog(string log);
        public void addFormLog(string log)
        {
            if (bClosing)
                return;
            if (this.InvokeRequired)
            {
                this.Invoke(new delegateAddFormLog(addFormLog), new object[] { log });
                return;
            }        

            txtLog.AppendText(log);
        }

        private void initBufferedData()
        {
            glstBalances.Clear();
            gBalanceUpdated = false;

            gdicKlines.Clear();
            gdicSupportKlines.Clear();
            gdicVollines.Clear();
            gdicSupportValues.Clear();
            gdicMALonglines.Clear();
            gdicMAShortlines.Clear();
            gdicMASupportlines.Clear();
            gdicMAVolumelines.Clear();
            gdicKlineUpdated.Clear();
            glstOpenOrders.Clear();
            gOrdersUpdated = false;

            gdicMASignalCheck.Clear();
            gdicMASupportSignalCheck.Clear();
            gdicVolumeSignalCheck.Clear();
            glstPositions.Clear();
            gPositionUpdated = false;

            gdicPrices.Clear();
            gdicStatus.Clear();
            gdicLastOrderTime.Clear();
            gdicWaitingBalance_Margin.Clear();
            gdicOrderReceived.Clear();
            gdicNewCandle.Clear();

            listView_Balances.Items.Clear();
            listView_OpenOrders.Items.Clear();

            for (int i = 0; i < arrPriceLabels.Count(); i++)
            {
                arrPriceLabels[i].Text = "--,--";
                arrPositionProfitLabels[i].Text = "----";
                arrSupportValueLabels[i].Text = "---";
                arrVolumeLabels[i].Text = "--,--";

            }
        }

        private void initChartData()
        {
            for(int i = 0; i < arrCharts.Count(); i++)
            {
                Chart chart = arrCharts.ElementAt(i);

                Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(i);

                if (symbol_setting.product.Length == 0)
                {
                    chart.Series[0].Name = "----";
                    chart.Series[1].Name = "LONG";
                    chart.Series[2].Name = "SHORT";
                    chart.Series[4].Name = "VOLUME MA";
                }
                else
                {
                    chart.Series[0].Name = symbol_setting.product;
                    chart.Series[1].Name = $"LONG({TIMEFRAME_STRINGS[symbol_setting.timeFrameIndex]}, {symbol_setting.maLongPeriod})";
                    chart.Series[2].Name = $"SHORT({TIMEFRAME_STRINGS[symbol_setting.timeFrameIndex]}, {symbol_setting.maShortPeriod})";
                    chart.Series[4].Name = $"VOLUME MA({TIMEFRAME_STRINGS[symbol_setting.timeFrameIndex]}, {symbol_setting.maVolumePeriod})";

                }
                for (int j = 0; j < chart.Series.Count; j++)
                {
                    chart.Series[j].Points.Clear();
                    //chartRate.Series[i].IsXValueIndexed = true;
                }

                chart.ChartAreas[0].AxisY.IsStartedFromZero = false;
                chart.ChartAreas[0].AxisY.LabelStyle.Format = "{0.###}";

                chart.ChartAreas[0].AxisX.LabelStyle.Format = "MM/dd HH:mm";

                chart.ChartAreas[0].AxisX.MajorGrid.LineColor = Color.Gainsboro;
                chart.ChartAreas[0].AxisY.MajorGrid.LineColor = Color.Gainsboro;

                chart.ChartAreas[1].AxisY.IsStartedFromZero = false;
                chart.ChartAreas[1].AxisY.LabelStyle.Format = "{0:#0,.#}K";
                chart.ChartAreas[1].AxisX.MajorGrid.LineColor = Color.Gainsboro;
                chart.ChartAreas[1].AxisY.MajorGrid.LineColor = Color.Gainsboro;
                chart.ChartAreas[1].AxisX.LabelStyle.Format = "MM/dd HH:mm";
            }
        }
        
        private void LoadGlobalSettings()
        {
            Utils.LoadSettings();

            chkUseTestNet.Checked = Utils.global_settings.useTestNet;
            txtApiKey.Text = Utils.global_settings.apiKey;
            txtSecretKey.Text = Utils.global_settings.secretKey;
            chkAllowTrade.Checked = Utils.global_settings.allowTrade;
            chkAutoQuantity.Checked = Utils.global_settings.autoQuantity;
            
            LoadSymbolSettings();
        }

        private void LoadSymbolSettings()
        {
            for (int i = 0; i < Utils.SYMBOL_COUNT; i++)
            {
                SettingsUI setting_ui = arrSettingsUI[i];
                Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(i);
                setting_ui.txtProduct.Text = symbol_setting.product;
                setting_ui.cmbTimeFrame.SelectedIndex = symbol_setting.timeFrameIndex;
                setting_ui.txtQuantity.Text = Utils.GetDecimalString(symbol_setting.quantity);
                setting_ui.cmbSupportTimeFrame.SelectedIndex = symbol_setting.supportTimeFrameIndex;
                setting_ui.txtMALongPeriod.Text = symbol_setting.maLongPeriod.ToString();
                setting_ui.txtMAShortPeriod.Text = symbol_setting.maShortPeriod.ToString();
                setting_ui.txtMASupportPeriod.Text = symbol_setting.maSupportPeriod.ToString();
                setting_ui.txtMAVolumePeriod.Text = symbol_setting.maVolumePeriod.ToString();
                setting_ui.txtMAVolumeTimelimit.Text = symbol_setting.maVolumeTimeLimit.ToString();
                setting_ui.txtMAPriceTimelimit.Text = symbol_setting.maPriceTimeLimit.ToString();
                setting_ui.txtGainPercent.Text = symbol_setting.gainProfit.ToString();
                setting_ui.chkChartOnly.Checked = symbol_setting.chartOnly;
                setting_ui.txtLeverage.Text = symbol_setting.leverage.ToString();
            }
        }
        
        private bool CheckAllSettingsUI()
        {
            bool error = false;
            for(int i = 0; i < Utils.SYMBOL_COUNT; i++)
            {
                error = CheckSettingsUI(i);
                if (error == false)
                    return false;
            }
            return true;
        }

        private bool CheckSettingsUI(int index)
        {
            SettingsUI setting_ui = arrSettingsUI[index];

            double quantity;
            if (double.TryParse(setting_ui.txtQuantity.Text, out quantity) == false || quantity <= 0)
                return false;

            double malongperiod;
            if (double.TryParse(setting_ui.txtMALongPeriod.Text, out malongperiod) == false)
                return false;

            double mashortperiod;
            if (double.TryParse(setting_ui.txtMAShortPeriod.Text, out mashortperiod) == false)
                return false;

            double masupportperiod;
            if (double.TryParse(setting_ui.txtMASupportPeriod.Text, out masupportperiod) == false)
                return false;

            double mavolumeperiod;
            if (double.TryParse(setting_ui.txtMAVolumePeriod.Text, out mavolumeperiod) == false)
                return false;

            int mapricetimelimit;
            if (int.TryParse(setting_ui.txtMAPriceTimelimit.Text, out mapricetimelimit) == false)
                return false;

            int mavolumetimelimit;
            if (int.TryParse(setting_ui.txtMAVolumeTimelimit.Text, out mavolumetimelimit) == false)
                return false;

            double gainProfit;
            if (double.TryParse(setting_ui.txtGainPercent.Text, out gainProfit) == false)
                return false;

            int leverage;
            if (int.TryParse(setting_ui.txtLeverage.Text, out leverage) == false || (leverage <= 0 || leverage > 125))
                return false;
            return true;
        }

        private int GetTradeSymbolCount()
        {
            int count = 0;
            for(int i =0; i < Utils.SYMBOL_COUNT;i++)
            {
                Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(i);

                if (/*symbol_setting.chartOnly == false && */symbol_setting.product.Length > 0)
                    count++;
            }

            return count;
        }

        private bool CheckSymbolDuplication(out string duplicated)
        {
            for (int i = 0; i < Utils.SYMBOL_COUNT - 1; i++)
            {
                Utils.SymbolSetting setting_1 = Utils.GetSymbolSetting(i);
                if (setting_1.product.Length == 0)
                    continue;

                for (int j = i + 1; j < Utils.SYMBOL_COUNT; j++)
                {
                    Utils.SymbolSetting setting_2 = Utils.GetSymbolSetting(j);
                    if (setting_1.product == setting_2.product)
                    {
                        duplicated = setting_1.product;
                        return false;
                    }
                }
            }

            duplicated = "";
            return true;
        }

        private Utils.SymbolSetting GetSymbolSettingFromUI(int i)
        {
            SettingsUI setting_ui = arrSettingsUI.ElementAt(i);

            Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(i);
            symbol_setting.product = setting_ui.txtProduct.Text.Trim().ToUpper();
            symbol_setting.timeFrameIndex = setting_ui.cmbTimeFrame.SelectedIndex;
            symbol_setting.supportTimeFrameIndex = setting_ui.cmbSupportTimeFrame.SelectedIndex;
            decimal.TryParse(setting_ui.txtQuantity.Text, out symbol_setting.quantity);

            int.TryParse(setting_ui.txtMALongPeriod.Text, out symbol_setting.maLongPeriod);
            int.TryParse(setting_ui.txtMAShortPeriod.Text, out symbol_setting.maShortPeriod);
            int.TryParse(setting_ui.txtMASupportPeriod.Text, out symbol_setting.maSupportPeriod);
            int.TryParse(setting_ui.txtMAVolumePeriod.Text, out symbol_setting.maVolumePeriod);
            int.TryParse(setting_ui.txtMAPriceTimelimit.Text, out symbol_setting.maPriceTimeLimit);
            int.TryParse(setting_ui.txtMAVolumeTimelimit.Text, out symbol_setting.maVolumeTimeLimit);

            decimal.TryParse(setting_ui.txtGainPercent.Text, out symbol_setting.gainProfit);
            symbol_setting.chartOnly = setting_ui.chkChartOnly.Checked;

            int.TryParse(setting_ui.txtLeverage.Text, out symbol_setting.leverage);

            return symbol_setting;
        }

        private bool SaveSettings(bool formClosing = false)
        {
            Utils.global_settings.useTestNet = chkUseTestNet.Checked;
            Utils.global_settings.apiKey = txtApiKey.Text;
            Utils.global_settings.secretKey = txtSecretKey.Text;
            Utils.global_settings.allowTrade = chkAllowTrade.Checked;
            Utils.global_settings.autoQuantity = chkAutoQuantity.Checked;

            for (int i = 0; i < Utils.SYMBOL_COUNT; i++)
            {
                Utils.SymbolSetting symbol_setting = GetSymbolSettingFromUI(i);
                Utils.SetSymbolSetting(i, symbol_setting);
            }

            Utils.SaveSettings();

            return true;
        }
        
        private void FrmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            bClosing = true;
            Utils.bWorking = false;
            gWorkingStatus = -1;
            SaveSettings(true);
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (gWorkingStatus == -1)
            {
                if (txtApiKey.Text == "" || txtSecretKey.Text == "")
                {
                    MessageBox.Show(this, "Please enter Api and Secret Key.", "Notification");
                    return;
                }

                if (CheckAllSettingsUI() == false)
                {
                    MessageBox.Show(this, "Please check the inputs.", "Notification");
                    return;
                }

                if (SaveSettings() == false)
                    return;

                if (GetTradeSymbolCount() == 0)
                {
                    MessageBox.Show(this, "No symbols to trade.", "Notification");
                    return;
                }

                string duplicated;
                if (CheckSymbolDuplication(out duplicated) == false)
                {
                    MessageBox.Show(this, $"{duplicated} is duplicated.", "Notification");
                    return;
                }
                
                btnStart.Enabled = false;
                btnStart.Text = "Stop";

                disableProductMA();
                initChartData();
                initBufferedData();

                Utils.addLog($"Started future trading...");

                new Thread(StartWorkThread).Start();
                timerUpdate.Start();

                new Thread(UpdateBalanceThread).Start();
            }
            else if (gWorkingStatus == 1)
            {
                stopStrategy();
                Utils.addLog("Stopped...");
            }
        }
        
        private void chkAllowTrade_CheckedChanged(object sender, EventArgs e)
        {
            if (bLoadingForm)
                return;

            Utils.global_settings.allowTrade = chkAllowTrade.Checked;
            Utils.SaveSettings();
        }

        private void chkAutoQuantity_CheckedChanged(object sender, EventArgs e)
        {
            if (bLoadingForm)
                return;

            Utils.global_settings.autoQuantity = chkAutoQuantity.Checked;
            Utils.SaveSettings();
        }

        private void UpdateBalanceThread()
        {
            while(gWorkingStatus >= 0)
            {
                Thread.Sleep(100);
                if (gWorkingStatus == 1)
                {
                    CheckKeepAlive();
                }
            }
        }

        private void CheckKeepAlive()
        {
            TimeSpan diffKeepAlive = DateTime.Now - gdtKeepAliveTime;
            if (diffKeepAlive.TotalMinutes > 30 && userStreamListenKey.Length != 0)
            {
                clientFuture.FuturesUsdt.UserStream.KeepAliveUserStream(userStreamListenKey);
                
                gdtKeepAliveTime = DateTime.Now;
            }
        }

        private void StartWorkThread()
        {
            string errMsg;
            gWorkingStatus = 0;
            
            bool bRet = startStrategy(out errMsg);

            if (bRet == false)
            {
                gErrMsg = errMsg;
                gErrorOccured = true;
            }
            else
            {
                gWorkingStatus = 1;
            }
        }

        private bool startStrategy(out string errMsg)
        {
            gServerTime = DateTime.MinValue;
            gSpotUpdateTime = DateTime.MinValue;
            gLastChartTime = DateTime.MinValue;

            for (int i = 0; i < Utils.SYMBOL_COUNT; i++)
            {
                Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(i);
                string symbol = symbol_setting.product;

                gdicStatus[symbol] = symbol_setting.chartOnly ? STATUS_CHARTONLY : STATUS_WAITSIGNAL;
                gdicPositionProfit[symbol] = "----";
                gdicLastOrderTime[symbol] = DateTime.MinValue;
                gdicWaitingBalance_Margin[symbol] = false;
                gdicOrderReceived[symbol] = false;
                gdicNewCandle[symbol] = false;
            }

            bool useTestNet = Utils.global_settings.useTestNet;
            
            BinanceClient.SetDefaultOptions(new BinanceClientOptions()
            {
                ApiCredentials = new ApiCredentials(Utils.global_settings.apiKey, Utils.global_settings.secretKey),
                LogVerbosity = LogVerbosity.Info,
                LogWriters = new List<TextWriter> { Console.Out }
            });
            BinanceSocketClient.SetDefaultOptions(new BinanceSocketClientOptions()
            {
                ApiCredentials = new ApiCredentials(Utils.global_settings.apiKey, Utils.global_settings.secretKey),
                LogVerbosity = LogVerbosity.Info,
                LogWriters = new List<TextWriter> { Console.Out }
            });
            
            if (useTestNet == false)
            {
                // use same Options for spot & future
                clientFuture = new BinanceClient();
                socketClientFuture = new BinanceSocketClient();
            }
            else
            {
                clientFuture = new BinanceClient(new BinanceClientOptions("https://testnet.binance.vision/", "https://testnet.binancefuture.com/", "https://testnet.binancefuture.com/")
                {
                    ApiCredentials = new ApiCredentials(Utils.global_settings.apiKey, Utils.global_settings.secretKey),
                    LogVerbosity = LogVerbosity.Info,
                    LogWriters = new List<TextWriter> { Console.Out }
                });
                socketClientFuture = new BinanceSocketClient(new BinanceSocketClientOptions("wss://testnet.binance.vision/", "wss://stream.binancefuture.com/", "wss://dstream.binancefuture.com/")
                {
                    ApiCredentials = new ApiCredentials(Utils.global_settings.apiKey, Utils.global_settings.secretKey),
                    LogVerbosity = LogVerbosity.Info,
                    LogWriters = new List<TextWriter> { Console.Out }
                });
            }

            WebCallResult<BinanceFuturesUsdtExchangeInfo> exchangeInfoResult = clientFuture.FuturesUsdt.System.GetExchangeInfo();
            if (exchangeInfoResult.Success == false)
            {
                errMsg = exchangeInfoResult.Error.ToString();
                Utils.addLog(exchangeInfoResult.Error.ToString());
                return false;
            }
            gExchangeInfo = exchangeInfoResult.Data;

            List<string> symbols = new List<string>();

            for (int i = 0; i < Utils.SYMBOL_COUNT; i++)
            {
                Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(i);
                string symbol = symbol_setting.product;

                if (symbol.Length == 0)
                    continue;

                symbols.Add(symbol);

                var bookPrice = clientFuture.FuturesUsdt.Market.GetBookPrices(symbol);
                if (bookPrice.Success)
                {
                    BidAskPrice price;
                    price.Ask = bookPrice.Data.ElementAt(0).BestAskPrice;
                    price.Bid = bookPrice.Data.ElementAt(0).BestBidPrice;
                    gdicPrices[symbol] = price;
                }

                KlineInterval timeInterval = Utils.GetKlineInterval(symbol_setting.timeFrameIndex);
                KlineInterval supportTimeInterval = Utils.GetKlineInterval(symbol_setting.supportTimeFrameIndex);

                // Klines
                WebCallResult<IEnumerable<IBinanceKline>> klines = clientFuture.FuturesUsdt.Market.GetKlines(symbol, timeInterval);
                if (klines.Success == false)
                {
                    errMsg = $"{symbol}, {klines.Error}";
                    Utils.addLog(errMsg);
                    return false;
                }
                // Klines Support
                WebCallResult<IEnumerable<IBinanceKline>> klinesSupport = clientFuture.FuturesUsdt.Market.GetKlines(symbol, supportTimeInterval);
                if (klines.Success == false)
                {
                    errMsg = $"{symbol}, {klines.Error}";
                    Utils.addLog(errMsg);
                    return false;
                }

                gdicKlines[symbol] = new List<IBinanceKline>(klines.Data);
                gdicSupportKlines[symbol] = new List<IBinanceKline>(klinesSupport.Data);
                gdicMASignalCheck[symbol] = new List<bool>();
                gdicMASupportSignalCheck[symbol] = new List<bool>();
                gdicVolumeSignalCheck[symbol] = new List<bool>();
                gdicMALonglines[symbol] = new List<decimal>();
                gdicMAShortlines[symbol] = new List<decimal>();
                gdicMASupportlines[symbol] = new List<decimal>();
                gdicMAVolumelines[symbol] = new List<decimal>();
                for (int k = 0;k < klines.Data.Count(); k ++)
                {
                    gdicMASignalCheck[symbol].Add(false);
                    gdicMASupportSignalCheck[symbol].Add(false);
                    gdicVolumeSignalCheck[symbol].Add(false);
                }
                
                calculateMAlines(symbol);
                updateSupportValue(symbol, false);
                for (int index = 1; index < gdicMASignalCheck[symbol].Count; index++)
                {
                    updatePriceMASignalCheckedArray(symbol, index);
                    updateVolumeMASignalCheckedArray(symbol, index);
                }

                gdicKlineUpdated[symbol] = true;

                // subscribe Klines
                socketClientFuture.FuturesUsdt.SubscribeToKlineUpdates(symbol, timeInterval, data => KlineReceived(data));

                socketClientFuture.FuturesUsdt.SubscribeToKlineUpdates(symbol, supportTimeInterval, data => KlineSupportReceived(data));
            }

            // OrdersList
            WebCallResult<IEnumerable<BinanceFuturesOrder>> ordersList = clientFuture.FuturesUsdt.Order.GetOpenOrders();
            if (ordersList.Success == false)
            {
                errMsg = $"OpenOrders {ordersList.Error}";
                Utils.addLog(errMsg);
                return false;
            }
            AllOrdersReceived(ordersList.Data);

            // subscribe Ticker
            socketClientFuture.FuturesUsdt.SubscribeToBookTickerUpdates(symbols.ToArray(), data => TickerReceived(data));

            // Balance
            WebCallResult<BinanceFuturesAccountInfo> accountInfo = clientFuture.FuturesUsdt.Account.GetAccountInfo();
            if (accountInfo.Success == false)
            {
                errMsg = accountInfo.Error.ToString();
                Utils.addLog(accountInfo.Error.ToString());
                return false;
            }
            AllBalancesReceived_Spot(accountInfo.Data.Assets);

            glstPositions.AddRange(accountInfo.Data.Positions);
            for (int i = 0; i < Utils.SYMBOL_COUNT; i++)
            {
                Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(i);
                string symbol = symbol_setting.product;

                if (symbol.Length == 0)
                    continue;

                int leverage = getSymbolLeverage(symbol);
                if (leverage == 0)
                {
                    Utils.addLog($"{symbol}, Failed to get leverage");
                    continue;
                }

                if (leverage != symbol_setting.leverage)
                {
                    WebCallResult<BinanceFuturesInitialLeverageChangeResult> leverageResult = clientFuture.FuturesUsdt.ChangeInitialLeverage(symbol, symbol_setting.leverage);
                    if (leverageResult.Success == false)
                    {
                        Utils.addLog($"{symbol}, Failed to change leverage {leverageResult.Error}");
                    }
                    else
                        setSymbolLeverage(symbol, symbol_setting.leverage);
                }

                if (getSymbolIsIsolated(symbol) == false)
                {
                    WebCallResult<BinanceFuturesChangeMarginTypeResult> result = clientFuture.FuturesUsdt.ChangeMarginType(symbol, FuturesMarginType.Isolated);
                    if (result.Success == false)
                    {
                        Utils.addLog($"{symbol}, Failed to change isolated margin type {result.Error}");
                    }
                    else
                        setSymbolIsIsolated(symbol);
                }
            }
            gPositionUpdated = true;

            var startSpotResult = clientFuture.FuturesUsdt.UserStream.StartUserStream();
            if (!startSpotResult.Success)
            {
                userStreamListenKey = "";
                errMsg = startSpotResult.Error.ToString();
                Utils.addLog(startSpotResult.Error.ToString());
                return false;
            }
            userStreamListenKey = startSpotResult.Data;

            // Subscribe userdata
            CallResult<UpdateSubscription> subscriptionResult = socketClientFuture.FuturesUsdt.SubscribeToUserDataUpdates(userStreamListenKey,
                    marginUpdate =>
                    {
                        Console.WriteLine($"Future marginUpdate {marginUpdate}");
                    },
                    accountUpdate =>
                    { // Handle account info update 
                        Console.WriteLine($"Future accountUpdate");

                        BalanceUpdate_Received(accountUpdate.UpdateData.Balances);
                        Positions_Received(accountUpdate.UpdateData.Positions);
                    },
                    orderUpdate =>
                    { // Handle order update
                        OrderUpdate_Received(orderUpdate.UpdateData);
                    },
                    listenkeyExpired =>
                    {
                        Utils.addLog($"Future listenkeyExpired");
                    }
                    );
          
            errMsg = "";
            gdtKeepAliveTime = DateTime.Now;
            return true;
        }

        private void setSymbolLeverage(string symbol, int leverage)
        {
            for (int i = 0; i < glstPositions.Count; i++)
            {
                if (glstPositions[i].Symbol == symbol)
                {
                    glstPositions[i].Leverage = leverage;
                }
            }
        }

        private int getSymbolLeverage(string symbol)
        {
            for(int i = 0; i < glstPositions.Count; i++)
            {
                if (glstPositions[i].Symbol == symbol)
                    return glstPositions[i].Leverage;
            }

            return 0;
        }

        private void setSymbolIsIsolated(string symbol)
        {
            for (int i = 0; i < glstPositions.Count; i++)
            {
                if (glstPositions[i].Symbol == symbol)
                {
                    glstPositions[i].Isolated = true;
                }
            }
        }
        private bool getSymbolIsIsolated(string symbol)
        {
            for (int i = 0; i < glstPositions.Count; i++)
            {
                if (glstPositions[i].Symbol == symbol)
                    return glstPositions[i].Isolated;
            }

            return true;
        }

        private void disableProductMA()
        {
            cmbTimeFrame1.Enabled = false;
            for(int i = 0; i < Utils.SYMBOL_COUNT; i++)
            {
                SettingsUI setting_ui = arrSettingsUI.ElementAt(i);
                setting_ui.txtProduct.Enabled = false;
                setting_ui.cmbTimeFrame.Enabled = false;
                setting_ui.txtMALongPeriod.Enabled = false;
                setting_ui.txtMAShortPeriod.Enabled = false;
                setting_ui.txtMASupportPeriod.Enabled = false;
                setting_ui.txtMAVolumePeriod.Enabled = false;
                setting_ui.btnUpdate.Visible = false;
                setting_ui.cmbSupportTimeFrame.Enabled = false;
            }
        }

        private void enableProductMA()
        {
            cmbTimeFrame1.Enabled = true;
            for (int i = 0; i < Utils.SYMBOL_COUNT; i++)
            {
                SettingsUI setting_ui = arrSettingsUI.ElementAt(i);
                setting_ui.txtProduct.Enabled = true;
                setting_ui.cmbTimeFrame.Enabled = true;
                setting_ui.txtMALongPeriod.Enabled = true;
                setting_ui.txtMAShortPeriod.Enabled = true;
                setting_ui.txtMASupportPeriod.Enabled = true;
                setting_ui.txtMAVolumePeriod.Enabled = true;
                setting_ui.cmbSupportTimeFrame.Enabled = true;
            }
        }

        private void stopStrategy()
        {
            gWorkingStatus = -1;
            if (socketClientFuture != null)
                socketClientFuture.UnsubscribeAll();
            clientFuture = null;
            socketClientFuture = null;

            btnStart.Enabled = true;
            timerUpdate.Stop();
            btnStart.Text = "Start";
            enableProductMA();
        }        

        private void AllBalancesReceived_Spot(IEnumerable<BinanceFuturesAccountAsset> arrBalances)
        {
            for(int i = 0; i < arrBalances.Count(); i++)
            {
                BinanceFuturesAccountAsset binance_balance = arrBalances.ElementAt(i);
                //Console.WriteLine($"Balance {balance.Asset}, Free {balance.Free}, Locked {balance.Locked}");

                for (int j = 0; j < Utils.SYMBOL_COUNT; j++)
                {
                    Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(j);
                    if (symbol_setting.product.StartsWith(binance_balance.Asset) || symbol_setting.product.EndsWith(binance_balance.Asset))
                    {
                        BalanceItem balance = new BalanceItem();
                        balance.Asset = binance_balance.Asset;
                        balance.Balance = binance_balance.MarginBalance;

                        bool exist = false;
                        for (int k = 0; k < glstBalances.Count; k++)
                        {
                            if (balance.Asset == glstBalances[k].Asset)
                            {
                                exist = true;
                                if (Math.Round(glstBalances[k].Balance, 5) != Math.Round(balance.Balance, 5))
                                {
                                    Utils.addLog($"Balance updated {balance.Asset}, Balance {glstBalances[k].Balance}=>{balance.Balance}");
                                }

                                glstBalances[k] = balance;
                                break;
                            }
                        }
                        if (exist == false)
                            glstBalances.Add(balance);
                        
                        break;
                    }
                }
            }
            
            gBalanceUpdated = true;
        }
        
        private void Positions_Received(IEnumerable<BinanceFuturesStreamPosition> arrStreamPositions)
        {
            Console.WriteLine("PositionUpdated");

            for (int i = 0; i < arrStreamPositions.Count(); i++)
            {
                BinanceFuturesStreamPosition streamPosition = arrStreamPositions.ElementAt(i);
                for (int j = 0; j < glstPositions.Count; j++)
                {
                    if (streamPosition.Symbol == glstPositions[j].Symbol)
                    {
                        BinanceFuturesAccountPosition position = glstPositions[j];

                        position.PositionSide = streamPosition.PositionSide;
                        position.PositionAmount = streamPosition.PositionAmount;
                        position.EntryPrice = streamPosition.EntryPrice;
                        position.UnrealizedProfit = streamPosition.UnrealizedPnl;
                        position.Isolated = streamPosition.MarginType == FuturesMarginType.Isolated;

                        glstPositions[j] = position;
                    }
                }
            }

            gPositionUpdated = true;
        }

        private void BalanceUpdate_Received(IEnumerable<BinanceFuturesStreamBalance> arrStreamBalances)
        {
            Console.WriteLine("BalanceUpdated");
            for (int i = 0; i < arrStreamBalances.Count(); i++)
            {
                BinanceFuturesStreamBalance streambalance = arrStreamBalances.ElementAt(i);
                for(int j = 0; j < glstBalances.Count; j++)
                {
                    if (streambalance.Asset == glstBalances[j].Asset)
                    {
                        BalanceItem balance = glstBalances[j];

                        balance.Asset = streambalance.Asset;
                        balance.Balance = streambalance.WalletBalance;

                        glstBalances[j] = balance;
                        
                        // todo comment
                        Utils.addLog($"Balance updated {balance.Asset}, Balance {balance.Balance}(Wallet), {streambalance.CrossBalance}(Cross) ");
                    }
                }
            }
            
            gBalanceUpdated = true;
        }

        private void AllOrdersReceived(IEnumerable<BinanceFuturesOrder> arrOrders)
        {
            Utils.addLog($"Open Orders {arrOrders.Count()}");
            lock (glstOpenOrders)
            {
                // remove deleted
                for (int j = glstOpenOrders.Count - 1; j >= 0; j--)
                {
                    bool found = false;

                    for (int i = 0; i < arrOrders.Count(); i++)
                    {
                        BinanceFuturesOrder binanceOrder = arrOrders.ElementAt(i);

                        if (glstOpenOrders[j].OrderId == binanceOrder.OrderId)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (found == false)
                        glstOpenOrders.RemoveAt(j);
                }

                // add or edit exist
                for (int i = 0; i < arrOrders.Count(); i++)
                {
                    BinanceFuturesOrder binanceOrder = arrOrders.ElementAt(i);

                    bool found = false;
                    for(int j = 0; j < glstOpenOrders.Count; j++)
                    {
                        if (glstOpenOrders[j].OrderId == binanceOrder.OrderId)
                        {
                            glstOpenOrders[j] = binanceOrder;
                            found = true;
                            break;
                        }
                    }
                    if (found == false)
                        glstOpenOrders.Add(binanceOrder);
                }
            }

            gOrdersUpdated = true;
        }
        
        private void OrderUpdate_Received(BinanceFuturesStreamOrderUpdateData orderUpdate)
        {
            // Handle order update
            Utils.addLog($"OrderUpdate {orderUpdate.Symbol}, {orderUpdate.Side}, {orderUpdate.Status}, Price {orderUpdate.Price}, Quantity {orderUpdate.Quantity}, FilledPrice {orderUpdate.PriceLastFilledTrade}");

            if (Utils.GetSymbolIndex(orderUpdate.Symbol) < 0)
            {
                Utils.addLog("Can't find symbol index.");
                return;
            }

            lock (glstOpenOrders)
            {
                int existIndex = -1;
                for (int i = 0; i < glstOpenOrders.Count; i++)
                {
                    if (glstOpenOrders[i].OrderId == orderUpdate.OrderId)
                    {
                        existIndex = i;
                        break;
                    }
                }

                OrderStatus status = orderUpdate.Status;

                BinanceFuturesOrder order = new BinanceFuturesOrder();
                order.ClientOrderId = orderUpdate.ClientOrderId;
                order.CreatedTime = orderUpdate.CreateTime;
                order.Price = orderUpdate.Price;
                order.StopPrice = orderUpdate.StopPrice;
                order.OrderId = orderUpdate.OrderId;
                order.OriginalQuantity = orderUpdate.Quantity;
                order.ExecutedQuantity = orderUpdate.QuantityOfLastFilledTrade;
                order.Side = orderUpdate.Side;
                order.Status = orderUpdate.Status;
                order.StopPrice = orderUpdate.StopPrice;
                order.Symbol = orderUpdate.Symbol;
                order.TimeInForce = orderUpdate.TimeInForce;
                order.Type = orderUpdate.Type;
                order.UpdateTime = DateTime.Now;

                if (status == OrderStatus.New || status == OrderStatus.PartiallyFilled)
                {
                    if (existIndex >= 0)
                    {
                        order.ExecutedQuantity += glstOpenOrders[existIndex].ExecutedQuantity;
                        glstOpenOrders[existIndex] = order;
                    }
                    else
                        glstOpenOrders.Add(order);
                }
                else
                {
                    if (existIndex >= 0)
                        glstOpenOrders.RemoveAt(existIndex);
                }
            }

            if (orderUpdate.Status == OrderStatus.Filled || orderUpdate.Status == OrderStatus.PartiallyFilled)
            {
                Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(orderUpdate.Symbol);

                if (orderUpdate.Side == OrderSide.Buy)
                {
                    decimal openFilled = orderUpdate.PriceLastFilledTrade;
                    symbol_setting.openPrice = openFilled;
                    Utils.addLog($"{symbol_setting.product} openPrice {symbol_setting.openPrice}");
                    Utils.SetSymbolSetting(orderUpdate.Symbol, symbol_setting);
                    Utils.SaveSettings();
                }
                else
                {
                    decimal closeFilled = orderUpdate.PriceLastFilledTrade;

                    if (symbol_setting.openPrice > 0 && orderUpdate.Status == OrderStatus.Filled)
                    {
                        decimal openPrice = symbol_setting.openPrice;
                        decimal profitRate = closeFilled / openPrice;

                        symbol_setting.openPrice = 0;
                        Utils.addLog($"{symbol_setting.product} openPrice reset");
                        if (Utils.global_settings.autoQuantity)
                        {
                            decimal old_quantity = symbol_setting.quantity;
                            decimal new_quantity;

                            // check lot min tick
                            CheckOrderQuantityFilter(orderUpdate.Symbol, old_quantity * profitRate, OrderType.Limit, out new_quantity);
                            Utils.addLog($"Update quantity {orderUpdate.Symbol}, OpenPrice {openPrice}, ClosePrice {closeFilled}, ProfitRate {(profitRate - 1) * 100:0.000}%, Quantity {old_quantity}=>{new_quantity}");

                            symbol_setting.quantity = new_quantity;

                            Utils.SetSymbolSetting(orderUpdate.Symbol, symbol_setting);
                            Utils.SaveSettings();

                            UpdateQuantityText(orderUpdate.Symbol, new_quantity);
                        }
                        else
                        {
                            Utils.SetSymbolSetting(orderUpdate.Symbol, symbol_setting);
                            Utils.SaveSettings();

                            Utils.addLog($"Update quantity {orderUpdate.Symbol}, OpenPrice {openPrice}, ClosePrice {closeFilled}, ProfitRate {(profitRate - 1) * 100:0.000}%");
                        }
                    }
                }
            }

            gdicOrderReceived[orderUpdate.Symbol] = true;

            gOrdersUpdated = true;
        }

        private delegate void deleageUpdateQuantity(string symbol, decimal quantity);
        private void UpdateQuantityText(string symbol, decimal quantity)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new deleageUpdateQuantity(UpdateQuantityText), new object[] { symbol, quantity });
                return;
            }

            int index = Utils.GetSymbolIndex(symbol);
            arrSettingsUI[index].txtQuantity.Text = Utils.GetDecimalString(quantity);
        }

        private void TickerReceived(BinanceFuturesStreamBookPrice data)
        {
            // Handle data
            BidAskPrice price;
            price.Ask = data.BestAskPrice;
            price.Bid = data.BestBidPrice;
            gdicPrices[data.Symbol] = price;
            gSpotUpdateTime = DateTime.Now;
        }

        private void KlineReceived(IBinanceStreamKlineData data)
        {
            string symbol = data.Symbol;
            
            if (gdicKlines.ContainsKey(symbol) == false ||
                gdicKlineUpdated.ContainsKey(symbol) == false ||
                gdicMALonglines.ContainsKey(symbol) == false ||
                gdicMAShortlines.ContainsKey(symbol) == false ||
                gdicMASupportlines.ContainsKey(symbol) == false ||
                gdicMAVolumelines.ContainsKey(symbol) == false)
            {
                return;
            }

            DateTime dtLastKlineTime = gdicKlines[symbol][gdicKlines[symbol].Count - 1].CloseTime;

            if (data.Data.CloseTime > dtLastKlineTime) // new bar
            {
                // Check New and cancel all pending orders.
                gdicKlines[symbol].Add(data.Data);

                gdicNewCandle[symbol] = true;

                gdicMASignalCheck[symbol].Add(false);
                gdicVolumeSignalCheck[symbol].Add(false);
                gdicMASupportSignalCheck[symbol].Add(false);
            }
            else // update bar
            {
                // Place Close stoplossLimit
                gdicKlines[symbol][gdicKlines[symbol].Count - 1] = data.Data;
            }
            calculateMAlines(symbol);
            updateSupportValue(symbol);
            updatePriceMASignalCheckedArray(symbol, gdicMASignalCheck[symbol].Count - 1);
            updateVolumeMASignalCheckedArray(symbol, gdicMASignalCheck[symbol].Count - 1);

            checkOpenBuySignal_Future(symbol);

            checkCloseSellSignal_Future(symbol);

            gdicKlineUpdated[symbol] = true;

            //Console.WriteLine($"kline {data.Symbol}, {data.Data.OpenTime}, {data.Data.CloseTime}, {data.Data.Open}, {data.Data.High}, {data.Data.Low}, {data.Data.Close}");
        }

        private void KlineSupportReceived(IBinanceStreamKlineData data)
        {
            string symbol = data.Symbol;
            if (!gdicSupportKlines.ContainsKey(symbol))
                return;

            DateTime dtLastKlineTime = gdicSupportKlines[symbol][gdicSupportKlines[symbol].Count - 1].CloseTime;
            if (data.Data.CloseTime > dtLastKlineTime)
                gdicSupportKlines[symbol].Add(data.Data);
            else
                gdicSupportKlines[symbol][gdicSupportKlines[symbol].Count - 1] = data.Data;

        }
        private bool checkExistPosition_Future(string symbol, out decimal positionAmount)
        {
            positionAmount = 0;
            BinanceFuturesUsdtSymbol symbolInfo = GetSymbolInfo(symbol);
            if (symbolInfo == null)
            {
                Console.WriteLine($"Cannot find symbolInfo {symbol}");
                return false;
            }

            for (int i = 0; i < glstPositions.Count; i++)
            {
                if (glstPositions[i].Symbol == symbol)
                {
                    positionAmount = glstPositions[i].PositionAmount;
                    return true;
                }
            }

            return false;
        }
        
        
        private bool checkExistOrders(string symbol)
        {
            for(int i = 0; i< glstOpenOrders.Count;i++)
            {
                if (glstOpenOrders[i].Symbol == symbol)
                    return true;
            }

            return false;
        }

        private string GetFormatedDecimal(string symbol, decimal value)
        {
            decimal tick = GetTickSize(symbol);
            decimal format = ((int)(value / tick)) * tick;
            return Utils.GetDecimalString(format);
        }

        // call when new bar appeared.
        private void checkOpenBuySignal_Future(string symbol)
        {
            Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(symbol);
            if (symbol_setting.chartOnly)
                return;

            if (Utils.global_settings.allowTrade == false)
                return;

            if (gOrdering_Spot)
                return;

            if (checkExistOrders(symbol))
            {
                // cancel open orders
                CancelAllOpenOrders_Spot(symbol);
            }

            decimal positionAmount;
            if (checkExistPosition_Future(symbol, out positionAmount) == false)
                return;

            if (CheckLastOrderTime(symbol) == false)
            {
                Console.WriteLine($"{symbol} Skip marketOrder (too often)");
                return;
            }

            if (gdicMASignalCheck[symbol].Last() && 
                gdicVolumeSignalCheck[symbol].Last() && 
                gdicMASupportSignalCheck[symbol].Last() && 
                positionAmount < symbol_setting.quantity)
            {
                decimal quantity = (decimal)symbol_setting.quantity;
                placeMarketOrder_Future(symbol, quantity, OrderSide.Buy);
            }
        }
        
        // for 
        private void checkCloseSellSignal_Future(string symbol)
        {
            if (gOrdering_Spot)
                return;

            Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(symbol);
            if (symbol_setting.chartOnly)
                return;

            List<IBinanceKline> lstKlines = gdicKlines[symbol];
            if (lstKlines.Count < 2)
                return;

            IBinanceKline previousBar = lstKlines[lstKlines.Count - 2];
            IBinanceKline kline = gdicKlines[symbol].Last();

            decimal positionAmount;

            if (checkExistPosition_Future(symbol, out positionAmount) == false)
                return;
            
            if (positionAmount <= 0)
            {
                // must check open
                return;
            }

            decimal openPrice = symbol_setting.openPrice;
            if (openPrice == 0)
            {
                openPrice = kline.Close;
                symbol_setting.openPrice = openPrice;
                Utils.addLog($"{symbol_setting.product} openPrice {symbol_setting.openPrice}");
                Utils.SetSymbolSetting(symbol, symbol_setting);
                Utils.SaveSettings();
            }

            decimal ma_value = gdicMALonglines[symbol].Last();

            decimal stopPrice = previousBar.Low - GetTickSize(symbol) * symbol_setting.sellDownTicks;

            decimal filterdQuantity;
            CheckOrderQuantityFilter(symbol, positionAmount, OrderType.Stop, out filterdQuantity);

            decimal positionProfit = (kline.Close / openPrice) * 100 - 100;
            string profitString = positionProfit.ToString("0.###");
            gdicPositionProfit[symbol] = profitString + "%";

            if (Utils.global_settings.allowTrade == false)
                return;

            if (positionProfit >= symbol_setting.gainProfit && symbol_setting.gainProfit > 0)
            {
                Utils.addLog($"Market close with profit Open {openPrice}, Close {kline.Close}, Profit {profitString}");

                if (checkExistOrders(symbol))
                    CancelAllOpenOrders_Spot(symbol);

                if (positionAmount != filterdQuantity)
                    Utils.addLog($"Filtered lot quantity {positionAmount}=>{filterdQuantity}");

                placeMarketOrder_Future(symbol, filterdQuantity, OrderSide.Sell);
                gdicStatus[symbol] = "Market close";
            }
            else if (gdicMAShortlines[symbol].ElementAt(gdicMAShortlines[symbol].Count - 2) > gdicMALonglines[symbol].ElementAt(gdicMALonglines[symbol].Count - 2) && 
                gdicMAShortlines[symbol].Last() < gdicMALonglines[symbol].Last())
            {

                Utils.addLog($"Place sell  {symbol}, Short MA:{GetFormatedDecimal(symbol, gdicMAShortlines[symbol].Last())} less than Long MA: {GetFormatedDecimal(symbol, gdicMALonglines[symbol].Last())}");

                if (checkExistOrders(symbol))
                    return;

                if (positionAmount != filterdQuantity)
                    Utils.addLog($"Filtered lot quantity {positionAmount}=>{filterdQuantity}");

                placeMarketOrder_Future(symbol, filterdQuantity, OrderSide.Sell);
            }
        }
        
        private void updatePriceLabels()
        {
            for(int i = 0; i< arrPriceLabels.Count(); i++)
            {
                Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(i);
                string symbol = symbol_setting.product;

                if (symbol.Length == 0)
                    continue;

                if (gdicPrices.ContainsKey(symbol) &&
                    gdicStatus.ContainsKey(symbol))
                {
                    string status = gdicStatus[symbol];
                    BidAskPrice price = gdicPrices[symbol];
                    arrPriceLabels[i].Text = $"{status}, {GetFormatedDecimal(symbol, price.Bid)}, {GetFormatedDecimal(symbol, price.Ask)}";
                }

                if (gdicPositionProfit.ContainsKey(symbol))
                    arrPositionProfitLabels[i].Text = gdicPositionProfit[symbol];

            }
        }

        private void updateVolumeLabels()
        {

            for (int i = 0; i < arrVolumeLabels.Count(); i ++)
            {
                Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(i);
                string symbol = symbol_setting.product;

                if (symbol.Length == 0)
                    continue;

                if (gdicKlines.ContainsKey(symbol))
                {
                    arrVolumeLabels[i].Text = $"Volume {Utils.ToKMB((decimal)(gdicKlines[symbol].Last().BaseVolume))}";
                }
            }
        }

        private void updateSupportLabels()
        {
            for (int i = 0; i < arrSupportValueLabels.Count(); i++)
            {
                Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(i);
                string symbol = symbol_setting.product;

                if (symbol.Length == 0)
                    continue;

                if (gdicSupportValues.ContainsKey(symbol))
                {
                    arrSupportValueLabels[i].Text = $"Support {GetFormatedDecimal(symbol, gdicSupportValues[symbol])}";
                }
            }
        }
        private void updateBalanceListView()
        {
            if (gBalanceUpdated == false)
                return;
            gBalanceUpdated = false;

            BalanceItem[] balances = glstBalances.ToArray();

            if (listView_Balances.Items.Count == 0)
            {
                for(int i = 0; i < balances.Count(); i++)
                {
                    BalanceItem balanceItem = balances[i];
                    ListViewItem lvItem = new ListViewItem(balanceItem.Asset);
                    lvItem.SubItems.Add(Utils.GetDecimalString(balanceItem.Balance));
                    listView_Balances.Items.Add(lvItem);
                }
            }
            else
            {
                for(int i = 0; i < balances.Count(); i++)
                {
                    BalanceItem balanceItem = balances[i];
                    listView_Balances.Items[i].SubItems[1].Text = Utils.GetDecimalString(balanceItem.Balance);
                }
            }
        }

        private void updatePositionsListView()
        {
            if (gPositionUpdated == false)
                return;

            gPositionUpdated = false;
            
            listView_Positions.Items.Clear();
            for (int i = 0; i < glstPositions.Count; i++)
            {
                if (glstPositions[i].PositionAmount == 0)
                    continue;

                ListViewItem lvItem = new ListViewItem();
                for (int j = 0; j < listView_Positions.Columns.Count - 1; j++)
                    lvItem.SubItems.Add("");
                listView_Positions.Items.Add(lvItem);

                UpdatePositionListItem(listView_Positions.Items.Count - 1, glstPositions[i]);
            }
        }
        private void UpdatePositionListItem(int listIndex, BinanceFuturesAccountPosition position)
        {
            ListViewItem lvItem = listView_Positions.Items[listIndex];
            lvItem.SubItems[colPositionSymbol.Index].Text = position.Symbol.ToString();
            lvItem.SubItems[colPositionSize.Index].Text = Utils.GetDecimalString(position.PositionAmount);
            lvItem.SubItems[colPositionEntryPrice.Index].Text = Utils.GetDecimalString(position.EntryPrice);
            lvItem.SubItems[colLeverage.Index].Text = position.Leverage.ToString();
            lvItem.SubItems[colPositionMarginType.Index].Text = position.Isolated ? "Isolated":"Cross"; 
        }
        private void updateOrdersListView()
        {
            if (gOrdersUpdated == false)
                return;
            gOrdersUpdated = false;

            while(listView_OpenOrders.Items.Count > glstOpenOrders.Count)
            {
                listView_OpenOrders.Items.RemoveAt(glstOpenOrders.Count);
            }

            while(listView_OpenOrders.Items.Count < glstOpenOrders.Count)
            {
                ListViewItem lvItem = new ListViewItem();
                for(int i =0; i< listView_OpenOrders.Columns.Count - 1;i++)
                    lvItem.SubItems.Add("");
                listView_OpenOrders.Items.Add(lvItem);
            }

            for(int i = 0; i < glstOpenOrders.Count; i++)
            {
                UpdateOrderListItem(i, glstOpenOrders[i]);
            }
        }
        private void UpdateOrderListItem(int listIndex, BinanceFuturesOrder order)
        {
            ListViewItem lvItem = listView_OpenOrders.Items[listIndex];
            lvItem.SubItems[colOrderId.Index].Text = order.OrderId.ToString();
            lvItem.SubItems[colOrderSymbol.Index].Text = order.Symbol;
            lvItem.SubItems[colOrderPrice.Index].Text = Utils.GetDecimalString(order.Price);
            lvItem.SubItems[colOrderStopPrice.Index].Text = Utils.GetDecimalString(order.StopPrice);
            lvItem.SubItems[colOrderQuantity.Index].Text = Utils.GetDecimalString(order.OriginalQuantity);
            lvItem.SubItems[colOrderQuantityFilled.Index].Text = Utils.GetDecimalString(order.ExecutedQuantity);
            lvItem.SubItems[colOrderSide.Index].Text = order.Side.ToString();
            lvItem.SubItems[colOrderStatus.Index].Text = order.Status.ToString();
            lvItem.SubItems[colOrderType.Index].Text = order.Type.ToString();
            lvItem.SubItems[colOrderCreateTime.Index].Text = order.CreatedTime.ToString();
            lvItem.SubItems[colOrderUpdated.Index].Text = order.UpdateTime.ToString();
        }

        private void calculateEMAvalue(List<decimal> MALines, int ma_period, IBinanceKline[] klines)
        {
            for (int i = Math.Max(0, MALines.Count - 1); i < klines.Count(); i++)
            {
                double Exp_Percent = (double)2 / (ma_period + 1);
                if (i == 0)
                {
                    MALines.Add(klines[i].Close);
                }
                else if (i < MALines.Count)
                {
                    MALines[i] = klines[i].Close * (decimal)Exp_Percent + MALines[i - 1] * (decimal)(1 - Exp_Percent);
                }
                else
                {
                    MALines.Add(klines[i].Close * (decimal)Exp_Percent + MALines[i - 1] * (decimal)(1 - Exp_Percent));
                }
            }
        }

        private void calculateMAvalue(List<decimal> MALines, int ma_period, IBinanceKline[] klines, bool isUseVolume = false)
        {
            int i, j;
            for (i = Math.Max(0, MALines.Count - 1); i < klines.Count(); i++)
            {
                decimal sum = 0;
                decimal average = 0;
                for (j = Math.Max(0, i - ma_period + 1); j <= i; j++)
                {
                    if (!isUseVolume)
                        sum += klines[j].Close;
                    else
                        sum += klines[j].BaseVolume;
                }
                average = sum / (decimal)Math.Min(i + 1, ma_period);

                if (i < MALines.Count)
                    MALines[i] = average;
                else
                    MALines.Add(average);
            }
        }

        private void calculateMAlines(string symbol)
        {
            Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(symbol);

            IBinanceKline[] klines = gdicKlines[symbol].ToArray();

            List<decimal> lstMALonglines = gdicMALonglines[symbol];
            List<decimal> lstMAShortlines = gdicMAShortlines[symbol];
            List<decimal> lstMAVolumelines = gdicMAVolumelines[symbol];
            List<decimal> lstMASupportlines = gdicMASupportlines[symbol];

            calculateEMAvalue(lstMALonglines, symbol_setting.maLongPeriod, klines);
            calculateEMAvalue(lstMAShortlines, symbol_setting.maShortPeriod, klines);
            calculateMAvalue(lstMAVolumelines, symbol_setting.maVolumePeriod, klines, true);
        }

        private void updateSupportValue(string symbol, bool isUpdate = true)
        {
            Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(symbol);
            IBinanceKline[] ksupportlines = gdicSupportKlines[symbol].ToArray();
            int i;
            double Exp_Percent = (double)2 / (symbol_setting.maSupportPeriod + 1);

            if (!isUpdate) // Init support value
            {
                for (i = 0; i < ksupportlines.Length; i++)
                {
                    if (i == 0)
                        gdicSupportValues[symbol] = ksupportlines[i].Close;
                    else
                        gdicSupportValues[symbol] = ksupportlines[i].Close * (decimal)Exp_Percent + gdicSupportValues[symbol] * (decimal)(1 - Exp_Percent);
                }
            }
            else
            {
                if (!gdicSupportValues.ContainsKey(symbol))
                    return;
                gdicSupportValues[symbol] = ksupportlines.Last().Close * (decimal)Exp_Percent + gdicSupportValues[symbol] * (decimal)(1 - Exp_Percent);
            }
        }
        private bool IsCorssedMA(int frontIndex, int backIndex, string symbol)
        {
            if (gdicMALonglines[symbol].ElementAt(frontIndex) >= gdicMAShortlines[symbol].ElementAt(frontIndex) &&
                gdicMALonglines[symbol].ElementAt(backIndex) < gdicMAShortlines[symbol].ElementAt(backIndex))
                return true;

            return false;
        }
        private void updatePriceMASignalCheckedArray(string symbol, int index)
        {
            Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(symbol);
            IBinanceKline[] klines = gdicKlines[symbol].ToArray();
            int i;
            if (index < symbol_setting.maPriceTimeLimit)
                return;

            if (gdicKlines[symbol].ElementAt(index).Close >= gdicSupportValues[symbol])
                gdicMASupportSignalCheck[symbol][index] = true;

            for (i = 0; i < symbol_setting.maPriceTimeLimit; i++)
            {
                if (IsCorssedMA(index - i - 1, index - i, symbol))
                {
                    gdicMASignalCheck[symbol][index] = true;
                    return;
                }
            }
        }

        private void updateVolumeMASignalCheckedArray(string symbol, int index)
        {
            Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(symbol);

            IBinanceKline[] klines = gdicKlines[symbol].ToArray();
            int i;
            if (index < symbol_setting.maVolumeTimeLimit)
                return;

            for (i = 1; i <= symbol_setting.maVolumeTimeLimit;i ++)
            {
                if (klines.ElementAt(index - i + 1).BaseVolume > gdicMAVolumelines[symbol].ElementAt(index - i + 1))
                { 
                    gdicVolumeSignalCheck[symbol][index] = true;
                    return;
                }
            }

        }

        private void updateAllChart()
        {
            for (int i = 0; i < Utils.SYMBOL_COUNT; i++)
            {
                Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(i);
                updateChart(symbol_setting.product);
            }
        }

        private void updateChart(string symbol)
        {
            if (gdicKlines.ContainsKey(symbol) == false ||
                gdicKlineUpdated.ContainsKey(symbol) == false ||
                gdicMALonglines.ContainsKey(symbol) == false ||
                gdicMAShortlines.ContainsKey(symbol) == false ||
                gdicMASupportlines.ContainsKey(symbol) == false ||
                gdicMAVolumelines.ContainsKey(symbol) == false)
            {
                return;
            }

            if (gdicKlineUpdated[symbol] == false)
                return;
            gdicKlineUpdated[symbol] = false;

            int symbol_index = Utils.GetSymbolIndex(symbol);
            if (symbol_index < 0)
                return;

            Chart chart = arrCharts[symbol_index];

            DateTime dtChartLastTime = DateTime.MinValue;

            if (chart.Series[0].Points.Count > 0)
                dtChartLastTime = DateTime.FromOADate(chart.Series[0].Points.Last().XValue);

            List<IBinanceKline> klines = gdicKlines[symbol];
            List<decimal> malongvalues = gdicMALonglines[symbol];
            List<decimal> mashortvalues = gdicMAShortlines[symbol];
            List<decimal> masupportvalues = gdicMASupportlines[symbol];
            List<decimal> mavolumevalues = gdicMAVolumelines[symbol];

            for(int i = 0; i < klines.Count; i++)
            {
                if (klines[i].OpenTime < dtChartLastTime)
                    continue;

                IBinanceKline kline = klines[i];
                int pindex = chart.Series[0].Points.Count;

                if (klines[i].OpenTime > dtChartLastTime)
                {
                    chart.Series[0].Points.AddXY(kline.OpenTime, kline.High); // high
                    chart.Series[0].Points[pindex].YValues[1] = (double)kline.Low; // low
                    chart.Series[0].Points[pindex].YValues[2] = (double)kline.Open; // open
                    chart.Series[0].Points[pindex].YValues[3] = (double)kline.Close; // close

                    chart.Series[1].Points.AddXY(kline.OpenTime, malongvalues[i]); //long ma
                    chart.Series[2].Points.AddXY(kline.OpenTime, mashortvalues[i]); //short ma
                    chart.Series[4].Points.AddXY(kline.OpenTime, mavolumevalues[i]); //volume ma

                    chart.Series[3].Points.AddXY(kline.OpenTime, kline.BaseVolume); // volume
                }
                else
                {
                    chart.Series[0].Points[pindex - 1].YValues[0] = (double)kline.High; // low
                    chart.Series[0].Points[pindex - 1].YValues[1] = (double)kline.Low; // low
                    chart.Series[0].Points[pindex - 1].YValues[2] = (double)kline.Open; // open
                    chart.Series[0].Points[pindex - 1].YValues[3] = (double)kline.Close; // close

                    chart.Series[1].Points[pindex - 1].YValues[0] = (double)malongvalues[i]; //long ma
                    chart.Series[2].Points[pindex - 1].YValues[0] = (double)mashortvalues[i]; //short ma
                    chart.Series[4].Points[pindex - 1].YValues[0] = (double)mavolumevalues[i]; //volume ma

                    chart.Series[3].Points[pindex - 1].YValues[0] = (double)kline.BaseVolume; // volume
                }
            }

            while (chart.Series[0].Points.Count > BAR_COUNT)
            {
                chart.Series[0].Points.RemoveAt(0);
                chart.Series[1].Points.RemoveAt(0);
                chart.Series[2].Points.RemoveAt(0);
                chart.Series[3].Points.RemoveAt(0);
                chart.Series[4].Points.RemoveAt(0);
            }

            chart.ChartAreas[0].RecalculateAxesScale();
            chart.ChartAreas[1].RecalculateAxesScale();
        }
                
        private void timerUpdate_Tick(object sender, EventArgs e)
        {
            if (gErrorOccured)
            {
                stopStrategy();
                MessageBox.Show(this, gErrMsg, "Error");
                gErrorOccured = false;
                return;
            }
            else
            {
                if (gWorkingStatus == 1 && btnStart.Enabled == false)
                    btnStart.Enabled = true;
            }

            updatePriceLabels();
            updateVolumeLabels();
            updateSupportLabels();
            updateBalanceListView();
            updateOrdersListView();
            updatePositionsListView();

            updateAllChart();
        }
        

        private decimal GetTickSize(string symbol)
        {
            foreach(BinanceFuturesUsdtSymbol binanceSymbol in gExchangeInfo.Symbols)
            {
                if (binanceSymbol.Name == symbol)
                {
                    return binanceSymbol.PriceFilter.TickSize;
                }
            }

            return 0.1m;
        }

        private BinanceFuturesUsdtSymbol GetSymbolInfo(string symbol)
        {
            foreach (BinanceFuturesUsdtSymbol binanceSymbol in gExchangeInfo.Symbols)
            {
                if (binanceSymbol.Name == symbol)
                {
                    return binanceSymbol;
                }
            }
            return null;
        }
        
        private decimal GetMinOrderQuantity(BinanceFuturesSymbol symbolInfo)
        {
            if (symbolInfo == null)
                return 0m;
            return symbolInfo.LotSizeFilter.MinQuantity;
        }

        /// <summary>
        /// check lot size
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="lot_quantity">lot_size</param>
        /// <param name="orderType"></param>
        /// <param name="filterdQuantity"></param>
        /// <returns></returns>
        private bool CheckOrderQuantityFilter(string symbol, decimal lot_quantity, OrderType orderType, out decimal filterdQuantity)
        {
            filterdQuantity = lot_quantity;
            BinanceFuturesSymbol symbolInfo = GetSymbolInfo(symbol);

            if (symbolInfo == null)
            {
                Utils.addLog($"Quantity: cannot find symbol {symbol}");
                return false;
            }

            decimal MaxQuantity = symbolInfo.LotSizeFilter.MaxQuantity;
            decimal MinQuantity = symbolInfo.LotSizeFilter.MinQuantity;
            decimal StepSize = symbolInfo.LotSizeFilter.StepSize;

            if (lot_quantity > MaxQuantity)
            {
                Utils.addLog($"{symbol} Invalid order quantity {lot_quantity}. (Great than MaxQuantity:{MaxQuantity})");
                return false;
            }

            if (lot_quantity < MinQuantity)
            {
                Utils.addLog($"{symbol} Invalid order quantity {lot_quantity}. (Less than MinQuantity:{MinQuantity})");
                return false;
            }

            filterdQuantity = ((int)(lot_quantity / StepSize)) * StepSize;
            
            return true;

        }

        private bool CheckLastOrderTime(string symbol)
        {
            // can place order after 1000 ms
            if (gdicLastOrderTime.ContainsKey(symbol) == false)
            {
                gdicLastOrderTime[symbol] = DateTime.Now;
                return true;
            }

            TimeSpan diff = DateTime.Now - gdicLastOrderTime[symbol];
            if (diff.TotalMilliseconds < 1000)
                return false;

            gdicLastOrderTime[symbol] = DateTime.Now;
            return true;
        }

        private int GetWaitingSymbolsCount_Margin()
        {
            int count = 0;
            for(int i = 0; i < gdicWaitingBalance_Margin.Values.Count; i++)
            {
                if (gdicWaitingBalance_Margin.Values.ElementAt(i))
                    count++;
            }
            return count;
        }

        private string[] GetWaitingSymbols_Margin()
        {
            List<string> lstRet = new List<string>();

            for (int i = 0; i < gdicWaitingBalance_Margin.Keys.Count; i++)
            {
                string symbol = gdicWaitingBalance_Margin.Keys.ElementAt(i);
                if (gdicWaitingBalance_Margin[symbol])
                    lstRet.Add(symbol);
            }

            return lstRet.ToArray();
        }

        private void SetWaitingSymbolsReceived_Margin(string[] symbols)
        {
            for (int i = 0; i < symbols.Count(); i++)
            {
                gdicWaitingBalance_Margin[symbols.ElementAt(i)] = false;
            }
        }

        private void CancelAllOpenOrders_Spot(string symbol)
        {
            if (clientFuture == null)
                return;
            
            Utils.addLog($"Cancel all orders : {symbol}");
            WebCallResult<BinanceFuturesCancelAllOrders> result = clientFuture.FuturesUsdt.Order.CancelAllOrders(symbol);
            if (result.Success == false)
                Utils.addLog(result.Error.ToString());

            Console.WriteLine("cancel finished");
        }
        
        async public void placeStopLimitOrder_Future(string symbol, decimal quantity, OrderSide orderSide, decimal stopPrice, decimal limitPrice)
        {
            if (clientFuture == null)
                return;

            gOrdering_Spot = true;
            gdicOrderReceived[symbol] = false;
            Utils.addLog($"Send order {symbol}, {orderSide}, Qty {quantity}");

            var result = await clientFuture.FuturesUsdt.Order.PlaceOrderAsync(
                symbol,
                orderSide,
                OrderType.Stop,
                quantity: quantity,
                timeInForce: TimeInForce.GoodTillCancel,
                price: limitPrice,
                stopPrice: stopPrice
                );

            Utils.addLog($"Order result {(result.Success ? "Success" : "Failed")}");
            if (result.Success == false)
            {
                gdicStatus[symbol] = "Order failed";
                Utils.addLog($"{symbol}, {result.Error}");

                // -3007: You have pending transcation, please try again later. 
                if (result.Error.ToString().StartsWith("-3007") && orderSide == OrderSide.Buy)
                {
                    gdicNewCandle[symbol] = true;
                    Utils.addLog($"{symbol} allow sell again.");
                }

                gOrdering_Spot = false;
                return;
            }
            else
            {
                if (gdicOrderReceived[symbol] == false)
                {
                    if (orderSide == OrderSide.Buy)
                    {
                        Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(symbol);
                    //    symbol_setting.openPrice = limitPrice;
                        Utils.addLog($"{symbol_setting.product} openPrice {symbol_setting.openPrice}");
                        Utils.SetSymbolSetting(symbol, symbol_setting);
                        Utils.SaveSettings();
                    }

                    Utils.addLog($"{symbol} order is not updated.");
                    updateAllBalanceOrders_Spot();
                }
            }

            gOrdering_Spot = false;
        }
        
        private void updateAllBalanceOrders_Spot()
        {
            Thread.Sleep(2000);

            int retry = 0;
            // Balance
            while (retry++ < 3)
            {
                WebCallResult<BinanceFuturesAccountInfo> accountInfo = clientFuture.FuturesUsdt.Account.GetAccountInfo();
                if (accountInfo.Success == false)
                {
                    Utils.addLog($"SpotBalance {accountInfo.Error}");
                    continue;
                }
                AllBalancesReceived_Spot(accountInfo.Data.Assets);
                break;
            }
            // OrdersList (Spot)
            retry = 0;
            while (retry++ < 3)
            {
                WebCallResult<IEnumerable<BinanceFuturesOrder>> ordersList = clientFuture.FuturesUsdt.Order.GetOpenOrders();
                if (ordersList.Success == false)
                {
                    Utils.addLog($"OpenOrders {ordersList.Error}");
                    continue;
                }
                AllOrdersReceived(ordersList.Data);
                break;
            }
        }
        
        async public void placeMarketOrder_Future(string symbol, decimal quantity, OrderSide orderSide)
        {
            if (clientFuture == null)
                return;

            gOrdering_Spot = true;
            gdicOrderReceived[symbol] = false;

            BidAskPrice price = gdicPrices[symbol];
            
            Utils.addLog($"Send Market {symbol}, {orderSide}, {(orderSide == OrderSide.Buy ? price.Ask : price.Bid)}, {quantity}");
            var result = await clientFuture.FuturesUsdt.Order.PlaceOrderAsync(symbol, orderSide, OrderType.Market, quantity: quantity);
            Utils.addLog($"Order result{(result.Success ? "Success":"Failed")}");
            if (result.Success == false)
                Utils.addLog($"{symbol}, {result.Error}");
            else
            {
                if (gdicOrderReceived[symbol] == false)
                {
                    if (orderSide == OrderSide.Buy)
                    {
                        Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(symbol);
                        symbol_setting.openPrice = price.Ask;
                        Utils.addLog($"{symbol_setting.product} openPrice {symbol_setting.openPrice}");
                        Utils.SetSymbolSetting(symbol, symbol_setting);
                        Utils.SaveSettings();
                    }

                    Utils.addLog($"{symbol} order is not updated.");
                   updateAllBalanceOrders_Spot();
                }
            }

            gOrdering_Spot = false;
        }
        
        private void btnSpotBuyMarket_Click(object sender, EventArgs e)
        {
            if (clientFuture == null)
            {
                MessageBox.Show("client is null");
                return;
            }

            Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(0);
            placeMarketOrder_Future(symbol_setting.product, symbol_setting.quantity, OrderSide.Buy);
        }

        private void btnSpotSellMarket_Click(object sender, EventArgs e)
        {
            if (clientFuture == null)
            {
                MessageBox.Show("client is null");
                return;
            }
            Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(0);
            placeMarketOrder_Future(symbol_setting.product, symbol_setting.quantity, OrderSide.Sell);
        }

        private void btnSpotBuyStop_Click(object sender, EventArgs e)
        {
            if (clientFuture == null)
            {
                MessageBox.Show("client is null");
                return;
            }

            decimal stopPrice;
            decimal limitPrice;

            stopPrice = decimal.Parse(txtStopPrice_Spot.Text);
            limitPrice = decimal.Parse(txtLimitPrice_Spot.Text);

            Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(0);
            decimal quantity = (decimal)symbol_setting.quantity;
//            placeStopLimitOrder_Future(symbol_setting.product, quantity, OrderSide.Buy, stopPrice, limitPrice);
        }

        private void btnSpotSellStop_Click(object sender, EventArgs e)
        {
            if (clientFuture == null)
            {
                MessageBox.Show("client is null");
                return;
            }

            decimal stopPrice;
            decimal limitPrice;

            stopPrice = decimal.Parse(txtStopPrice_Spot.Text);
            limitPrice = decimal.Parse(txtLimitPrice_Spot.Text);

            Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(0);
            decimal quantity = (decimal)symbol_setting.quantity;
//            placeStopLimitOrder_Future(symbol_setting.product, quantity, OrderSide.Sell, stopPrice, limitPrice);
        }

        private void btnSpotCancellAll_Click(object sender, EventArgs e)
        {
            Utils.SymbolSetting symbol_setting = Utils.GetSymbolSetting(0);
            CancelAllOpenOrders_Spot(symbol_setting.product);
        }        

        private void cmbTimeFrame_SelectedIndexChanged(object sender, EventArgs e)
        {
            CheckSettingChanged();
        }

        private void txtParam_TextChanged(object sender, EventArgs e)
        {
            //if (gWorkingStatus == -1)
            //    return;

            CheckSettingChanged();
        }

        private void chkChartOnly_CheckedChanged(object sender, EventArgs e)
        {
            //if (gWorkingStatus == -1)
            //    return;

            CheckSettingChanged();
        }

        private void CheckSettingChanged()
        {
            for (int i = 0; i < Utils.SYMBOL_COUNT; i++)
            {
                SettingsUI setting_ui = arrSettingsUI[i];
                Utils.SymbolSetting setting_fromui = GetSymbolSettingFromUI(i);
                Utils.SymbolSetting setting_saved = Utils.GetSymbolSetting(i);

                if (setting_saved.product == setting_fromui.product &&
                    setting_saved.timeFrameIndex == setting_fromui.timeFrameIndex &&
                    setting_saved.supportTimeFrameIndex == setting_fromui.supportTimeFrameIndex &&
                    setting_saved.maLongPeriod == setting_fromui.maLongPeriod && 
                    setting_saved.quantity == setting_fromui.quantity &&
                    setting_saved.buyUpTicks == setting_fromui.buyUpTicks &&
                    setting_saved.sellDownTicks == setting_fromui.sellDownTicks &&
                    setting_saved.gainProfit == setting_fromui.gainProfit &&
                    setting_saved.chartOnly == setting_fromui.chartOnly &&
                    setting_saved.leverage == setting_fromui.leverage &&
                    setting_saved.maShortPeriod == setting_fromui.maShortPeriod &&
                    setting_saved.maSupportPeriod == setting_fromui.maSupportPeriod &&
                    setting_saved.maVolumePeriod == setting_fromui.maVolumePeriod &&
                    setting_saved.maPriceTimeLimit == setting_fromui.maPriceTimeLimit &&
                    setting_saved.maVolumeTimeLimit == setting_fromui.maVolumeTimeLimit)
                {
                    setting_ui.btnUpdate.Visible = false;
                }else
                {
                    setting_ui.btnUpdate.Visible = true;
                }
            }
        }

        private void btnUpdateSetting_Click(object sender, EventArgs e)
        {
            int index = -1;

            for(int i = 0; i < Utils.SYMBOL_COUNT; i++)
            {
                if (arrSettingsUI[i].btnUpdate == sender)
                {
                    index = i;
                    break;
                }
            }

            if (index == -1)
                return;

            if (CheckSettingsUI(index) == false)
            {
                MessageBox.Show(this, "Please check the inputs.", "Error");
                return;
            }

            Utils.SymbolSetting setting_org = Utils.GetSymbolSetting(index);
            int leverage_org = setting_org.leverage;

            Utils.SymbolSetting symbol_setting = GetSymbolSettingFromUI(index);
            Utils.SetSymbolSetting(index, symbol_setting);
            SaveSettings();

            if (gWorkingStatus >= 0 && symbol_setting.product.Length > 0)
            {
                if (symbol_setting.chartOnly == false && gdicStatus[symbol_setting.product] == STATUS_CHARTONLY)
                    gdicStatus[symbol_setting.product] = STATUS_WAITSIGNAL;

                if (symbol_setting.chartOnly == true && gdicStatus[symbol_setting.product] != STATUS_CHARTONLY)
                    gdicStatus[symbol_setting.product] = STATUS_CHARTONLY;
            }
            
            arrSettingsUI[index].btnUpdate.Visible = false;

            if (gWorkingStatus > 0 && symbol_setting.product.Length > 0 && leverage_org != symbol_setting.leverage)
            {
                WebCallResult<BinanceFuturesInitialLeverageChangeResult> leverageResult = clientFuture.FuturesUsdt.ChangeInitialLeverage(symbol_setting.product, symbol_setting.leverage);
                if (leverageResult.Success == false)
                {
                    Utils.addLog($"{symbol_setting.product}, Failed to change leverage {leverageResult.Error}");
                    return;
                }

                setSymbolLeverage(symbol_setting.product, symbol_setting.leverage);
                gPositionUpdated = true;
            }
        }
        
        private void btnSellMarket_Click(object sender, EventArgs e)
        {
            int index = -1;

            for (int i = 0; i < Utils.SYMBOL_COUNT; i++)
            {
                if (arrSettingsUI[i].btnSell == sender)
                {
                    index = i;
                    break;
                }
            }

            if (index == -1)
                return;

            Utils.SymbolSetting symbol_setting = GetSymbolSettingFromUI(index);
            if (symbol_setting.product.Length == 0)
                return;

            placeMarketOrder_Future(symbol_setting.product, symbol_setting.quantity, OrderSide.Sell);
        }

        private void btnBuyMarket_Click(object sender, EventArgs e)
        {
            int index = -1;

            for (int i = 0; i < Utils.SYMBOL_COUNT; i++)
            {
                if (arrSettingsUI[i].btnBuy == sender)
                {
                    index = i;
                    break;
                }
            }

            if (index == -1)
                return;

            Utils.SymbolSetting symbol_setting = GetSymbolSettingFromUI(index);
            if (symbol_setting.product.Length == 0)
                return;

            placeMarketOrder_Future(symbol_setting.product, symbol_setting.quantity, OrderSide.Buy);
        }

        private void btnCancelAll_Click(object sender, EventArgs e)
        {
            int index = -1;

            for (int i = 0; i < Utils.SYMBOL_COUNT; i++)
            {
                if (arrSettingsUI[i].btnCancel == sender)
                {
                    index = i;
                    break;
                }
            }

            if (index == -1)
                return;

            Utils.SymbolSetting symbol_setting = GetSymbolSettingFromUI(index);
            if (symbol_setting.product.Length == 0)
                return;

            CancelAllOpenOrders_Spot(symbol_setting.product);
        }
    }
}
