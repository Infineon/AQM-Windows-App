using System.Threading.Tasks;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using System.Linq;
using Windows.UI.Core;
using BLE_Graph;
using SkiaSharp;
using Windows.UI.Xaml.Media;
using System.ComponentModel;
using LiveChartsCore.SkiaSharpView.Painting;

namespace LineGraph
{
	public class Viewmodel {
		private MainPage rootPage = MainPage.Current;

		private ObservableCollection<ObservablePoint> _emptySeries = new ObservableCollection<ObservablePoint>();

		private BluetoothLEDevice _bluetoothLeDevice = null; // selected device and connection object
		private GattDeviceServicesResult _bluetoothLeServicesResult = null; // services of connected device
		private List<GattDeviceService> _bluetoothLeServices;

		private BleModel _bleObject;


		public ObservableCollection<ConfigCharacteristic> ConfigCharacteristics { get; set; }
		public ObservableCollection<MeasurementCharacteristic> MeasurementCharacteristics { get; set; }//= new(); // all measurement characteristics found
		public MeasurementCharacteristic GraphVisibleMeasurementCharacteristic; // currently viewed measurement characteristic

		// Configurations
		private double _config_dataPointsTimeMultiplicator_Sec = 1; // Multiplicand to get the X/Time Values of each point right
		private double _config_SamplingTimeDivider = 1000;       // Divider to convert the SamplingTime to the wanted unit (e.g. ms to sec)

		public object Sync { get; }
		public ObservableCollection<ISeries> Series { get; set; }
		public List<Axis> XAxes { get; set; }
		public List<Axis> YAxes { get; set; }

		public BluetoothLEDevice BluetoothLeDevice { get => _bluetoothLeDevice; set => _bluetoothLeDevice = value; }
		public GattDeviceServicesResult BluetoothLeServicesResult { get => _bluetoothLeServicesResult; set => _bluetoothLeServicesResult = value; }
		public List<GattDeviceService> BluetoothLeServices { get => _bluetoothLeServices; set => _bluetoothLeServices = value; }



		public static readonly List<SolidColorBrush> TextColors_Brush = new List<SolidColorBrush>(){
			new SolidColorBrush(Windows.UI.ColorHelper.FromArgb(0xFF,0xc1,0x39,0x2b)), //Dark Shade of Tomato
			new SolidColorBrush(Windows.UI.ColorHelper.FromArgb(0xFF,0x2a,0x80,0xb9)), //Dark Summer Sky
			new SolidColorBrush(Windows.UI.ColorHelper.FromArgb(0xFF,0x27,0xae,0x61)), //Dark Medium Sea Green
			new SolidColorBrush(Windows.UI.ColorHelper.FromArgb(0xFF,0x8f,0x44,0xad)), //Dark Amethyst
			new SolidColorBrush(Windows.UI.ColorHelper.FromArgb(0xFF,0xd2,0x54,0x00)), //Dark Bright Orange
			new SolidColorBrush(Windows.UI.ColorHelper.FromArgb(0xFF,0x16,0xa0,0x86)), //Dark Light Sea Green
			new SolidColorBrush(Windows.UI.ColorHelper.FromArgb(0xFF,0xd1,0xaa,0x0d)), //Dark Shade of Yellow (modified)

			new SolidColorBrush(Windows.UI.ColorHelper.FromArgb(0xFF,0xe8,0x4c,0x3d)), //Shade of Tomato
			new SolidColorBrush(Windows.UI.ColorHelper.FromArgb(0xFF,0x35,0x98,0xdb)), //Summer Sky
			new SolidColorBrush(Windows.UI.ColorHelper.FromArgb(0xFF,0x2d,0xcc,0x77)), //Medium Sea Green
			new SolidColorBrush(Windows.UI.ColorHelper.FromArgb(0xFF,0x9b,0x58,0xb5)), //Amethyst
			new SolidColorBrush(Windows.UI.ColorHelper.FromArgb(0xFF,0xe7,0x7e,0x23)), //Bright Orange
			new SolidColorBrush(Windows.UI.ColorHelper.FromArgb(0xFF,0x1b,0xbc,0x9b)), //Light Sea Green
			new SolidColorBrush(Windows.UI.ColorHelper.FromArgb(0xFF,0xf1,0xc4,0x0f)), //Shade of Yellow

			new SolidColorBrush(Windows.UI.Colors.DarkRed),
			new SolidColorBrush(Windows.UI.Colors.DarkBlue),
			new SolidColorBrush(Windows.UI.Colors.DarkGreen),
			new SolidColorBrush(Windows.UI.Colors.DarkViolet),
			new SolidColorBrush(Windows.UI.Colors.DarkOrange),
			new SolidColorBrush(Windows.UI.Colors.Brown),
			new SolidColorBrush(Windows.UI.Colors.Black)

		};
		public int Colors_index = -1; // The index of the last used Data/Text Color -> incremented every time a measurement characteristic is added
		 
		public Viewmodel()
        {
			// Initialize config and measurement characteristics lists
			ConfigCharacteristics = new ObservableCollection<ConfigCharacteristic>();
			MeasurementCharacteristics = new ObservableCollection<MeasurementCharacteristic>();
			_bluetoothLeServices = new List<GattDeviceService>();

			_bleObject = new BleModel();

			// Initialize Series
			Series = new ObservableCollection<ISeries> {
				new LineSeries<ObservablePoint>
				{
					Values = _emptySeries,
					Fill = null
				}
			};

			// Initialize Axes objects (X Axis Values are set hard because they are not supposed to change)
			XAxes = new List<Axis>{
				new Axis{
					Name = "Time",
					Labeler = (value) => value + " s",
					MinStep = 0.5
				}
			};
			YAxes = new List<Axis>{
				new Axis{}
			};

			// Initialize Sync object (lock of line series list during write and read due to graph reset)
			Sync = new object();
		}

					


		public async Task<bool> LoadBleDevice(string SelectedBleDeviceId, Guid MeasurementServiceUuid, Guid ConfigServiceUuid, Guid Config_SamplingTimeCharacteristic, bool ForceReset) {
			bool resetError = true;
			bool connectionError = true;
			bool configLoadError = true; 
			bool measInitError = true;


			// Try to reset internal variables (Device, Characteristics, etc)
			if (ForceReset)
				resetError = await ResetBleDevice();
			else
				resetError = false;

			// Try to connect with Ble device
			if (resetError == false) {
				connectionError = await ConnectBleDevice_LineGraph(SelectedBleDeviceId);
			}

			// Try to initialize measurement and graph
			if (connectionError == false) {
				configLoadError = await LoadBleConfigCharacteristics(ConfigServiceUuid, Config_SamplingTimeCharacteristic);
			}

			if(configLoadError == false) {
				measInitError = await InitBleMeasurementGraph(MeasurementServiceUuid);
			}

			// List all services and characteristics 
			//_bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(SelectedBleDeviceId);
			//GattDeviceServicesResult result = await _bluetoothLeDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);
			//Utilities.ListAllBleServicesAndCharacteristics(result);
			
			if (resetError == false && connectionError == false && configLoadError == false && measInitError == false) {
				// Success
				rootPage.NotifyUser("Successfully connected", NotifyType.StatusMessage);
				return false;
			}

			// Error
			else if (resetError == false)
				rootPage.NotifyUser("Connection reset failed", NotifyType.ErrorMessage);
			else if (connectionError == false)
				rootPage.NotifyUser("Connection establishment failed", NotifyType.ErrorMessage);
			else if (configLoadError == false)
				rootPage.NotifyUser("Connection load of configuration failed", NotifyType.ErrorMessage);
			else if (measInitError == false)
				rootPage.NotifyUser("Connection load of measurement failed", NotifyType.ErrorMessage);
			return true;
		}

		public async Task<bool> ResetBleDevice() {
			// Note: For proper BLE disconnect its necessary to dispose everything related to the connection: https://social.msdn.microsoft.com/Forums/sqlserver/en-US/9eae39ff-f6ca-4aa9-adaf-97450f2b4a6c/disconnect-bluetooth-low-energy?forum=wdk

			bool resetError = false;
			Debug.WriteLine("ResetBleDevice ---");

			// Try to reset with device
			try {
				// Reset graph
				Colors_index = -1;
				if (GraphVisibleMeasurementCharacteristic != null) {
					Graph_ClearSeries();
				}
			}
			catch (Exception ex) {
				// Error
				Debug.WriteLine("Error on ResetBleDevice, Reset Graph: " + ex.Message);
				resetError = true;
			}

			// Unregister notifications/PropertyChanged and clear list of characteristics registered for measurement
			try {
				if (MeasurementCharacteristics != null) {
					foreach (MeasurementCharacteristic mc in MeasurementCharacteristics) {
						if (mc.IsNotifyEnabled == true) {
							try {
								await mc.DetachNotification();
							}
							catch (Exception ex) {
								Debug.WriteLine("Unable to detach notification: " + ex.Message);
							}
						}
					}
					MeasurementCharacteristics.Clear();
				}
			}
			catch (Exception ex) {
				// Error
				Debug.WriteLine("Error on ResetBleDevice, Reset MeasurementCharacteristics: " + ex.Message);
				resetError = true;
			}

			// Unregister PropertyChanged and clear list of characteristics registered for config
			try{
				if (ConfigCharacteristics != null) {
					foreach (ConfigCharacteristic cc in ConfigCharacteristics) {
						if (cc.GattCharacteristic.Uuid == MainPage.Config_SamplingTimeCharacteristic) {
							cc.PropertyChanged -= ConfigCharacteristic_PropertyChanged;
						}
					}
					ConfigCharacteristics.Clear();
				}
			}
			catch (Exception ex) {
				// Error
				Debug.WriteLine("Error on ResetBleDevice, Reset ConfigCharacteristics: " + ex.Message);
				resetError = true;
			}

			// Reset services
			try {
				_bleObject.ResetBleDevice();
			}
			catch (Exception ex) {
				// Error
				Debug.WriteLine("Error on ResetBleDevice, Reset Services: " + ex.Message);
				resetError = true;
			}

			// Success
			return resetError;
		}

		public async Task<bool> ConnectBleDevice_LineGraph(string SelectedBleDeviceId) {
			Debug.WriteLine("ConnectBleDevice_LineGraph ---");

			// Try to connect with device and get list of services
			try {
				// Connect and get connection object as well as services
				await _bleObject.ConnectBleDevice(SelectedBleDeviceId);

				// Checked status
				if (_bleObject.IsConnected == false) {
					// Error
					Debug.WriteLine("\tDevice connection failed");
					return true;
				}
				else {
					// Success
					Debug.WriteLine("\tDevice connection successful");
					return false;
				}
			}
			catch (Exception ex) { //when (ex.HResult == 0) {
				// Error
				rootPage.NotifyUser("Connect failed: " + ex.Message, NotifyType.ErrorMessage);
				return true;
			}
		}

		private async Task<bool> InitBleMeasurementGraph(Guid MeasurementServiceUuid) {
			Debug.WriteLine("InitBleMeasurementGraph ---");

			// Try to initialize measurements
			try {
				// Generate a list of Measurement characteristics and copy it to the used list
				ObservableCollection<MeasurementCharacteristic> temp = await MeasurementCharacteristic.GenerateMeasurementCharacteristicsAsync(_bleObject ,MeasurementServiceUuid, TextColors_Brush, Colors_index, MeasurementCharacteristic_ValueChanged);
				foreach (var chr in temp)
					MeasurementCharacteristics.Add(chr);
				temp.Clear();

				// Add data series to graph, if non is shown at the moment (this will set the first characteristic as the initially shown)
				if (MeasurementCharacteristics != null) {
					if (GraphVisibleMeasurementCharacteristic == null) {
						await Graph_AddNewSeries(MeasurementCharacteristics[0]);
					}
				}

				// No error - success
				return false;
			}
			catch (Exception ex) {
				// Error
				Debug.WriteLine("Error on InitBleMeasurementGraph: " + ex.Message);
				rootPage.NotifyUser("Can't load measurements: " + ex.Message, NotifyType.ErrorMessage);
				return true;
			}
		}


		private async Task<bool> LoadBleConfigCharacteristics(Guid ConfigServiceUuid, Guid Config_SamplingTimeCharacteristic) {
			Debug.WriteLine("LoadBleConfigCharacteristics ---");

			// Try to initialize configurations
			try {
				// Generate a list of config characteristics and copy it to the used list
				ObservableCollection<ConfigCharacteristic> temp = await ConfigCharacteristic.GenerateConfigCharacteristicsAsync(_bleObject, ConfigServiceUuid, MainPage.WellKnownCharacteristics);
				foreach (var chr in temp) {
					if (chr.WellKnownCharacteristic.IsVisible == true) {
						ConfigCharacteristics.Add(chr);
					}
				}
				temp.Clear();

				// Add data series to graph, if non is shown at the moment (this will set the first characteristic as the initially shown)
				if (ConfigCharacteristics != null) {
					foreach (var charact in ConfigCharacteristics) {

						/// Check if this characteristic contains config information needed:
						if (charact.GattCharacteristic.Uuid == Config_SamplingTimeCharacteristic) {
							_config_dataPointsTimeMultiplicator_Sec = charact.Value / _config_SamplingTimeDivider;
							charact.PropertyChanged += ConfigCharacteristic_PropertyChanged;
							Debug.WriteLine("Setting Sampling Time Config to " + _config_dataPointsTimeMultiplicator_Sec.ToString() + " sec per time step!");
						}

						/// ADD CONFIGURATIONS HERE IF NEEDED
					}
				}

				// No error - success
				return false;
			}
			catch (Exception ex) {
				// Error
				Debug.WriteLine("Error on LoadBleConfigCharacteristics: " + ex.Message);
				rootPage.NotifyUser("Can't load configurations: " + ex.Message, NotifyType.ErrorMessage);
				return true;
			}
		}

		public void Graph_ResetDataPoints() {
			foreach(MeasurementCharacteristic msc in MeasurementCharacteristics) {
				lock (Sync) {
					msc.ResetDataPoints();
				}
			}
		}

		public void Graph_ClearSeries() {
			// Remove PropertyChanged handler on currently shown MeasurementCharacteristic
			GraphVisibleMeasurementCharacteristic.PropertyChanged -= MeasurementCharacteristic_PropertyChanged;
			// Reset IsShownByGraph marker
			GraphVisibleMeasurementCharacteristic.IsShownByGraph = false;
			// Set target as currently visible MeasurementCharacteristic
			GraphVisibleMeasurementCharacteristic = null;
			// Create new series with target DataPoints
			Series.Clear();
		}

		public async Task Graph_AddNewSeries(MeasurementCharacteristic TargetMeasurementCharacteristic) {
			// Set target as currently visible MeasurementCharacteristic
			TargetMeasurementCharacteristic.IsShownByGraph = true;
			GraphVisibleMeasurementCharacteristic = TargetMeasurementCharacteristic;


			// Axis setting
			//ViewModel.YAxes[0].Name = "Y";
			YAxes[0].Labeler = (value) => value + " " + TargetMeasurementCharacteristic.Unit;
			YAxes[0].MinLimit = TargetMeasurementCharacteristic.MinValue;
			YAxes[0].MaxLimit = TargetMeasurementCharacteristic.MaxValue;
			YAxes[0].MinStep = 0.5;

			// Create color object
			SolidColorBrush brush = TargetMeasurementCharacteristic.ColorBrush;
			var sk_color = new SKColor(brush.Color.R, brush.Color.G, brush.Color.B, brush.Color.A);

			// Create new Series
			var NewSeries = new LineSeries<ObservablePoint> {
				Values = GraphVisibleMeasurementCharacteristic.DataPoints,
				Fill = null,
				Stroke = new SolidColorPaint(sk_color, 3),
				GeometryStroke = new SolidColorPaint(sk_color, 3),
				GeometrySize = 1,
			};
			// Get Name of Series
			var Name = "";
			try {
				Name = await GraphVisibleMeasurementCharacteristic.NameAsync();
			}
			catch (Exception e) {
				Debug.WriteLine("Unable to get series (descriptor) name: " + e.Message);
			}


			// Format Series
			NewSeries.LineSmoothness = 0.3;
			NewSeries.Name = Name;
			// Create new series with target DataPoints
			Series.Add(NewSeries);

			// Subscribe to special changes that are not bound directly in UI
			TargetMeasurementCharacteristic.PropertyChanged += MeasurementCharacteristic_PropertyChanged;
		}

		
		void MeasurementCharacteristic_PropertyChanged(object sender, PropertyChangedEventArgs e) {
			MeasurementCharacteristic chr;
			//Debug.WriteLine("MeasurementCharacteristic_PropertyChanged: " + e.PropertyName);

			switch (e.PropertyName) {
				case "MinValue":
					chr = (MeasurementCharacteristic)sender;
					if (chr.IsShownByGraph) {
						YAxes[0].MinLimit = chr.MinValue;
					}
					break;
				case "MaxValue":
					chr = (MeasurementCharacteristic)sender;
					if (chr.IsShownByGraph) {
						YAxes[0].MaxLimit = chr.MaxValue;
					}
					break;
			}
		}

		
		void ConfigCharacteristic_PropertyChanged(object sender, PropertyChangedEventArgs e) {
			ConfigCharacteristic chr;
			//Debug.WriteLine("ConfigCharacteristic_PropertyChanged: " + e.PropertyName);

			switch (e.PropertyName) {
				case "Value":
					chr = (ConfigCharacteristic)sender;
					if (chr.GattCharacteristic.Uuid == MainPage.Config_SamplingTimeCharacteristic) {
						// Change Multiplicand
						_config_dataPointsTimeMultiplicator_Sec = chr.Value / _config_SamplingTimeDivider;
						Debug.WriteLine("Setting Sampling Time Config to " + _config_dataPointsTimeMultiplicator_Sec.ToString() + " sec per time step!");
						rootPage.NotifyUser("Sampling time has changed to " + _config_dataPointsTimeMultiplicator_Sec.ToString() + "s!", NotifyType.StatusMessage);
					}
					break;
			}
		}


		private async void MeasurementCharacteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args) {
			MeasurementCharacteristic myMeasChr;
			try {
				// Find corresponding Object and manage data
				myMeasChr = MeasurementCharacteristics.Where(x => x.GattCharacteristic.Uuid == sender.Uuid).First();

				// Receive value of MeasurementCharacteristic and store it in DataPoints
				// Note: This must run in context of the UI because it ultimately changes variables directly bound to it: https://social.msdn.microsoft.com/Forums/windowsapps/en-US/f1fe027f-2c6c-4a22-9418-566fcf67cebe/the-application-called-an-interface-that-was-marshalled-for-a-different-thread?forum=winappswithcsharp, https://stackoverflow.com/questions/19341591/the-application-called-an-interface-that-was-marshalled-for-a-different-thread
				await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
				async () => {
					// Receive value, format it and store it in DataPoints list
					if (myMeasChr != null && myMeasChr == GraphVisibleMeasurementCharacteristic) {
						// If the received value belongs to the series shown on graph we need to lock the graph so it cannot read while we write 
						lock (Sync) {
							Task task = myMeasChr.StoreFormattedNumValue(_config_dataPointsTimeMultiplicator_Sec); // Todo: This may not be clean
						}
					}
					else if (myMeasChr != null) {
						// Store value
						await myMeasChr.StoreFormattedNumValue(_config_dataPointsTimeMultiplicator_Sec);
					}
					else {
						Debug.WriteLine("Cannot find corresponding MeasurementCharacteristic!");
					}
				});
			}
			catch (Exception e) {
				Debug.WriteLine("Received a notification value but failed at ValueChanged event: " + e.Message);
			}
		}


	}

}
