#region using
using Infragistics.Shared;
using Infragistics.Win;
using Infragistics.Win.UltraWinStatusBar;

using OCTech.Common;
using OCTech.Controls;
using OCTech.Interfaces;
using OCTech.OBD2.Applications.Plugin;
using OCTech.OBD2.Applications.Properties;
using OCTech.PIDPlugin;
using OCTech.Plugin;
using OCTech.Utilities;
using OCTech.Utilities.Filter;
using OCTech.Utilities.Fuel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
#endregion

namespace OCTech.OBD2.Applications
{
	public class MainForm : Form, IStatusDisplay, IPluginHost, IColorTheme, IErrorDisplay, IWindowManager, IPIDPluginContext, IErrorHandler
	{
		public event EventHandler<EventArgs> ColorThemeChanged;
		public event EventHandler ApplicationClosing;

		#region Variables

		private static string m_ApplicationName = null;
		private static string m_MyDocumentsPath;
		private static string m_ApplicationDataDirectory;

		private bool m_isDisposed;
		private string m_PIDPluginDataPath;
		private bool m_loading = true;
		private Preferences m_prefences;
		private static Form m_mainForm = null;
		private bool m_ForceClose = false;

		private ScanTool m_scanTool = new ScanTool();

		private CustomListView customListView;
		private ToolStripContainer toolStripContainer;
		private ToolStripMenuItem vehicleManagerToolStripMenuItem;
		private ToolStripMenuItem sensorCalibrationToolStripMenuItem;
		private ToolStripMenuItem userDefinedPIDsToolStripMenuItem;
		private ToolStripMenuItem pluginManagerToolStripMenuItem;
		private ToolStripSeparator toolStripSeparatorToolsMenu;

		private string m_statusBarBanner = string.Empty;
		private MainForm.UltraStatusBarEx m_Port_ECU_Status;

		private Panel pagePanel;
		private Dictionary<string, ToolStripMenuItem> m_StripMenus = new Dictionary<string, ToolStripMenuItem>();
		private Dictionary<string, Control> m_TabPages = new Dictionary<string, Control>();

		private ConnectionStatus f000166;
		private LogsPage m_logs;
		private PIDMonitor m_pidMonitor;
		private static IContext m_StaticContext = null;
		private IContext m_context;
		private MessageDispatcher m_messageDispatcher = new MessageDispatcher();

		private float f0000a3;
		private int m_reconnectCount;
		private bool m_AskReconnect;
		private System.Windows.Forms.Timer m_timer;

		private TableLayoutPanel tableLayoutPanel;
		private bool m_FullScreen;
		private FormState m_FormState = new FormState();
		private CustomDashboard m_dashboard;

		private ToolStripMenuItem windowToolStripMenuItem;
		private SetupPage m_setupPage;

		private IContainer m_container;

		private IIRFilter f00015d = new IIRFilter(new double[1] { 0.9 });
		private RefreshLimiter f000168 = new RefreshLimiter(250);
		private const string f000015 = "1.00";
		private const int f000022 = 0;
		private const string f00014f = "OCTech.Plugin.*.dll";

		private VehicleManagement vehicleManagement;
		private DiagnosticsPage m_diagnostic;
		private MonitorsPage m_monitors;
		private SensorCalibration sensorCalibration;
		private MenuStrip menuStrip;
		private ToolStripMenuItem fileToolStripMenuItem;
		private ToolStripMenuItem exitToolStripMenuItem;
		private ToolStripMenuItem setupToolStripMenuItem;
		private ToolStripMenuItem diagnosticsToolStripMenuItem;
		private ToolStripMenuItem monitorsToolStripMenuItem;
		private ToolStripMenuItem dashboardToolStripMenuItem;
		private ToolStripMenuItem logsToolStripMenuItem;
		private ToolStripMenuItem toolsToolStripMenuItem;
		private ToolStripMenuItem preferencesToolStripMenuItem;
		private ToolStripMenuItem connectionToolStripMenuItem;
		private ToolStripMenuItem connectToolStripMenuItem;
		private ToolStripMenuItem disconnectToolStripMenuItem;
		private ToolStripMenuItem viewToolStripMenuItem;
		private ToolStripMenuItem showStatusBarToolStripMenuItem;
		private ToolStripMenuItem showListViewToolStripMenuItem;
		private ToolStripMenuItem fullScreenToolStripMenuItem;
		private ToolStripSeparator toolStripSeparator1;
		private ToolStripMenuItem powerSaveSetupToolStripMenuItem;
		private ToolStripMenuItem pidInspectorToolStripMenuItem;
		private ToolStripSeparator toolStripSeparatorToolsMenu2;
		private ListPluginClassInfo m_PluginsInfo;
		private UltraStatusBar m_statusPortECU;
		#endregion

		#region Properties
		public static string ApplicationName
		{
			get
			{
				if (string.IsNullOrEmpty(m_ApplicationName))
					m_ApplicationName = ((AssemblyTitleAttribute[])Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), true))[0].Title;
				return m_ApplicationName;
			}
		}

		public Form TopLevelForm
		{
			get { return this; }
		}
		public static Form Form
		{
			get { return m_mainForm; }
		}
		public static IContext StaticContext
		{
			get { return m_StaticContext; }
		}

		public static string GetSpecialFolder(Environment.SpecialFolder type)
		{
			// Environment.SpecialFolder.Desktop
			// Environment.SpecialFolder.CommonApplicationData
			// Environment.SpecialFolder.Personal
			string path = Environment.GetFolderPath(type);
			return Application.StartupPath;
		}

		public static string ApplicationDataDirectory
		{
			get
			{
				if (string.IsNullOrEmpty(m_ApplicationDataDirectory))
				{
					string dataFolder = Path.Combine(
						GetSpecialFolder(Environment.SpecialFolder.CommonApplicationData),
						ApplicationName
						);
					FileUtility.CreateDirectory(dataFolder);
					m_ApplicationDataDirectory = dataFolder;
				}
				return m_ApplicationDataDirectory;
			}
		}
		public string PIDPluginDataPath
		{
			get
			{
				if (string.IsNullOrEmpty(m_PIDPluginDataPath))
				{
					string path = Path.Combine(ApplicationDataDirectory, "PIDPlugins");
					FileUtility.CreateDirectory(path);
					m_PIDPluginDataPath = path;
				}
				return m_PIDPluginDataPath;
			}
		}

		public static string MyDocumentsPath
		{
			get
			{
				if (string.IsNullOrEmpty(m_MyDocumentsPath))
				{
					string path = Path.Combine(
						GetSpecialFolder(Environment.SpecialFolder.Personal),
						ApplicationName
						);
					DirectoryInfo directoryInfo = new DirectoryInfo(path);
					if (!directoryInfo.Exists)
						directoryInfo.Create();
					m_MyDocumentsPath = path;
				}
				return m_MyDocumentsPath;
			}
		}

		private string getPIDPluginSearchPath
		{
			get { return Path.Combine(Application.StartupPath, "PIDPlugins"); }
		}

		private string getPreferencesPath
		{
			get { return Path.Combine(ApplicationDataDirectory, "Preferences.xml"); }
		}
		private string getDataPath
		{
			get { return Path.Combine(ApplicationDataDirectory, "Data.xml"); }
		}
		#endregion

		#region DisplayException / DisplayError / statusPortECU
		public void DisplayException(Exception p0)
		{
			DisplayError(p0);
		}

		public void DisplayError(Exception ex)
		{
			displayError(ex);
		}

		private void displayError(Exception ex)
		{
			if (m_isDisposed)
				return;

			if (ex is LostCommunicationException)
			{
				if (m_prefences.Communications.InterfaceCommunicationType == InterfaceCommunicationType.Wifi
					&& m_reconnectCount < 2
					)
				{	// Try 2 reconnection for WiFi
					++m_reconnectCount;
					Thread.Sleep(1000);
					invokeCall((MethodInvoker)(() => m_setupPage.ConnectToScanTool()));
				}
				else
				{
					if (!m_AskReconnect)
					{
						LostCommunicationException commException = (LostCommunicationException)ex;
						invokeCall((MethodInvoker)(() =>
						{
							m_AskReconnect = true;
							try
							{
								using (PopupDialogForm popup = new PopupDialogForm(false))
								{
									popup.StartPosition = FormStartPosition.CenterParent;
									popup.Owner = this;
									ConnectionErrorDisplay connectionErrorDisplay = new ConnectionErrorDisplay(
										ConnectionErrorType.LostCommunications,
										commException.Message,
										commException.Resolution,
										commException.HelpLink
										);
									popup.SetUserControl(connectionErrorDisplay);
									popup.ShowDialog(this);
									clearError();

									if (connectionErrorDisplay.ConnectionErrorDisplayAction == ConnectionErrorDisplayAction.Reconnect)
										m_setupPage.ConnectToScanTool();
									else
										m_setupPage.DisconnectFromScanTool();
								}
							}
							finally
							{
								m_AskReconnect = false;
							}
						}));
					}
				}
			}
			else if (ex is InvalidDataLengthException
					|| ex is TimeoutException
					|| ex is IncorrectResponseException
					)
			{
				m_diagnostic.WriteToLog(ex.Message);
			}
			else
			{
				if (m_diagnostic != null)
					m_diagnostic.WriteToLog(ex.Message);

				string message = ex.Message;
				if (!string.IsNullOrEmpty(message))
					message = message.Replace("\r", string.Empty).Replace("\n", string.Empty);
				statusErrorECU = message;
			}
		}

		private bool statusPortECU
		{
			get { return m_statusPortECU.Visible; }
			set
			{
				m_statusPortECU.Visible = value;
				if (value)
					toolStripContainer.ContentPanel.Padding = new Padding(0, 0, 0, m_statusPortECU.Height);
				else
					toolStripContainer.ContentPanel.Padding = new Padding(0);
			}
		}

		#endregion

		#region Windows Full/View
		private bool fullScreen
		{
			get { return m_FullScreen; }
			set
			{
				m_FullScreen = value;
				if (m_FullScreen)
				{
					m_FormState.Save(this);
					m_FormState.Maximize(this);
				}
				else
					m_FormState.Restore(this);
			}
		}

		private bool showListViewPanel
		{
			get { return (double)tableLayoutPanel.ColumnStyles[0].Width != 0.0; }
			set
			{
				if (value)
					tableLayoutPanel.ColumnStyles[0].Width = f0000a3;
				else
				{
					f0000a3 = tableLayoutPanel.ColumnStyles[0].Width;
					tableLayoutPanel.ColumnStyles[0].Width = 0.0f;
				}
			}
		}
		#endregion

		#region Public Properties
		public ConnectionStatus ConnectedStatus
		{
			set
			{
				if (f000166 == value)
					return;
				f000166 = value;
				invokeCall((MethodInvoker)(() =>
					{
						m_Port_ECU_Status.ConnectionStatus = value;
					}));
			}
		}

		public string CurrentOperationStatus
		{
			set
			{
				invokeCall((MethodInvoker)(() =>
				{
					if (value.Equals(OCTech.OBD2.Applications.Properties.Resources.SystemReadyText))
						value = string.Empty;

					if (!m_Port_ECU_Status.CurrentOperationText.Equals(value))
						m_Port_ECU_Status.CurrentOperationText = value;
				}));
			}
		}
		public int MonitorRoundTripTime
		{
			set
			{
				invokeCall((MethodInvoker)(() =>
				{
					m_Port_ECU_Status.TimingText = string.Format("{0} msec", value);
				}));
			}
		}

		public double MonitorPIDsPerSecond
		{
			set
			{
				invokeCall((MethodInvoker)(() =>
				{
					m_Port_ECU_Status.TimingText = string.Format("{0} PID/sec", value);
				}));

			}
		}

		public IPluginContext Context
		{
			get { return m_context as IPluginContext; }
		}

		public ColorTheme ActiveTheme
		{
			get { return Themer.GetColorTheme(m_prefences.General.ColorMode); }
		}

		public IErrorHandler ErrorHandler
		{
			get { return (IErrorHandler)this; }
		}
		#endregion

		#region ECU indicator error
		private string statusErrorECU
		{
			set
			{
				invokeCall((MethodInvoker)(() =>
					{
						if (string.IsNullOrEmpty(value))
							value = m_statusErrorECU;
						m_Port_ECU_Status.ErrorText = value;
					}));
			}
		}
		private string m_statusErrorECU
		{
			get { return m_statusBarBanner; }
			set
			{
				m_statusBarBanner = value;
				ClearError();
			}
		}
		#endregion

		#region Constructors
		public MainForm()
			: this(null)
		{
		}

		public MainForm(string[] arguments)
		{
			m_loading = true;
			Visible = false;
			m_prefences = new Preferences(getPreferencesPath);
			Localization.SetCulture(m_prefences.General.Culture);

			InitializeComponent();

			m_Port_ECU_Status = new MainForm.UltraStatusBarEx(m_statusPortECU);

			this.Icon = OCTech.OBD2.Applications.Properties.Resources.Application;
			this.Text = MainForm.ApplicationName;
			string titleBarCompany = Localization.GetTitleBarCompany(m_prefences.General.Culture);
			if (!string.IsNullOrEmpty(titleBarCompany))
				Text = string.Format("{0} - {1}", Text, titleBarCompany);
			m_statusErrorECU = Localization.GetStatusBarBanner(m_prefences.General.Culture);

			m_mainForm = (Form)this;
			processCommandOptions(arguments);

			this.Disposed += new EventHandler(mainForm_Disposed);

			Version version = m_scanTool.GetType().Assembly.GetName().Version;
			int unlock = version.Major ^ version.Minor ^ version.Build ^ version.Revision;
			m_scanTool.Unlock(unlock);

			FileUtility.PathResolverReplacements.Add("[ApplicationName]", MainForm.ApplicationName);

			LoggableItem.RegisterLoggableItem(
				typeof(PIDLoggableItem),
				new LoggableItemXmlReader(PIDLoggableItem.FromXml)
				);
			LoggableItem.RegisterLoggableItem(
				typeof(FuelLoggableItem),
				new LoggableItemXmlReader(FuelLoggableItem.FromXml)
				);
			LoggableItem.RegisterLoggableItem(
				typeof(UserPIDLoggableItem),
				new LoggableItemXmlReader(UserPIDLoggableItem.FromXml)
				);
			LoggableItem.RegisterLoggableItem(
				typeof(PIDPluginLoggableItem),
				new LoggableItemXmlReader(PIDPluginLoggableItem.FromXml)
				);

			if (m_prefences.Layout.TouchScreenSize)
			{
				customListView.ItemSpacing = ListViewItemSpacing.Small;
				toolStripContainer.TopToolStripPanelVisible = false;
			}

			customListView.Add(
				OCTech.OBD2.Applications.Properties.Resources.SetupPageName,
				"Setup",
				OCTech.OBD2.Applications.Properties.Resources.p000075
				);
			customListView.Add(
				OCTech.OBD2.Applications.Properties.Resources.DiagPageName,
				"Diagnostics",
				OCTech.OBD2.Applications.Properties.Resources.p000026
				);
			customListView.Add(
				OCTech.OBD2.Applications.Properties.Resources.MonitorsPageName,
				"Monitors",
				OCTech.OBD2.Applications.Properties.Resources.p000045
				);
			customListView.Add(
				OCTech.OBD2.Applications.Properties.Resources.DashboardPageName,
				"Dashboard",
				OCTech.OBD2.Applications.Properties.Resources.p000015
				);
			customListView.Add(
				OCTech.OBD2.Applications.Properties.Resources.LogsPageName,
				"Logs",
				OCTech.OBD2.Applications.Properties.Resources.p000043
				);
			customListView.Add(
				OCTech.OBD2.Applications.Properties.Resources.ExitText,
				"Exit",
				OCTech.OBD2.Applications.Properties.Resources.p000032,
				true
				);

			customListView.ItemSelected += new EventHandler<CustomListViewItemSelectedEventArgs>(customListView_ItemSelected);
			customListView.Font = this.Font;

			CurrentOperationStatus = OCTech.OBD2.Applications.Properties.Resources.SystemReadyText;
			ConnectedStatus = ConnectionStatus.NotConnected;

			if (m_prefences.Layout.SelectedPageKey == "Exit"
			|| !m_prefences.Layout.RememberLastPageOnStartup
			|| string.IsNullOrEmpty(m_prefences.Layout.SelectedPageKey)
				)
				m_prefences.Layout.SelectedPageKey = "Setup";

			m_scanTool.CommunicationErrorOccurred += new EventHandler<MessageEventArgs>(m_scanTool_CommunicationErrorOccurred);
			m_scanTool.SelectECU += new EventHandler<SelectedECUEventArgs>(m_scanTool_SelectECU);
			m_scanTool.ConnectionChanged += new EventHandler(m_scanTool_ConnectionChanged);
			m_scanTool.ConnectionStatusChanged += new EventHandler<ConnectionStatusChangedEventArgs>(m_scanTool_ConnectionStatusChanged);

			m_pidMonitor = new PIDMonitor(m_scanTool);
			m_pidMonitor.SetCulture(m_prefences.General.Culture);
			m_pidMonitor.ErrorOccurred += new EventHandler<OCTech.Utilities.ExceptionEventArgs>(utilities_ErrorOccurred);
			m_pidMonitor.NewPIDResponseArrived += new EventHandler<PIDResponseEventArgs>(m_pidMonitor_NewPIDResponseArrived);
			m_pidMonitor.NewPIDTimingAvailable += new EventHandler<PIDMonitorTimingEventArgs>(m_pidMonitor_NewPIDTimingAvailable);
			m_pidMonitor.Playback.RecordedPlaybackChanged += new EventHandler<RecordedDataPlayBackEventArgs>(m_pidMonitor_RecordedPlaybackChanged);

			FuelCalculator fuelCalculator = new FuelCalculator(m_pidMonitor, m_scanTool);
			fuelCalculator.ErrorOccurred += new EventHandler<OCTech.Utilities.ExceptionEventArgs>(utilities_ErrorOccurred);

			vehicleManagement = new VehicleManagement(this, m_scanTool, m_pidMonitor, fuelCalculator, this, MainForm.MyDocumentsPath);

			m_context = new AppContext(m_prefences, this, m_scanTool, m_pidMonitor, fuelCalculator, MainForm.MyDocumentsPath, MainForm.ApplicationDataDirectory, m_messageDispatcher, this, this, vehicleManagement);
			MainForm.m_StaticContext = m_context;

			TripManager.Initialize(m_context.ScanTool, m_context.PidMonitor, m_context.FuelCalculator, MainForm.ApplicationDataDirectory);
			TripManager.ErrorOccurred += new EventHandler<OCTech.Utilities.ExceptionEventArgs>(utilities_ErrorOccurred);

			pluginInitialize();

			sensorCalibration = new SensorCalibration();
			sensorCalibration.Initialize(m_context);

			DispatchPID.Initialize(m_context);

			m_setupPage = new SetupPage(m_context);
			m_setupPage.ColorModeChanged += new EventHandler<ColorModeChangedEventArgs>(m_setupPage_ColorModeChanged);

			m_diagnostic = new DiagnosticsPage(m_context);
			m_monitors = new MonitorsPage(m_context);
			m_dashboard = new CustomDashboard(m_context);
			m_logs = new LogsPage(m_context);

			m_TabPages.Add("Setup", m_setupPage);
			m_TabPages.Add("Diagnostics", m_diagnostic);
			m_TabPages.Add("Monitors", m_monitors);
			m_TabPages.Add("Dashboard", m_dashboard);
			m_TabPages.Add("Logs", m_logs);

			pagePanel.SuspendLayout();

			addPageToPanel(m_setupPage);
			addPageToPanel(m_diagnostic);
			addPageToPanel(m_monitors);
			addPageToPanel(m_dashboard);
			addPageToPanel(m_logs);

			pagePanel.ResumeLayout();

			m_StripMenus.Add("Setup", setupToolStripMenuItem);
			m_StripMenus.Add("Diagnostics", diagnosticsToolStripMenuItem);
			m_StripMenus.Add("Monitors", monitorsToolStripMenuItem);
			m_StripMenus.Add("Dashboard", dashboardToolStripMenuItem);
			m_StripMenus.Add("Logs", logsToolStripMenuItem);

			setControlsText();
			setConnectButtonState();

			loadOCTechPlugins();
			if (m_PluginsInfo.Plugins.Count == 0 && !PIDPluginController.AreAnyPIDPluginAssembliesLoaded)
				pluginManagerToolStripMenuItem.Visible = false;

			if (!m_TabPages.ContainsKey(m_prefences.Layout.SelectedPageKey))
				m_prefences.Layout.SelectedPageKey = "Setup";
			switchPageByName(m_prefences.Layout.SelectedPageKey);
			customListView.SelectItem(m_prefences.Layout.SelectedPageKey);
			loadDataDashboard(getDataPath);

			try
			{
				m_pidMonitor.LoadSettings(MainForm.ApplicationDataDirectory);
			}
			catch { }

			m_setupPage.m001d9a(m_PluginsInfo);

			setColorTheme(ActiveTheme);
			m_scanTool.DebugEnabled = true;

			Gauge.ErrorOccurred += new EventHandler<Controls.ExceptionEventArgs>(controls_ErrorOccurred);
			PowerBar.ErrorOccurred += new EventHandler<Controls.ExceptionEventArgs>(controls_ErrorOccurred);
			RoundedLabel.ErrorOccurred += new EventHandler<Controls.ExceptionEventArgs>(controls_ErrorOccurred);
			LinearGauge.ErrorOccurred += new EventHandler<Controls.ExceptionEventArgs>(controls_ErrorOccurred);
		}

		/// <summary>
		/// Parse command line options
		///		-cp		delete preferences file
		///		-cd		delete data file
		/// </summary>
		/// <param name="args"></param>
		private void processCommandOptions(string[] args)
		{
			char[] minus = new char[1] { '-' };

			try
			{
				if (args == null || args.Length <= 1)
					return;
				for (int index = 1; index < args.Length; ++index)
				{
					string option = args[index].ToLower().Trim(minus);
					if (string.IsNullOrEmpty(option))
						continue;
					if (option == "cp")
						deleteFile(getPreferencesPath);
					else if (option == "cd")
						deleteFile(getDataPath);
				}
			}
			catch { }
		}

		private void deleteFile(string file)
		{
			if (File.Exists(file))
				File.Delete(file);
		}

		#region setControlsText
		private void setControlsText()
		{
			fileToolStripMenuItem.Text = UIResources.FileText;
			viewToolStripMenuItem.Text = UIResources.ViewText;
			windowToolStripMenuItem.Text = UIResources.WindowText;
			connectionToolStripMenuItem.Text = UIResources.ConnectionText;
			toolsToolStripMenuItem.Text = UIResources.ToolsText;
			setupToolStripMenuItem.Text = OCTech.OBD2.Applications.Properties.Resources.SetupPageName;
			diagnosticsToolStripMenuItem.Text = OCTech.OBD2.Applications.Properties.Resources.DiagPageName;
			monitorsToolStripMenuItem.Text = OCTech.OBD2.Applications.Properties.Resources.MonitorsPageName;
			dashboardToolStripMenuItem.Text = OCTech.OBD2.Applications.Properties.Resources.DashboardPageName;
			logsToolStripMenuItem.Text = OCTech.OBD2.Applications.Properties.Resources.LogsPageName;
			exitToolStripMenuItem.Text = OCTech.OBD2.Applications.Properties.Resources.ExitText;
			showStatusBarToolStripMenuItem.Text = UIResources.StatusBarText;
			showListViewToolStripMenuItem.Text = UIResources.ListViewText;
			fullScreenToolStripMenuItem.Text = UIResources.FullScreenText;
			connectToolStripMenuItem.Text = UIResources.ConnectText;
			disconnectToolStripMenuItem.Text = UIResources.DisconnectText;
			vehicleManagerToolStripMenuItem.Text = UIResources.VehicleManagerText;
			sensorCalibrationToolStripMenuItem.Text = UIResources.SensorCalibrationText;
			preferencesToolStripMenuItem.Text = UIResources.PreferencesText;
			userDefinedPIDsToolStripMenuItem.Text = UIResources.UserDefinedPIDsText;
			pidInspectorToolStripMenuItem.Text = UIResources.PIDInspectorText;
			powerSaveSetupToolStripMenuItem.Text = UIResources.PowerSaveSettingsText;
			pluginManagerToolStripMenuItem.Text = UIResources.PluginManagerText;
		}
		#endregion
		#endregion

		#region invokeCall
		private void invokeCall(MethodInvoker invoker)
		{
			if (m_isDisposed || Disposing)
				return;

			if (InvokeRequired)
			{
				Invoke((MethodInvoker)(() =>
				{
					try
					{
						invoker();
					}
					catch (Exception ex)
					{
						displayError(ex);
					}
				}));
			}
			else
			{
				try
				{
					invoker();
				}
				catch (Exception ex)
				{
					displayError(ex);
				}
			}
		}
		#endregion

		#region saveDataDashboard / loadDataDashboard
		private void saveDataDashboard()
		{
			using (FileStream fileStream = new FileStream(getDataPath, FileMode.Create))
			{
				XmlTextWriter xmlTextWriter = new XmlTextWriter(fileStream, Encoding.UTF8);
				xmlTextWriter.Formatting = Formatting.Indented;
				xmlTextWriter.Indentation = 2;
				xmlTextWriter.Namespaces = false;

				xmlTextWriter.WriteStartDocument();
				CultureInfo cultureInfo = CultureInfo.InvariantCulture;

				xmlTextWriter.WriteStartElement("Data");
				xmlTextWriter.WriteAttributeString("culture", cultureInfo.Name);
				xmlTextWriter.WriteAttributeString("version", "1.00");

				xmlTextWriter.WriteStartElement("Dashboard");
				m_context.FuelCalculator.ToXml(xmlTextWriter, cultureInfo);
				xmlTextWriter.WriteEndElement();

				xmlTextWriter.WriteEndElement();
				xmlTextWriter.Close();
			}
		}

		private void loadDataDashboard(string fileName)
		{
			try
			{
				if (!File.Exists(fileName))
					return;

				using (FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
				{
					XmlDocument xmlDocument = new XmlDocument();
					xmlDocument.Load(fileStream);
					XmlNode xmlData = XmlHelper.GetNodeByName("Data", xmlDocument.ChildNodes);
					if (xmlData == null)
						return;
					CultureInfo cultureInfo = CultureInfo.GetCultureInfo(xmlData.Attributes["culture"].Value);
					foreach (XmlNode xmlNode in xmlData.ChildNodes)
					{
						string name = xmlNode.Name;
						if (name != null && name == "Dashboard")
							m_context.FuelCalculator.FromXml(xmlNode, cultureInfo);
					}
				}
			}
			catch { }
		}
		#endregion

		#region switchDayNight
		private void switchDayNight(DateTime now)
		{
			if (m_prefences == null || !m_prefences.General.ColorModeSwitching.AutoSwitch)
				return;

			TimeSpan timeNow = now.TimeOfDay;
			TimeSpan dayStart = m_prefences.General.ColorModeSwitching.DayStart.TimeOfDay;
			TimeSpan nightStart = m_prefences.General.ColorModeSwitching.NightStart.TimeOfDay;
			ColorMode mode = ColorMode.Night;
			if (timeNow >= dayStart && timeNow < nightStart)
				mode = ColorMode.Day;

			if (mode != m_prefences.General.ColorMode)
				m_setupPage.SwitchColorMode(mode);
		}
		#endregion

		#region ClearError
		public void ClearError()
		{
			clearError();
		}
		private void clearError()
		{
			invokeCall((MethodInvoker)(() => statusErrorECU = string.Empty));
		}
		#endregion

		#region ToggleWindowMode / SetWindowMode
		public void ToggleWindowMode(WindowMode onWindowMode)
		{
			switch (onWindowMode)
			{
				case WindowMode.ListViewPanel:
					showListViewPanel = !showListViewPanel;
					break;
				case WindowMode.StatusBar:
					statusPortECU = !statusPortECU;
					break;
				case WindowMode.FullScreen:
					fullScreen = !fullScreen;
					break;
			}
		}

		public void SetWindowMode(WindowMode windowMode, bool isOn)
		{
			switch (windowMode)
			{
				case WindowMode.ListViewPanel:
					showListViewPanel = isOn;
					break;
				case WindowMode.StatusBar:
					statusPortECU = isOn;
					break;
				case WindowMode.FullScreen:
					fullScreen = isOn;
					break;
			}
		}
		#endregion

		#region ChangeDashboard
		public void ChangeDashboard(bool goForward)
		{
			m_dashboard.ChangeDashboard(goForward);
		}
		#endregion

		#region switchPageByName
		private void switchPageByName(string pageName)
		{
			if (m_TabPages.ContainsKey(pageName))
			{
				Control newPage = m_TabPages[pageName];
				Control currentPage = null;
				foreach (Control control in pagePanel.Controls)
				{
					if (control.Visible)
					{
						currentPage = control;
						break;
					}
				}

				if (newPage == currentPage)
				{	// Page already active
					if (!(newPage is IPage))
						return;
					((IPage)newPage).PageReactivated();
				}
				else
				{
					if (currentPage != null)
					{	// Hide current page
						currentPage.Visible = false;
						if (currentPage is IPage)
							((IPage)currentPage).PageActive = false;
					}

					newPage.Visible = true;
					m_prefences.Layout.SelectedPageKey = pageName;
					foreach (KeyValuePair<string, ToolStripMenuItem> stripMenu in m_StripMenus)
						stripMenu.Value.Checked = stripMenu.Key.Equals(pageName);

					if (newPage is IPage)
						((IPage)newPage).PageActive = true;
				}
			}
			else if (pageName == "Exit")
				Close();
		}
		#endregion

		#region OnLoad / Disposed

		protected override void OnLoad(EventArgs p0)
		{
			if (m_prefences.General.ConnectOnStartup)
				m_setupPage.ConnectToScanTool();
			if (m_prefences.General.ShowWarningMessage)
			{
				using (PopupDialogForm popup = new PopupDialogForm(false))
				{
					DisclaimerDisplay disclaimerDisplay = new DisclaimerDisplay();
					disclaimerDisplay.WarningMessage = OCTech.OBD2.Applications.Properties.Resources.DisclaimerWarningMessage;
					disclaimerDisplay.ShowDisplayNextTimeCheckBox = true;
					popup.SetUserControl(disclaimerDisplay);
					popup.ShowDialog();
					m_prefences.General.ShowWarningMessage = disclaimerDisplay.DisplayNextTime;
				}
			}

			base.OnLoad(p0);

			if (m_prefences.Layout.UseWindowSettings)
			{
				this.SuspendLayout();
				this.Size = m_prefences.Layout.WindowSize;
				this.Location = m_prefences.Layout.WindowLocation;
				if (m_prefences.Layout.WindowState == FormWindowState.Minimized)
					this.WindowState = FormWindowState.Normal;
				else
					this.WindowState = m_prefences.Layout.WindowState;
				this.ResumeLayout();
			}
			else
			{
				m_prefences.Layout.WindowSize = this.Size;
				m_prefences.Layout.WindowLocation = this.Location;
			}
			this.Visible = true;
			m_loading = false;
		}

		private void mainForm_Disposed(object sender, EventArgs e)
		{
			m_isDisposed = true;
		}
		#endregion

		#region ForceClose / OnClosing

		public static void ForceClose()
		{
			((MainForm)m_mainForm).m_ForceClose = true;
			m_mainForm.Close();
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			bool disableClose = false;
			if (m_prefences.General.PromptOnExit && !m_ForceClose)
			{
				using (PopupDialogForm popup = new PopupDialogForm(false))
				{
					ConfirmExitDisplay confirmExitDisplay = new ConfirmExitDisplay();
					popup.SetUserControl(confirmExitDisplay);
					if (popup.ShowDialog(this) == DialogResult.No)
						disableClose = true;
					m_prefences.General.PromptOnExit = confirmExitDisplay.PromptOnExit;
				}
			}

			if (disableClose)
			{
				e.Cancel = true;
				customListView.SelectItem(m_prefences.Layout.SelectedPageKey);
			}
			else
			{
				base.OnClosing(e);

				m_pidMonitor.PollingPIDs = false;
				m_pidMonitor.Pause();
				m_logs.NotifyClosing();
				m_prefences.Layout.WindowState = WindowState;
				m_prefences.Layout.UseWindowSettings = true;
				m_prefences.Save(getPreferencesPath);
				saveDataDashboard();
				m_pidMonitor.SaveSettings(MainForm.ApplicationDataDirectory);
				pluginUnInitialize();

				try
				{
					OnApplicationClosing(EventArgs.Empty);
				}
				catch { }

				Visible = false;
				try
				{
					m_scanTool.Disconnect();
				}
				catch { }
			}
		}
		protected void OnApplicationClosing(EventArgs e)
		{
			if (ApplicationClosing != null)
				ApplicationClosing(this, e);
		}

		#endregion

		#region OnSizeChanged / OnLocationChanged
		protected override void OnSizeChanged(EventArgs e)
		{
			if (!m_loading && this.WindowState == FormWindowState.Normal && m_prefences != null)
				m_prefences.Layout.WindowSize = this.Size;
			base.OnSizeChanged(e);
		}
		protected override void OnLocationChanged(EventArgs e)
		{
			if (!m_loading && this.WindowState == FormWindowState.Normal && m_prefences != null)
				m_prefences.Layout.WindowLocation = this.Location;
			base.OnLocationChanged(e);
		}
		#endregion

		#region WndProc
		protected override void WndProc(ref Message message)
		{
			try
			{
				base.WndProc(ref message);
				m_messageDispatcher.NewMessageArrived(message);
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region m_pidMonitor_NewPIDResponseArrived
		private void m_pidMonitor_NewPIDResponseArrived(object sender, PIDResponseEventArgs e)
		{
			try
			{
				if (!m_pidMonitor.Playback.PlaybackActive)
					return;
				invokeCall((MethodInvoker)(() =>
				{
					double playbackTimeRemaining = m_pidMonitor.Playback.PlaybackTimeRemaining;
					if (playbackTimeRemaining > 60.0)
					{
						TimeSpan timeSpan = new TimeSpan(0, 0, (int)playbackTimeRemaining);
						if (timeSpan.Hours > 0)
							m_Port_ECU_Status.PlaybackTimeText = string.Format("{0} h {1} m {2} s", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
						else
							m_Port_ECU_Status.PlaybackTimeText = string.Format("{0} min {1} sec", timeSpan.Minutes, timeSpan.Seconds);
					}
					else
						m_Port_ECU_Status.PlaybackTimeText = string.Format("{0} sec", Math.Round(playbackTimeRemaining));
				}));
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region utilities_ErrorOccurred / controls_ErrorOccurred
		private void utilities_ErrorOccurred(object sender, OCTech.Utilities.ExceptionEventArgs ex)
		{
			displayError(ex.Exception);
		}
		private void controls_ErrorOccurred(object sender, OCTech.Controls.ExceptionEventArgs ex)
		{
			displayError(ex.Exception);
		}
		#endregion

		#region m_pidMonitor_NewPIDTimingAvailable
		private void m_pidMonitor_NewPIDTimingAvailable(object p0, PIDMonitorTimingEventArgs p1)
		{
			try
			{
				if (!m_pidMonitor.PollingPIDs || !m_scanTool.Connected)
					f00015d.Clear();
				IIRFilter iirFilter = f00015d;
				TimeSpan timeSpan = p1.TimeSpan;

				double p1_1 = (double)timeSpan.Milliseconds;
				double p0_1 = iirFilter.AddSample(p1_1);
				if (!f000168.ShouldRefresh)
					return;
				if (m_prefences.General.PIDTimingMode == PIDTimingMode.UpdateTime)
					MonitorRoundTripTime = (int)Math.Round(p0_1);
				else
					MonitorPIDsPerSecond = Math.Round(p1.AveragePIDsPerSecond, 1);
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region m_pidMonitor_RecordedPlaybackChanged
		private void m_pidMonitor_RecordedPlaybackChanged(object p0, RecordedDataPlayBackEventArgs p1)
		{
			invokeCall((MethodInvoker)(() =>
			{
				try
				{
					if (p1.PlaybackRunning)
						CurrentOperationStatus = OCTech.OBD2.Applications.Properties.Resources.PlayingBackDataText;
					else
					{
						CurrentOperationStatus = OCTech.OBD2.Applications.Properties.Resources.SystemReadyText;
						MonitorRoundTripTime = 0;
					}
					m_Port_ECU_Status.PlaybackTimeVisible = p1.PlaybackRunning;
					setConnectButtonState();
				}
				catch (Exception ex)
				{
					displayError(ex);
				}
			}));
		}
		#endregion

		#region m_pidMonitor_RecordedPlaybackChanged
		private void m_setupPage_ColorModeChanged(object p0, ColorModeChangedEventArgs p1)
		{
			try
			{
				setColorTheme(Themer.GetColorTheme(p1.ColorMode));
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region m_scanTool_CommunicationErrorOccurred
		private void m_scanTool_CommunicationErrorOccurred(object p0, MessageEventArgs p1)
		{
			try
			{
				m_diagnostic.WriteToLog(p1.Message);
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region m_scanTool_SelectECU
		private void m_scanTool_SelectECU(object p0, SelectedECUEventArgs p1)
		{
			try
			{
				if (m_prefences.Communications.PromptForECU
					|| string.IsNullOrEmpty(m_prefences.Communications.ActiveECU)
					|| !p1.SupportedPIDs.ContainsKey(m_prefences.Communications.ActiveECU)
					)
				{
					ThreadHelper.SyncExecuteInvoke(this, (MethodInvoker)(() =>
					{
						try
						{
							PopupDialogForm form = new PopupDialogForm(false);
							try
							{
								ECUSelector selector = new ECUSelector(m_prefences, m_scanTool, p1);
								form.SetUserControl(selector);
								form.ShowDialog(this);
								m_prefences.Communications.ActiveECU = p1.ActiveECU;
								selector.Cleanup();
							}
							finally
							{
								if (form != null)
								{
									form.Dispose();
								}
							}
						}
						catch (Exception ex)
						{
							displayError(ex);
						}
					}));
				}
			}
			catch (Exception exception)
			{
				displayError(exception);
			}
		}
		#endregion

		#region setConnectButtonState
		private void setConnectButtonState()
		{
			switch (m_scanTool.ConnectionStatus)
			{
				case ConnectionStatus.NotConnected:
					connectToolStripMenuItem.Enabled = !(m_pidMonitor.Playback.PlaybackActive);
					disconnectToolStripMenuItem.Enabled = false;
					break;
				case ConnectionStatus.Connecting:
				case ConnectionStatus.ConnectedToInterface:
					connectToolStripMenuItem.Enabled = false;
					disconnectToolStripMenuItem.Enabled = false;
					break;
				case ConnectionStatus.ConnectedToECU:
					connectToolStripMenuItem.Enabled = false;
					disconnectToolStripMenuItem.Enabled = true;
					break;
			}

			pidInspectorToolStripMenuItem.Enabled = (m_scanTool.Connected || m_pidMonitor.Playback.PlaybackActive);
		}
		#endregion

		#region m_scanTool_ConnectionStatusChanged
		private void m_scanTool_ConnectionStatusChanged(object p0, ConnectionStatusChangedEventArgs p1)
		{
			try
			{
				if (p1.Connected || p1.Connecting)
					return;
				m_diagnostic.WriteToLog("Disconnected");
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region m_scanTool_ConnectionChanged
		private void m_scanTool_ConnectionChanged(object p0, EventArgs p1)
		{
			try
			{
				if (m_scanTool.Connected)
					m_reconnectCount = 0;
				ThreadHelper.SyncExecute(this, (MethodInvoker)(() =>
				{
					try
					{
						setConnectButtonState();
						powerSaveSetupToolStripMenuItem.Enabled = m_scanTool.Connected;
					}
					catch (Exception ex)
					{
						displayError(ex);
					}
				}));
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region exitToolStripMenuItem_Click
		private void exitToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				Close();
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region viewToolStripMenuItem_DropDownOpening
		private void viewToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
		{
			try
			{
				showStatusBarToolStripMenuItem.Checked = statusPortECU;
				showListViewToolStripMenuItem.Checked = showListViewPanel;
				fullScreenToolStripMenuItem.Checked = fullScreen;
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region showStatusBarToolStripMenuItem_Click
		private void showStatusBarToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				ToggleWindowMode(WindowMode.StatusBar);
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region showListViewToolStripMenuItem_Click
		private void showListViewToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				ToggleWindowMode(WindowMode.ListViewPanel);
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region fullScreenToolStripMenuItem_Click
		private void fullScreenToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				ToggleWindowMode(WindowMode.FullScreen);
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region setupToolStripMenuItem_Click
		private void setupToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				switchPageByName("Setup");
				customListView.SelectItem("Setup");
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region diagnosticsToolStripMenuItem_Click
		private void diagnosticsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				switchPageByName("Diagnostics");
				customListView.SelectItem("Diagnostics");
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region monitorsToolStripMenuItem_Click
		private void monitorsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				switchPageByName("Monitors");
				customListView.SelectItem("Monitors");
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region dashboardToolStripMenuItem_Click
		private void dashboardToolStripMenuItem_Click(object p0, EventArgs p1)
		{
			try
			{
				switchPageByName("Dashboard");
				customListView.SelectItem("Dashboard");
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region logsToolStripMenuItem_Click
		private void logsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				switchPageByName("Logs");
				customListView.SelectItem("Logs");
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region vehicleManagerToolStripMenuItem_Click
		private void vehicleManagerToolStripMenuItem_Click(object p0, EventArgs p1)
		{
			try
			{
				ClearError();
				vehicleManagement.DisplayVehicleManager();
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region sensorCalibrationToolStripMenuItem_Click
		private void sensorCalibrationToolStripMenuItem_Click(object p0, EventArgs p1)
		{
			try
			{
				ClearError();
				using (PopupDialogForm popupDialogForm = new PopupDialogForm(DialogButtons.OK))
				{
					popupDialogForm.SetUserControl(sensorCalibration);
					popupDialogForm.ShowDialog(this);
					popupDialogForm.RemoveUserControl();
				}
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region userDefinedPIDsToolStripMenuItem_Click
		private void userDefinedPIDsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				ClearError();
				using (PopupDialogForm popupDialogForm = new PopupDialogForm(DialogButtons.OK))
				{
					UserDefinedPIDViewer definedPidViewer = new UserDefinedPIDViewer(m_context);
					popupDialogForm.SetUserControl(definedPidViewer);
					popupDialogForm.ShowDialog(this);
				}
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region pluginManagerToolStripMenuItem_Click
		private void pluginManagerToolStripMenuItem_Click(object p0, EventArgs p1)
		{
			try
			{
				ClearError();
				using (PopupDialogForm popupDialogForm = new PopupDialogForm())
				{
					PluginPreferenceEditor preferenceEditor = new PluginPreferenceEditor();
					preferenceEditor.m003fdd(m_context, m_PluginsInfo);
					popupDialogForm.SetUserControl(preferenceEditor);
					if (popupDialogForm.ShowDialog() != DialogResult.OK)
						return;
					preferenceEditor.ApplyChanges();
				}
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region preferencesToolStripMenuItem_Click
		private void preferencesToolStripMenuItem_Click(object p0, EventArgs p1)
		{
			try
			{
				ClearError();
				using (PopupDialogForm popupDialogForm = new PopupDialogForm(DialogButtons.OKCancel))
				{
					PreferenceEditor preferenceEditor = new PreferenceEditor(m_context);
					popupDialogForm.SetUserControl(preferenceEditor);
					if (popupDialogForm.ShowDialog(this) != DialogResult.OK)
						return;
					preferenceEditor.ApplyPreferences();
				}
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region powerSaveSetupToolStripMenuItem_Click
		private void powerSaveSetupToolStripMenuItem_Click(object p0, EventArgs p1)
		{
			try
			{
				ClearError();
				using (PopupDialogForm popupDialogForm = new PopupDialogForm())
				{
					PowerSaveSetup powerSaveSetup = new PowerSaveSetup();
					popupDialogForm.SetUserControl(powerSaveSetup);
					powerSaveSetup.Initialize(m_context);
					if (popupDialogForm.ShowDialog() != DialogResult.OK)
						return;
					powerSaveSetup.ApplyPowerSaveSettings();
				}
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region pidInspectorToolStripMenuItem_Click
		private void pidInspectorToolStripMenuItem_Click(object p0, EventArgs p1)
		{
			try
			{
				using (PopupDialogForm popupDialogForm = new PopupDialogForm(DialogButtons.OK))
				{
					PIDInspector pidInspector = new PIDInspector();
					pidInspector.m00267f(m_context);
					popupDialogForm.SetUserControl(pidInspector);
					popupDialogForm.ShowDialog(this);
				}
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region connectToolStripMenuItem_Click
		private void connectToolStripMenuItem_Click(object p0, EventArgs p1)
		{
			try
			{
				ClearError();
				m_setupPage.ConnectToScanTool();
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region disconnectToolStripMenuItem_Click
		private void disconnectToolStripMenuItem_Click(object p0, EventArgs p1)
		{
			try
			{
				ClearError();
				m_setupPage.DisconnectFromScanTool();
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region toolStripMenuItem_Click
		private void toolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				string tag = (string)((ToolStripMenuItem)sender).Tag;
				switchPageByName(tag);
				customListView.SelectItem(tag);
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region customListView_ItemSelected
		private void customListView_ItemSelected(object sender, CustomListViewItemSelectedEventArgs e)
		{
			try
			{
				clearError();
				pagePanel.SuspendLayout();
				switchPageByName(e.Key);
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
			finally
			{
				pagePanel.ResumeLayout();
			}
		}
		#endregion

		#region m_timer_Tick
		private void m_timer_Tick(object sender, EventArgs e)
		{
			try
			{
				CurrentOperationStatus =
					(!m_scanTool.RecordData || !m_scanTool.Connected)
					? (m_pidMonitor == null || !m_pidMonitor.Playback.PlaybackActive
						? OCTech.OBD2.Applications.Properties.Resources.SystemReadyText
						: OCTech.OBD2.Applications.Properties.Resources.PlayingBackDataText)
					: OCTech.OBD2.Applications.Properties.Resources.RecordingDataText;

				switchDayNight(DateTime.Now);

				if (sensorCalibration != null)
					m_Port_ECU_Status.CalActive = m_scanTool.CalibrationActive;
			}
			catch (Exception ex)
			{
				displayError(ex);
			}
		}
		#endregion

		#region Color Theme support
		public void SetControlColorTheme(Control control)
		{
			Themer.SetControlColorTheme(control, ActiveTheme);
		}

		public void RecurseControlColorTheme(Control control)
		{
			Themer.RecurseControlColorTheme(control, ActiveTheme);
		}
		protected void OnColorThemeChanged(ColorThemeChangedEventArgs p0)
		{
			if (ColorThemeChanged != null)
				ColorThemeChanged(this, (EventArgs)p0);
		}
		private void setColorTheme(ColorTheme theme)
		{
			customListView.SetColorMode(theme);
			m_dashboard.SetColorMode(theme);
			m_logs.SetColorMode(theme);
			m_setupPage.SetColorMode(theme);
			m_diagnostic.SetColorMode(theme);
			m_monitors.SetColorMode(theme);

			OnColorThemeChanged(new ColorThemeChangedEventArgs(theme));
		}
		#endregion

		#region Windows form
		protected override void Dispose(bool disposing)
		{
			if (disposing && m_container != null)
				m_container.Dispose();
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			m_container = (IContainer)new Container();
			Infragistics.Win.Appearance appearance = new Infragistics.Win.Appearance();
			UltraStatusPanel ultraStatusPanel1 = new UltraStatusPanel();
			UltraStatusPanel ultraStatusPanel2 = new UltraStatusPanel();
			UltraStatusPanel ultraStatusPanel3 = new UltraStatusPanel();
			UltraStatusPanel ultraStatusPanel4 = new UltraStatusPanel();
			UltraStatusPanel ultraStatusPanel5 = new UltraStatusPanel();
			UltraStatusPanel ultraStatusPanel6 = new UltraStatusPanel();
			UltraStatusPanel ultraStatusPanel7 = new UltraStatusPanel();
			UltraStatusPanel ultraStatusPanel8 = new UltraStatusPanel();
			this.tableLayoutPanel = new TableLayoutPanel();
			this.pagePanel = new Panel();
			this.customListView = new CustomListView();
			m_timer = new System.Windows.Forms.Timer(m_container);
			menuStrip = new MenuStrip();
			fileToolStripMenuItem = new ToolStripMenuItem();
			exitToolStripMenuItem = new ToolStripMenuItem();
			viewToolStripMenuItem = new ToolStripMenuItem();
			showStatusBarToolStripMenuItem = new ToolStripMenuItem();
			showListViewToolStripMenuItem = new ToolStripMenuItem();
			toolStripSeparator1 = new ToolStripSeparator();
			fullScreenToolStripMenuItem = new ToolStripMenuItem();
			windowToolStripMenuItem = new ToolStripMenuItem();
			setupToolStripMenuItem = new ToolStripMenuItem();
			diagnosticsToolStripMenuItem = new ToolStripMenuItem();
			monitorsToolStripMenuItem = new ToolStripMenuItem();
			dashboardToolStripMenuItem = new ToolStripMenuItem();
			logsToolStripMenuItem = new ToolStripMenuItem();
			connectionToolStripMenuItem = new ToolStripMenuItem();
			connectToolStripMenuItem = new ToolStripMenuItem();
			disconnectToolStripMenuItem = new ToolStripMenuItem();
			toolsToolStripMenuItem = new ToolStripMenuItem();
			vehicleManagerToolStripMenuItem = new ToolStripMenuItem();
			sensorCalibrationToolStripMenuItem = new ToolStripMenuItem();
			userDefinedPIDsToolStripMenuItem = new ToolStripMenuItem();
			pluginManagerToolStripMenuItem = new ToolStripMenuItem();
			toolStripSeparatorToolsMenu = new ToolStripSeparator();
			pidInspectorToolStripMenuItem = new ToolStripMenuItem();
			powerSaveSetupToolStripMenuItem = new ToolStripMenuItem();
			toolStripSeparatorToolsMenu2 = new ToolStripSeparator();
			preferencesToolStripMenuItem = new ToolStripMenuItem();
			toolStripContainer = new ToolStripContainer();
			m_statusPortECU = new UltraStatusBar();
			tableLayoutPanel.SuspendLayout();
			menuStrip.SuspendLayout();
			toolStripContainer.ContentPanel.SuspendLayout();
			toolStripContainer.TopToolStripPanel.SuspendLayout();

			this.toolStripContainer.SuspendLayout();
			((ISupportInitialize)this.m_statusPortECU).BeginInit();
			this.SuspendLayout();

			this.tableLayoutPanel.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
			this.tableLayoutPanel.ColumnCount = 2;
			this.tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84f));
			tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
			tableLayoutPanel.Controls.Add(pagePanel, 1, 0);
			tableLayoutPanel.Controls.Add(customListView, 0, 0);
			tableLayoutPanel.Dock = DockStyle.Fill;
			tableLayoutPanel.Location = new Point(0, 0);
			tableLayoutPanel.Margin = new Padding(2);
			tableLayoutPanel.Name = "tableLayoutPanel";
			tableLayoutPanel.RowCount = 1;
			tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
			tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 434f));
			tableLayoutPanel.Size = new Size(763, 435);
			tableLayoutPanel.TabIndex = 1;

			pagePanel.Dock = DockStyle.Fill;
			pagePanel.Location = new Point(86, 1);
			pagePanel.Margin = new Padding(0);
			pagePanel.Name = "pagePanel";
			pagePanel.Size = new Size(676, 433);
			pagePanel.TabIndex = 2;

			customListView.Dock = DockStyle.Fill;
			customListView.ItemSpacing = ListViewItemSpacing.f00009d;
			customListView.Location = new Point(3, 3);
			customListView.Margin = new Padding(2);
			customListView.Name = "customListView";
			customListView.Size = new Size(80, 429);
			customListView.TabIndex = 3;

			m_timer.Enabled = true;
			m_timer.Interval = 1000;
			m_timer.Tick += new EventHandler(m_timer_Tick);

			menuStrip.Dock = DockStyle.None;
			menuStrip.Items.AddRange(new ToolStripItem[]
			{
				fileToolStripMenuItem,
				viewToolStripMenuItem,
				windowToolStripMenuItem,
				connectionToolStripMenuItem,
				toolsToolStripMenuItem
			});
			menuStrip.Location = new Point(0, 0);
			menuStrip.Name = "menuStrip";
			menuStrip.Size = new Size(763, 24);
			menuStrip.TabIndex = 0;
			fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[1]
			{
				exitToolStripMenuItem
			});
			fileToolStripMenuItem.Name = "fileToolStripMenuItem";
			fileToolStripMenuItem.Size = new Size(37, 20);
			fileToolStripMenuItem.Text = "&File";
			exitToolStripMenuItem.Image = OCTech.OBD2.Applications.Properties.Resources.p000031;
			exitToolStripMenuItem.Name = "exitToolStripMenuItem";
			exitToolStripMenuItem.Size = new Size(92, 22);
			exitToolStripMenuItem.Text = "E&xit";
			exitToolStripMenuItem.Click += new EventHandler(exitToolStripMenuItem_Click);

			viewToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]
			{
				showStatusBarToolStripMenuItem,
				showListViewToolStripMenuItem,
				toolStripSeparator1,
				fullScreenToolStripMenuItem
			});
			viewToolStripMenuItem.Name = "viewToolStripMenuItem";
			viewToolStripMenuItem.Size = new Size(44, 20);
			viewToolStripMenuItem.Text = "&View";
			viewToolStripMenuItem.DropDownOpening += new EventHandler(viewToolStripMenuItem_DropDownOpening);

			showStatusBarToolStripMenuItem.Name = "showStatusBarToolStripMenuItem";
			showStatusBarToolStripMenuItem.Size = new Size(131, 22);
			showStatusBarToolStripMenuItem.Text = "&Status Bar";
			showStatusBarToolStripMenuItem.Click += new EventHandler(showStatusBarToolStripMenuItem_Click);

			showListViewToolStripMenuItem.Name = "showListViewToolStripMenuItem";
			showListViewToolStripMenuItem.Size = new Size(131, 22);
			showListViewToolStripMenuItem.Text = "&List View";
			showListViewToolStripMenuItem.Click += new EventHandler(showListViewToolStripMenuItem_Click);

			toolStripSeparator1.Name = "toolStripSeparator1";
			toolStripSeparator1.Size = new Size(128, 6);

			fullScreenToolStripMenuItem.Name = "fullScreenToolStripMenuItem";
			fullScreenToolStripMenuItem.Size = new Size(131, 22);
			fullScreenToolStripMenuItem.Text = "&Full Screen";
			fullScreenToolStripMenuItem.Click += new EventHandler(fullScreenToolStripMenuItem_Click);

			windowToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[5]
			{
				setupToolStripMenuItem,
				diagnosticsToolStripMenuItem,
				monitorsToolStripMenuItem,
				dashboardToolStripMenuItem,
				logsToolStripMenuItem
			});
			windowToolStripMenuItem.Name = "windowToolStripMenuItem";
			windowToolStripMenuItem.Size = new Size(63, 20);
			windowToolStripMenuItem.Text = "&Window";

			setupToolStripMenuItem.Image = (Image)OCTech.OBD2.Applications.Properties.Resources.p000074;
			setupToolStripMenuItem.Name = "setupToolStripMenuItem";
			setupToolStripMenuItem.Size = new Size(135, 22);
			setupToolStripMenuItem.Text = "&Setup";
			setupToolStripMenuItem.Click += new EventHandler(setupToolStripMenuItem_Click);

			diagnosticsToolStripMenuItem.Image = (Image)OCTech.OBD2.Applications.Properties.Resources.p000025;
			diagnosticsToolStripMenuItem.Name = "diagnosticsToolStripMenuItem";
			diagnosticsToolStripMenuItem.Size = new Size(135, 22);
			diagnosticsToolStripMenuItem.Text = "&Diagnostics";
			diagnosticsToolStripMenuItem.Click += new EventHandler(diagnosticsToolStripMenuItem_Click);

			monitorsToolStripMenuItem.Image = (Image)OCTech.OBD2.Applications.Properties.Resources.p000044;
			monitorsToolStripMenuItem.Name = "monitorsToolStripMenuItem";
			monitorsToolStripMenuItem.Size = new Size(135, 22);
			monitorsToolStripMenuItem.Text = "&Monitors";
			monitorsToolStripMenuItem.Click += new EventHandler(monitorsToolStripMenuItem_Click);

			dashboardToolStripMenuItem.Image = (Image)OCTech.OBD2.Applications.Properties.Resources.p000014;
			dashboardToolStripMenuItem.Name = "dashboardToolStripMenuItem";
			dashboardToolStripMenuItem.Size = new Size(135, 22);
			dashboardToolStripMenuItem.Text = "&D&ashboard";
			dashboardToolStripMenuItem.Click += new EventHandler(dashboardToolStripMenuItem_Click);

			logsToolStripMenuItem.Image = (Image)OCTech.OBD2.Applications.Properties.Resources.p000042;
			logsToolStripMenuItem.Name = "logsToolStripMenuItem";
			logsToolStripMenuItem.Size = new Size(135, 22);
			logsToolStripMenuItem.Text = "&Logs";
			logsToolStripMenuItem.Click += new EventHandler(logsToolStripMenuItem_Click);

			connectionToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[2]
			{
				connectToolStripMenuItem,
				disconnectToolStripMenuItem
			});
			connectionToolStripMenuItem.Name = "connectionToolStripMenuItem";
			connectionToolStripMenuItem.Size = new Size(81, 20);
			connectionToolStripMenuItem.Text = "&Connection";

			connectToolStripMenuItem.Image = (Image)OCTech.OBD2.Applications.Properties.Resources.p00000e;
			connectToolStripMenuItem.Name = "connectToolStripMenuItem";
			connectToolStripMenuItem.Size = new Size(133, 22);
			connectToolStripMenuItem.Text = "&Connect";
			connectToolStripMenuItem.Click += new EventHandler(connectToolStripMenuItem_Click);

			disconnectToolStripMenuItem.Image = (Image)OCTech.OBD2.Applications.Properties.Resources.p000027;
			disconnectToolStripMenuItem.Name = "disconnectToolStripMenuItem";
			disconnectToolStripMenuItem.Size = new Size(133, 22);
			disconnectToolStripMenuItem.Text = "&Disconnect";
			disconnectToolStripMenuItem.Click += new EventHandler(disconnectToolStripMenuItem_Click);

			toolsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[9]
			{
				vehicleManagerToolStripMenuItem,
				sensorCalibrationToolStripMenuItem,
				userDefinedPIDsToolStripMenuItem,
				pluginManagerToolStripMenuItem,
				toolStripSeparatorToolsMenu,
				pidInspectorToolStripMenuItem,
				powerSaveSetupToolStripMenuItem,
				toolStripSeparatorToolsMenu2,
				preferencesToolStripMenuItem
			});
			toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
			toolsToolStripMenuItem.Size = new Size(48, 20);
			toolsToolStripMenuItem.Text = "&Tools";

			vehicleManagerToolStripMenuItem.Name = "vehicleManagerToolStripMenuItem";
			vehicleManagerToolStripMenuItem.Size = new Size(179, 22);
			vehicleManagerToolStripMenuItem.Text = "Vehicle &Manager";
			vehicleManagerToolStripMenuItem.Click += new EventHandler(vehicleManagerToolStripMenuItem_Click);

			sensorCalibrationToolStripMenuItem.Name = "sensorCalibrationToolStripMenuItem";
			sensorCalibrationToolStripMenuItem.Size = new Size(179, 22);
			sensorCalibrationToolStripMenuItem.Text = "Sensor &Calibration";
			sensorCalibrationToolStripMenuItem.Click += new EventHandler(sensorCalibrationToolStripMenuItem_Click);

			userDefinedPIDsToolStripMenuItem.Name = "userDefinedPIDsToolStripMenuItem";
			userDefinedPIDsToolStripMenuItem.Size = new Size(179, 22);
			userDefinedPIDsToolStripMenuItem.Text = "&User-Defined PIDs";
			userDefinedPIDsToolStripMenuItem.Click += new EventHandler(userDefinedPIDsToolStripMenuItem_Click);

			pluginManagerToolStripMenuItem.Image = OCTech.OBD2.Applications.Properties.Resources.p00005a;
			pluginManagerToolStripMenuItem.Name = "pluginManagerToolStripMenuItem";
			pluginManagerToolStripMenuItem.Size = new Size(179, 22);
			pluginManagerToolStripMenuItem.Text = "Plugin Manager";
			pluginManagerToolStripMenuItem.Click += new EventHandler(pluginManagerToolStripMenuItem_Click);

			toolStripSeparatorToolsMenu.Name = "toolStripSeparatorToolsMenu";
			toolStripSeparatorToolsMenu.Size = new Size(176, 6);

			pidInspectorToolStripMenuItem.Enabled = false;
			pidInspectorToolStripMenuItem.Image = OCTech.OBD2.Applications.Properties.Resources.p000057;
			pidInspectorToolStripMenuItem.Name = "pidInspectorToolStripMenuItem";
			pidInspectorToolStripMenuItem.Size = new Size(179, 22);
			pidInspectorToolStripMenuItem.Text = "PID Inspector";
			pidInspectorToolStripMenuItem.Click += new EventHandler(pidInspectorToolStripMenuItem_Click);

			powerSaveSetupToolStripMenuItem.Enabled = false;
			powerSaveSetupToolStripMenuItem.Image = OCTech.OBD2.Applications.Properties.Resources.PowerSave16x16;
			powerSaveSetupToolStripMenuItem.Name = "powerSaveSetupToolStripMenuItem";
			powerSaveSetupToolStripMenuItem.Size = new Size(179, 22);
			powerSaveSetupToolStripMenuItem.Text = "Power Save Settings";
			powerSaveSetupToolStripMenuItem.Click += new EventHandler(powerSaveSetupToolStripMenuItem_Click);

			toolStripSeparatorToolsMenu2.Name = "toolStripSeparatorToolsMenu2";
			toolStripSeparatorToolsMenu2.Size = new Size(176, 6);

			preferencesToolStripMenuItem.Image = OCTech.OBD2.Applications.Properties.Resources.p00005d;
			preferencesToolStripMenuItem.Name = "preferencesToolStripMenuItem";
			preferencesToolStripMenuItem.Size = new Size(179, 22);
			preferencesToolStripMenuItem.Text = "&Preferences";
			preferencesToolStripMenuItem.Click += new EventHandler(preferencesToolStripMenuItem_Click);

			toolStripContainer.ContentPanel.Controls.Add(tableLayoutPanel);
			toolStripContainer.ContentPanel.Padding = new Padding(0, 0, 0, 23);
			toolStripContainer.ContentPanel.Size = new Size(763, 458);
			toolStripContainer.Dock = DockStyle.Fill;
			toolStripContainer.Location = new Point(0, 0);
			toolStripContainer.Name = "toolStripContainer";
			toolStripContainer.Size = new Size(763, 482);
			toolStripContainer.TabIndex = 0;
			toolStripContainer.TopToolStripPanel.Controls.Add(menuStrip);

			m_statusPortECU.Location = new Point(0, 459);
			m_statusPortECU.Name = "ultraStatusBar";
			appearance.TextHAlignAsString = "Center";
			m_statusPortECU.PanelAppearance = appearance;

			ultraStatusPanel1.Key = "InterfacePanel";
			ultraStatusPanel1.SizingMode = PanelSizingMode.Automatic;
			ultraStatusPanel1.Style = PanelStyle.ControlContainer;
			ultraStatusPanel1.Width = 90;

			ultraStatusPanel2.Key = "ECUPanel";
			ultraStatusPanel2.SizingMode = PanelSizingMode.Automatic;
			ultraStatusPanel2.Style = PanelStyle.ControlContainer;
			ultraStatusPanel2.Width = 75;

			ultraStatusPanel3.Key = "CurrentOperationPanel";
			ultraStatusPanel3.SizingMode = PanelSizingMode.Automatic;
			ultraStatusPanel3.Visible = false;
			ultraStatusPanel3.Width = 90;

			ultraStatusPanel4.Key = "ErrorPanel";
			ultraStatusPanel4.SizingMode = PanelSizingMode.Spring;

			ultraStatusPanel5.Key = "CalPanel";
			ultraStatusPanel5.SizingMode = PanelSizingMode.Automatic;
			ultraStatusPanel5.Visible = false;

			ultraStatusPanel6.Key = "PlaybackTimePanel";
			ultraStatusPanel6.SizingMode = PanelSizingMode.Automatic;
			ultraStatusPanel6.Visible = false;

			ultraStatusPanel7.Key = "PIDTimingPanel";
			ultraStatusPanel7.Width = 85;

			ultraStatusPanel8.Key = "ClockPanel";
			ultraStatusPanel8.SizingMode = PanelSizingMode.Automatic;
			ultraStatusPanel8.Style = PanelStyle.Time;

			m_statusPortECU.Panels.AddRange(new UltraStatusPanel[]
			{
				ultraStatusPanel1,
				ultraStatusPanel2,
				ultraStatusPanel3,
				ultraStatusPanel4,
				ultraStatusPanel5,
				ultraStatusPanel6,
				ultraStatusPanel7,
				ultraStatusPanel8
			});

			m_statusPortECU.Size = new Size(763, 23);
			m_statusPortECU.SizeGripVisible = DefaultableBoolean.False;
			m_statusPortECU.TabIndex = 1;
			m_statusPortECU.ViewStyle = ViewStyle.VisualStudio2005;
			this.AutoScaleDimensions = new SizeF(6f, 13f);
			AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new Size(763, 482);
			this.Controls.Add(m_statusPortECU);
			this.Controls.Add(toolStripContainer);
			this.MainMenuStrip = menuStrip;
			this.Margin = new Padding(2);
			this.Name= "MainForm";
			StartPosition = FormStartPosition.CenterScreen;
			this.WindowState = FormWindowState.Maximized;
			tableLayoutPanel.ResumeLayout(false);
			menuStrip.ResumeLayout(false);
			menuStrip.PerformLayout();
			toolStripContainer.ContentPanel.ResumeLayout(false);
			toolStripContainer.TopToolStripPanel.ResumeLayout(false);
			toolStripContainer.TopToolStripPanel.PerformLayout();
			toolStripContainer.ResumeLayout(false);
			toolStripContainer.PerformLayout();
			((ISupportInitialize)m_statusPortECU).EndInit();
			this.ResumeLayout(false);
		}
		#endregion

		#region AddPage
		public void AddPage(IPlugin plugin, string name, Image image, Control control)
		{
			if (control == null)
				return;

			string key = plugin.PluginName;
			customListView.Add(name, key, image);
			m_TabPages.Add(key, control);
			addPageToPanel(control);
			ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem(name, image, new EventHandler(toolStripMenuItem_Click));
			toolStripMenuItem.Image = image;
			toolStripMenuItem.Tag = key;
			windowToolStripMenuItem.DropDownItems.Add(toolStripMenuItem);
			m_StripMenus.Add(key, toolStripMenuItem);
		}

		private void addPageToPanel(Control control)
		{
			control.Dock = DockStyle.Fill;
			control.Visible = false;
			pagePanel.Controls.Add(control);
		}
		#endregion

		#region Load OCTech.Plugin.*.dll
		private void loadOCTechPlugins()
		{
			try
			{
				int numberOfItemsAdded = customListView.NumberOfItemsAdded;
				m_PluginsInfo = new ListPluginClassInfo(
									PluginClassInfo.LoadPlugins(Application.StartupPath, "OCTech.Plugin.*.dll")
									);
				foreach (PluginClassInfo plugin in m_PluginsInfo.Plugins)
					plugin.IPlugin.Initialize(this);

				if (customListView.NumberOfItemsAdded <= numberOfItemsAdded
				|| customListView.ItemSpacing <= ListViewItemSpacing.Medium
					)
					return;
				customListView.ItemSpacing = ListViewItemSpacing.Medium;
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}
		#endregion

		#region Plugin Initialize / UnInitialize
		private void pluginInitialize()
		{
			try
			{
				PIDPluginController.Initialize(m_context, PIDPluginCustom.LoadPlugins(getPIDPluginSearchPath));
				PIDPluginController.InitializePlugins(this);
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		private void pluginUnInitialize()
		{
			if (PIDPluginController.Plugins != null)
				foreach (PIDPluginCustom plugin in PIDPluginController.Plugins)
					try
					{
						plugin.IPIDPlugin.UnInitialize();
					}
					catch { }
		}
		#endregion

		#region UltraStatusBarEx (Bottom Left corner)
		private class UltraStatusBarEx
		{
			private ConnectStatusImage m_indicatorPort = new ConnectStatusImage();
			private ConnectStatusImage m_indicatorECU = new ConnectStatusImage();
			private UltraStatusBar m_ultraStatusBar;
			private ConnectionStatus m_connectStatus;
			private bool m_CalActive;

			public ConnectionStatus ConnectionStatus
			{
				set
				{
					if (m_connectStatus == value)
					{
						switch (value)
						{
							case OBD2.ConnectionStatus.NotConnected:
								m_indicatorPort.ConnectStatus = ConnectStatus.NotConnected;
								m_indicatorECU.ConnectStatus = ConnectStatus.NotConnected;
								break;
							case OBD2.ConnectionStatus.Connecting:
								m_indicatorPort.ConnectStatus = ConnectStatus.Connecting;
								m_indicatorECU.ConnectStatus = ConnectStatus.Connecting;
								break;
							case OBD2.ConnectionStatus.ConnectedToInterface:
								m_indicatorPort.ConnectStatus = ConnectStatus.Connected;
								if (m_connectStatus == ConnectionStatus.ConnectedToECU)
									m_indicatorECU.ConnectStatus = ConnectStatus.NotConnected;
								break;
							case OBD2.ConnectionStatus.ConnectedToECU:
								m_indicatorPort.ConnectStatus = ConnectStatus.Connected;
								m_indicatorECU.ConnectStatus = ConnectStatus.Connected;
								break;
						}
						m_connectStatus = value;
						m_indicatorECU.Refresh();
						m_indicatorPort.Refresh();
					}
				}
			}

			public string CurrentOperationText
			{
				get { return m_ultraStatusBar.Panels["CurrentOperationPanel"].Text; }
				set
				{
					m_ultraStatusBar.Panels["CurrentOperationPanel"].Text = value;
					if (string.IsNullOrEmpty(value))
						m_ultraStatusBar.Panels["CurrentOperationPanel"].Visible = false;
					else
						m_ultraStatusBar.Panels["CurrentOperationPanel"].Visible = true;
				}
			}

			public string ErrorText
			{
				get { return m_ultraStatusBar.Panels["ErrorPanel"].Text; }
				set { m_ultraStatusBar.Panels["ErrorPanel"].Text = value; }
			}

			public string TimingText
			{
				set { m_ultraStatusBar.Panels["PIDTimingPanel"].Text = value; }
			}

			public bool PlaybackTimeVisible
			{
				set { m_ultraStatusBar.Panels["PlaybackTimePanel"].Visible = value; }
			}

			public string PlaybackTimeText
			{
				set { m_ultraStatusBar.Panels["PlaybackTimePanel"].Text = value; }
			}

			public bool CalActive
			{
				set
				{
					if (m_CalActive == value)
						return;
					m_CalActive = value;
					if (value)
					{
						m_ultraStatusBar.Panels["CalPanel"].Text = "CAL";
						m_ultraStatusBar.Panels["CalPanel"].Visible = true;
					}
					else
					{
						m_ultraStatusBar.Panels["CalPanel"].Text = string.Empty;
						m_ultraStatusBar.Panels["CalPanel"].Visible = false;
					}
				}
			}

			public UltraStatusBarEx(UltraStatusBar ultraStatusBar)
			{
				m_ultraStatusBar = ultraStatusBar;
				m_ultraStatusBar.Panels["InterfacePanel"].Control = m_indicatorPort;
				m_ultraStatusBar.Panels["ECUPanel"].Control = m_indicatorECU;

				m_indicatorPort.DisplayText = OCTech.OBD2.Applications.Properties.Resources.InterfaceStatusBarText;
				m_indicatorECU.DisplayText = "ECU: ";
			}
			/*
			private static class c000068
			{
				internal const string f000015 = "InterfacePanel";
				internal const string f00018b = "ECUPanel";
				internal const string f00018c = "CurrentOperationPanel";
				internal const string f00018d = "ErrorPanel";
				internal const string f00018e = "CalPanel";
				internal const string f00018f = "PlaybackTimePanel";
				internal const string f000192 = "PIDTimingPanel";
			}
			*/
		}
		#endregion
	}
}
