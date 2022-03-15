using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
// From BluetoothLE example
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.UI.Core;
using Windows.UI.Xaml.Navigation;

using System.Threading.Tasks;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using LiveChartsCore.Defaults;
using Windows.Security.Cryptography;
using System.Text;
using LiveChartsCore.SkiaSharpView;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.ViewManagement;
using Windows.UI;
using System.Collections.Specialized;
using Windows.UI.Popups;

//#pragma warning disable IDE0007 // Use implicit type
//#pragma warning disable IDE0055 // Use implicit type
namespace BLE_Graph
{
	/// <summary>
	/// An empty page used to list bluetooth devices and load a view to the ContentControl
	/// </summary>
	public sealed partial class MainPage : Page
	{
		public static MainPage Current;
		
		private ObservableCollection<BluetoothLEDeviceDisplay> _knownDevices = new ObservableCollection<BluetoothLEDeviceDisplay>();
		private List<DeviceInformation> _unknownDevices = new List<DeviceInformation>();

		private DeviceWatcher _deviceWatcher;

		public static string SelectedBleDeviceId;
		public static string SelectedBleDeviceName = "No device selected";

		public static LineGraph.View GraphView;
		public static Log.View LogView;

		// Hard coded IDs of Services and Characteristics - These could be made changeable in GUId if needed
		public readonly static Guid MeasurementServiceUuid = new Guid("2a13dada-295d-f7af-064f-28eac027639f");
		public readonly static Guid ConfigServiceUuid	= new Guid("2119458a-f72c-269b-4d4d-2df0319121dd");
		public readonly static Guid Config_SamplingTimeCharacteristic = new Guid("8420e6c6-49ba-7c8d-104f-10fe496d061f");
		public readonly static Guid Config_CalibrationReferenceValueCharacteristic = new Guid("6f8afe94-a93d-cfb2-1b47-da0f98d9bfa1");
		public readonly static Guid Config_EnableSensorCalibrationCharacteristic   = new Guid("e64d0510-07f3-ac96-9c4d-5af82839425c"); 

		// This list contains information on how to  interpret, show and set known characteristics
		public static readonly List<WellKnownCharacteristic> WellKnownCharacteristics = new List<WellKnownCharacteristic>() {
			new WellKnownCharacteristic(new Guid("4ef31e63-93b4-eca8-3846-84684719c484"), WellKnownCharacteristic.TypeCharacteristic.MCI_MeasurementCharacteristic, "CO2",         CharacteristicDataFormats.VarInt,            "ppm",     0, 1400,           true),
			new WellKnownCharacteristic(new Guid("0b4f4b0c-0795-1fab-a44d-ab5297a9d33b"), WellKnownCharacteristic.TypeCharacteristic.MCI_MeasurementCharacteristic, "Pressure",    CharacteristicDataFormats.VarInt,            "Pa",  95000, 10000,          true),
			new WellKnownCharacteristic(new Guid("7eb330af-8c43-f0ab-8e41-dc2adb4a3ce4"), WellKnownCharacteristic.TypeCharacteristic.MCI_MeasurementCharacteristic, "Temperature", CharacteristicDataFormats.Int2B_Point_Int2B, "°C",     -5,    30,          true),
			new WellKnownCharacteristic(new Guid("421da449-112f-44b6-4743-5c5a7e9c9a1f"), WellKnownCharacteristic.TypeCharacteristic.MCI_MeasurementCharacteristic, "Humidity",    CharacteristicDataFormats.Int2B_Point_Int2B, "%" ,      0,    50,          true),
			new WellKnownCharacteristic(new Guid("8420e6c6-49ba-7c8d-104f-10fe496d061f"), WellKnownCharacteristic.TypeCharacteristic.MCI_ConfigCharacteristic,		"Measurement Rate",		CharacteristicDataFormats.VarUInt,	"ms",    750, Double.MaxValue,true),
			new WellKnownCharacteristic(new Guid("6ab7f5b0-7eac-31b9-5d40-5efa6c8d67d4"), WellKnownCharacteristic.TypeCharacteristic.MCI_ConfigCharacteristic,		"Pressure Compensation",CharacteristicDataFormats.VarUInt,	"hPa",     0, Double.MaxValue,true),
			new WellKnownCharacteristic(new Guid("4ffb7e99-85ba-de86-4242-004f76f23409"), WellKnownCharacteristic.TypeCharacteristic.MCI_ConfigCharacteristic,		"Alarm Threshold",		CharacteristicDataFormats.VarUInt,	"ppm",     0, 1000000,        true),
			new WellKnownCharacteristic(new Guid("6f8afe94-a93d-cfb2-1b47-da0f98d9bfa1"), WellKnownCharacteristic.TypeCharacteristic.MCI_ConfigCharacteristic,		"Offset Compensation",  CharacteristicDataFormats.VarUInt,	"ppm",     0, 1000000,        true),
			new WellKnownCharacteristic(new Guid("e64d0510-07f3-ac96-9c4d-5af82839425c"), WellKnownCharacteristic.TypeCharacteristic.MCI_ConfigCharacteristic,		"Start Sensor Calibration",  CharacteristicDataFormats.VarUInt,	"",     0, 1000000,       false)
		};

		// This list contains information on all Applications and how to handle them
		private BLE_Application CurrentlyShownApplication = null;
		public List<BLE_Application> BLE_Applications = new List<BLE_Application>() {
			new BLE_Application("BLE_App_CO2",     "XENSIV PAS CO2 ™", BLE_Application.ApplicationTypes.BLE_LineGraph_Common),
			new BLE_Application("BLE_CO2_SensNet", "XENSIV PAS CO2 ™", BLE_Application.ApplicationTypes.BLE_LineGraph_Common)
		};


		public MainPage()
		{
			// Initialize objects
			InitializeComponent();

			// This is a static public property that allows downstream pages to get a handle to the MainPage instance
			// in order to call methods that are in this class.
			Current = this;

			// Create an load an instance of the line-graph view to the main area
			var t_LineGraph = Type.GetType($"LineGraph.View");
			GraphView = (LineGraph.View)Activator.CreateInstance(t_LineGraph);
			content.Content = GraphView;

			var t_Log = Type.GetType($"Log.View");
			LogView = (Log.View)Activator.CreateInstance(t_Log);

			// Start 
			StartBleDeviceWatcher();

			// Update device counter if known device collection changed (detection/filter)
			_knownDevices.CollectionChanged += new NotifyCollectionChangedEventHandler(KnownDevices_CollectionChanged);

			// Set color of title bar
			ApplicationViewTitleBar titleBar = ApplicationView.GetForCurrentView().TitleBar;
			titleBar.BackgroundColor = Windows.UI.Color.FromArgb(0xFF, 0x5E, 0xA2, 0x90);
			titleBar.ForegroundColor = Colors.White;
			titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0xFF, 0x5E, 0xA2, 0x90);
			titleBar.ButtonForegroundColor = Colors.White;
		}

	private void KnownDevices_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
		//different kind of changes that may have occurred in collection
		if (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Remove || e.Action == NotifyCollectionChangedAction.Reset || e.Action == NotifyCollectionChangedAction.Replace || e.Action == NotifyCollectionChangedAction.Move)
			FilterCountTextBlock.Text = _knownDevices.Count.ToString();
	}

	private async void DisconnectBleButton_Click() {
			ReloadBleProgressRing.IsActive = true;
			ResultsListView.IsEnabled = false;

			await DisconnectAndResetContentView();
			NotifyUser("Disconnected", NotifyType.StatusMessage);

			DisconnectBleButton.IsEnabled = false;
			ReloadBleProgressRing.IsActive = false;
			ResultsListView.IsEnabled = true;
		}

		private void ReloadBleButton_Click() {
			if (_deviceWatcher == null)
				StartBleDeviceWatcher();
			else
				StopBleDeviceWatcher();
		}

		private async void ResultsListView_ItemClick(object sender, ItemClickEventArgs e)
		{
			ReloadBleProgressRing.IsActive = true;
			ResultsListView.IsEnabled = false;

			// Stop Watcher - Device is already selected and we don't want changes after this
			StopBleDeviceWatcher();

			// Get selected BLE device and check if not empty
			var bleDeviceDisplay = (BluetoothLEDeviceDisplay)e.ClickedItem; //var bleDeviceDisplay = ResultsListView.SelectedItem as BluetoothLEDeviceDisplay; //
			if (bleDeviceDisplay != null) {
				// Store selected device id and name
				SelectedBleDeviceId = bleDeviceDisplay.Id;
				SelectedBleDeviceName = bleDeviceDisplay.Name;
				Debug.WriteLine(String.Format("Item Clicked: {0}", SelectedBleDeviceId));

				// Reset content view (if necessary) based on currently shown application type
				try {
					await DisconnectAndResetContentView();
				}
				catch (Exception ex) {
					// Error
					NotifyUser("Failed to disconnect and reset View: " + ex.Message, NotifyType.ErrorMessage);
				}

				// Set environment based on device name
				try {
					await ConnectAndLoadContentView(bleDeviceDisplay);
				}
				catch (Exception ex) {
					// Error
					NotifyUser("Failed to connect and load view: " + ex.Message, NotifyType.ErrorMessage);
				}

			}
			else {
				NotifyUser("Selected Item is no valid device (NULL)", NotifyType.ErrorMessage);
				Debug.WriteLine("Selected Item is no valid device (NULL)");
			}

			ReloadBleProgressRing.IsActive = false;
			ResultsListView.IsEnabled = true;
		}


		private async Task DisconnectAndResetContentView() {
			// Reset content view (if necessary) based on currently shown application type
			Header.Text = "";
			if (CurrentlyShownApplication != null) {
				switch (CurrentlyShownApplication.ApplicationType) {
					case BLE_Application.ApplicationTypes.BLE_LineGraph_Common:
						// Initiate Graph View - BLE reset, connect and initialize measurement
						await GraphView.ViewModel.ResetBleDevice();
						Debug.WriteLine("Resetting BLE_LineGraph_Common!");
						break;
					default:
						// Type is unknown
						Debug.WriteLine("The currently shown application is of unknown type, no reset necessary!");
						break;
				}
			}
			else {
				Debug.WriteLine("No CurrentlyShownApplication found - no reset necessary!");
			}
		}

		private async Task ConnectAndLoadContentView(BluetoothLEDeviceDisplay bleDeviceDisplay) {
			// Set environment based on device name
			foreach (var AppType in BLE_Applications) {
				if (SelectedBleDeviceName == AppType.DeviceName) {
					// Set application header name
					Header.Text = AppType.HeaderName;

					// Initiate load of content view based on type
					switch (AppType.ApplicationType) {
						case BLE_Application.ApplicationTypes.BLE_LineGraph_Common:
							// Initiate Graph View - BLE reset, connect and initialize measurement
							NotifyUser("Loading Line Graph for Device: " + bleDeviceDisplay.Address, NotifyType.StatusMessage);
							await GraphView.ViewModel.LoadBleDevice(SelectedBleDeviceId, MeasurementServiceUuid, ConfigServiceUuid, Config_SamplingTimeCharacteristic, false);
							DisconnectBleButton.IsEnabled = true;
							CurrentlyShownApplication = AppType;
							break;
						default:
							// Type is unknown
							NotifyUser("The selected device is of unknown type! No application can be loaded!", NotifyType.ErrorMessage);
							Debug.WriteLine("The selected device is of unknown type!");
							break;
					}

					break;
				}

			}
			if (Header.Text == "") {
				NotifyUser("The selected device application is unknown!", NotifyType.ErrorMessage);
				Debug.WriteLine("The selected device application is unknown!");
			}
		}


		#region Device discovery

		/// <summary>
		/// Starts a device watcher that looks for all nearby Bluetooth devices (paired or unpaired). 
		/// Attaches event handlers to populate the device collection.
		/// </summary>
		private void StartBleDeviceWatcher()
		{
			ReloadBleButton.Content = "Stop BLE Detection";
			Debug.WriteLine("Start BLE Watcher");
			ReloadBleProgressRing.IsActive = true;

			// Additional properties we would like about the device.
			// Property strings are documented here https://msdn.microsoft.com/en-us/library/windows/desktop/ff521659(v=vs.85).aspx
			string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.Devices.Aep.Bluetooth.Le.IsConnectable" };

			// BT_Code: Example showing paired and non-paired in a single query.
			string aqsAllBluetoothLEDevices = "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";

			_deviceWatcher = DeviceInformation.CreateWatcher(
						aqsAllBluetoothLEDevices,
						requestedProperties,
						DeviceInformationKind.AssociationEndpoint);

			// Register event handlers before starting the watcher.
			_deviceWatcher.Added += DeviceWatcher_Added;
			_deviceWatcher.Updated += DeviceWatcher_Updated;
			_deviceWatcher.Removed += DeviceWatcher_Removed;
			_deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
			_deviceWatcher.Stopped += DeviceWatcher_Stopped;

			// Start over with an empty collection.
			_knownDevices.Clear();

			// Start the watcher. Active enumeration is limited to approximately 30 seconds.
			// This limits power usage and reduces interference with other Bluetooth activities.
			// To monitor for the presence of Bluetooth LE devices for an extended period,
			// use the BluetoothLEAdvertisementWatcher runtime class. See the BluetoothAdvertisement
			// sample for an example.
			_deviceWatcher.Start();
		}

		/// <summary>
		/// Stops watching for all nearby Bluetooth devices.
		/// </summary>
		private void StopBleDeviceWatcher()
		{
			ReloadBleButton.Content = "Scan for BLE Devices";
			Debug.WriteLine("Stop BLE Watcher");
			ReloadBleProgressRing.IsActive = false;

			if (_deviceWatcher != null)
			{
				// Unregister the event handlers.
				_deviceWatcher.Added -= DeviceWatcher_Added;
				_deviceWatcher.Updated -= DeviceWatcher_Updated;
				_deviceWatcher.Removed -= DeviceWatcher_Removed;
				_deviceWatcher.EnumerationCompleted -= DeviceWatcher_EnumerationCompleted;
				_deviceWatcher.Stopped -= DeviceWatcher_Stopped;

				// Stop the watcher.
				_deviceWatcher.Stop();
				_deviceWatcher = null;
			}
		}

		private BluetoothLEDeviceDisplay FindBluetoothLEDeviceDisplay(string id)
		{
			foreach (BluetoothLEDeviceDisplay bleDeviceDisplay in _knownDevices) {
				if (bleDeviceDisplay.Id == id) {
					return bleDeviceDisplay;
				}
			}
			return null;
		}

		private DeviceInformation FindUnknownDevices(string id)
		{
			foreach (DeviceInformation bleDeviceInfo in _unknownDevices) {
				if (bleDeviceInfo.Id == id) {
					return bleDeviceInfo;
				}
			}
			return null;
		}

		private async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation deviceInfo) {
			// We must update the collection on the UI thread because the collection is databound to a UI element.
			await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
				lock (this) {
					//Debug.WriteLine(String.Format("Added {0}{1}", deviceInfo.Id, deviceInfo.Name));

					// Protect against race condition if the task runs after the app stopped the deviceWatcher.
					if (sender == _deviceWatcher) {
						// Make sure device isn't already present in the list.
						if (FindBluetoothLEDeviceDisplay(deviceInfo.Id) == null) {
							// Get Connection status
							bool IsConnected = ((bool?)deviceInfo.Properties["System.Devices.Aep.IsConnected"] == true);
							bool IsConnectable = ((bool?)deviceInfo.Properties["System.Devices.Aep.Bluetooth.Le.IsConnectable"] == true);

							// Only add device if its connectible and not already connected
							if (deviceInfo.Name != string.Empty && IsConnectable && !IsConnected) {
								BluetoothLEDeviceDisplay devInfo = new BluetoothLEDeviceDisplay(deviceInfo);

								if (String.IsNullOrWhiteSpace(DeviceFilterTextBox.Text)) {
									// If device has a friendly name display it immediately.
									//_knownDevices.Add(devInfo);
									_knownDevices.Insert(GetIndexInOrderedList(_knownDevices, devInfo), devInfo);
								}
								else {
									if (devInfo.Address.Contains(DeviceFilterTextBox.Text)) {
										Debug.WriteLine("Added filtered device: " + deviceInfo.Id);
										//_knownDevices.Add(devInfo);
										_knownDevices.Insert(GetIndexInOrderedList(_knownDevices, devInfo), devInfo);
									}
									else
										_unknownDevices.Add(deviceInfo);
								}
							}
							else {
								// Add it to a list in case the name gets updated later. 
								_unknownDevices.Add(deviceInfo);
							}
						}

					}
				}
			});
		}

		private int GetIndexInOrderedList(ObservableCollection<BluetoothLEDeviceDisplay> List, BluetoothLEDeviceDisplay Item) {
			int result = List.Count - 1;
			if (result < 0) 
				return 0;

			int listIndex = 0;
			foreach(var listItem in List) {
				int compareResult = string.Compare(Item.Address, listItem.Address, StringComparison.Ordinal);
				if (compareResult < 0 && result > listIndex) {
					result = listIndex;
				}

				listIndex++;
			}

			if (result >= 0 && result < List.Count)
				return result;
			else {
				Debug.WriteLine("Error, index was out of bounds. List: " + List.Count + ", Result: " + result);
				return 0;
			}
		}

		private async void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate) {
			// We must update the collection on the UI thread because the collection is databound to a UI element.
			await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
				lock (this) {
					//Debug.WriteLine(String.Format("Updated {0}{1}", deviceInfoUpdate.Id, ""));

					// Protect against race condition if the task runs after the app stopped the deviceWatcher.
					if (sender == _deviceWatcher) {
						BluetoothLEDeviceDisplay bleDeviceDisplay = FindBluetoothLEDeviceDisplay(deviceInfoUpdate.Id);
						if (bleDeviceDisplay != null) {
							// Device is already being displayed - update UX.
							bleDeviceDisplay.Update(deviceInfoUpdate);
							return;
						}

						DeviceInformation deviceInfo = FindUnknownDevices(deviceInfoUpdate.Id);
						if (deviceInfo != null) {
							deviceInfo.Update(deviceInfoUpdate);

							// Get Connection status
							bool IsConnected = ((bool?)deviceInfo.Properties["System.Devices.Aep.IsConnected"] == true);
							bool IsConnectable = ((bool?)deviceInfo.Properties["System.Devices.Aep.Bluetooth.Le.IsConnectable"] == true);

							// If device has been updated with a friendly name, is connectible and not already connected, it's no longer unknown.
							if (deviceInfo.Name != string.Empty && IsConnectable && !IsConnected) {
								BluetoothLEDeviceDisplay devInfo = new BluetoothLEDeviceDisplay(deviceInfo);

								if (String.IsNullOrWhiteSpace(DeviceFilterTextBox.Text)) {
									// If device has a friendly name display it immediately.
									//_knownDevices.Add(devInfo);
									_knownDevices.Insert(GetIndexInOrderedList(_knownDevices, devInfo), devInfo);
								}
								else {
									if (devInfo.Address.Contains(DeviceFilterTextBox.Text)) {
										Debug.WriteLine("Updated filtered device: " + deviceInfo.Id);
										//_knownDevices.Add(devInfo);
										_knownDevices.Insert(GetIndexInOrderedList(_knownDevices, devInfo), devInfo);
									}
									else
										_unknownDevices.Add(deviceInfo);
								}

								try {
									_unknownDevices.Remove(deviceInfo);
								}
								catch { }
							}
						}
					}
				}
			});
		}

		private async void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate) {
			// We must update the collection on the UI thread because the collection is databound to a UI element.
			await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
				lock (this) {
					//Debug.WriteLine(String.Format("Removed {0}{1}", deviceInfoUpdate.Id, ""));

					// Protect against race condition if the task runs after the app stopped the deviceWatcher.
					if (sender == _deviceWatcher) {
						// Find the corresponding DeviceInformation in the collection and remove it.
						BluetoothLEDeviceDisplay bleDeviceDisplay = FindBluetoothLEDeviceDisplay(deviceInfoUpdate.Id);
						if (bleDeviceDisplay != null) {
							_knownDevices.Remove(bleDeviceDisplay);
						}

						DeviceInformation deviceInfo = FindUnknownDevices(deviceInfoUpdate.Id);
						if (deviceInfo != null) {
							_unknownDevices.Remove(deviceInfo);
						}
					}
				}
			});
		}

		private async void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object e) {
			// We must update the collection on the UI thread because the collection is databound to a UI element.
			await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
				// Protect against race condition if the task runs after the app stopped the deviceWatcher.
				if (sender == _deviceWatcher) {
					Debug.WriteLine($"{_knownDevices.Count} devices found. Enumeration completed.");
					//ReloadBleButton.Content = "Start BLE Detection";
					StopBleDeviceWatcher();
				}
			});
		}

		private async void DeviceWatcher_Stopped(DeviceWatcher sender, object e) {
			// We must update the collection on the UI thread because the collection is databound to a UI element.
			await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
				// Protect against race condition if the task runs after the app stopped the deviceWatcher.
				if (sender == _deviceWatcher) {
					Debug.WriteLine($"No longer watching for devices.");
				}
			});
		}


		#endregion

		int MenuButtonCount = 0;
		int ConfigButtonCount = 0;
		private void ToggleMenuButton_Click(object sender, RoutedEventArgs e) {
			// Open/close left menu
			Splitter.IsPaneOpen = !Splitter.IsPaneOpen;

			// Hidden mass config feature - Experimental feature used to calibrate multiple sensors to the same reference value. Cannot be undone!
			MenuButtonCount++;
		}

		private void ToggleConfigButton_Click(object sender, RoutedEventArgs e) {
			// Try to open/close right menu
			try { GraphView.ToggleConfigPane(); }
			catch { }

			// Hidden mass config feature - Experimental feature used to calibrate multiple sensors to the same reference value. Cannot be undone!
			if (MenuButtonCount == 4)
				ConfigButtonCount++;
			if (MenuButtonCount == 4 && ConfigButtonCount == 4)
				CalibrationCheckBox.Visibility = Visibility.Visible;
		}

		private void ToggleLogButton_Click(object sender, RoutedEventArgs e) {
			if (content.Content == GraphView)
				content.Content = LogView;
			else
				content.Content = GraphView;
		}

		/// <summary>
		/// Display a message to the user.
		/// This method may be called from any thread.
		/// </summary>
		/// <param name="strMessage"></param>
		/// <param name="type"></param>
		public void NotifyUser(string strMessage, NotifyType type) {
			// If called from the UI thread, then update immediately.
			// Otherwise, schedule a task on the UI thread to perform the update.
			if (Dispatcher.HasThreadAccess) {
				UpdateStatus(strMessage, type);
			}
			else {
				var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateStatus(strMessage, type));
			}
		}

		private void UpdateStatus(string strMessage, NotifyType type) {
			switch (type) {
				case NotifyType.StatusMessage:
					StatusBlock.Foreground = new SolidColorBrush(Windows.UI.Colors.Green);
					break;
				case NotifyType.ErrorMessage:
					StatusBlock.Foreground = new SolidColorBrush(Windows.UI.Colors.Red);
					break;
			}

			// Add Message to Notifications list and show it
			LogView.ViewModel.UserNotifications.Insert(0, new UserNotification(strMessage, type));
			//LogView.ViewModel.UserNotifications.Add(new UserNotification(strMessage, type));
			Debug.Write(strMessage + "\r\n");
			StatusBlock.Text = strMessage;
			StatusBlock.Visibility = Visibility.Visible;

			// Collapse the StatusBlock if it has no text to conserve real estate.
			//StatusBorder.Visibility = (StatusBlock.Text != String.Empty) ? Visibility.Visible : Visibility.Collapsed;
			//if (StatusBlock.Text != String.Empty) {
			//	StatusBorder.Visibility = Visibility.Visible;
			//	StatusPanel.Visibility = Visibility.Visible;
			//}
			//else {
			//	StatusBorder.Visibility = Visibility.Collapsed;
			//	StatusPanel.Visibility = Visibility.Collapsed;
			//}

			// Raise an event if necessary to enable a screen reader to announce the status update.
			var peer = FrameworkElementAutomationPeer.FromElement(StatusBlock);
			if (peer != null) {
				peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
			}
		}

		private void DeviceFilterTextBox_KeyDown(object sender, KeyRoutedEventArgs e) {
			if (e.Key == Windows.System.VirtualKey.Enter) {
				StopBleDeviceWatcher();
				_knownDevices.Clear();
				_knownDevices.Clear();

				StartBleDeviceWatcher();
			}
		}


		#region MASS CALIBRATION
		private void CalibrationCheckBox_Click(object sender, RoutedEventArgs e) {
			if (CalibrationCheckBox.IsChecked == false)
				CalibrationPanel.Visibility = Visibility.Collapsed;
			else
				CalibrationPanel.Visibility = Visibility.Visible;
		}

		private async void CalibrationStartButton_Click(object sender, RoutedEventArgs e) {
			StopBleDeviceWatcher();

			// Ask user if he really wants to do this
			MessageDialog dialog = new MessageDialog("This will start a calibration cycle on every currently shown Bluetooth device and can not be undone. Are You sure?",  "Start Calibration");
			dialog.Commands.Add(new UICommand("Yes"));
			dialog.Commands.Add(new UICommand("No"));
			dialog.DefaultCommandIndex = 1; // Set the command that will be invoked by default
			dialog.CancelCommandIndex = 1; // Set the command to be invoked when escape is pressed
			var result = await dialog.ShowAsync();

			if (result.Label == "Yes") {
				// Reset current view
				try { await DisconnectAndResetContentView(); }
				catch { }

				// Convert entered reference value and check if its legal
				short RefVal = 0;
				try { RefVal = Convert.ToInt16(CalibrationValueTextBox.Text); }
				catch { }
				if (RefVal < 350 || RefVal > 900) {
					NotifyUser("Error: Reference value must be set between 350 and 900 ppm!", NotifyType.ErrorMessage);
					return; // If conversion failed or out of range stop calibration
				}

				// Connect with every device and start calibration
				using (BleModel BleObj = new BleModel()) {
					foreach (var knownDevice in _knownDevices) {
						try {
							NotifyUser("Try to start calibration (Reference value: " + RefVal.ToString() + " ppm) on Device: " + knownDevice.Address, NotifyType.StatusMessage);

							// Disconnect device - Try to reset internal variables (Device, Characteristics, etc)
							BleObj.ResetBleDevice();
							System.Threading.Thread.Sleep(500);

							// Try to connect with Ble device
							await BleObj.ConnectBleDevice(knownDevice.Id);

							// Write reference Value
							byte[] refData = BitConverter.GetBytes((short)RefVal);
							await BleObj.WriteValueAsync(ConfigServiceUuid, Config_CalibrationReferenceValueCharacteristic, refData);

							// Start calibration
							byte[] calibData = new byte[] { 1 };
							await BleObj.WriteValueAsync(ConfigServiceUuid, Config_EnableSensorCalibrationCharacteristic, calibData);
							System.Threading.Thread.Sleep(500);

							NotifyUser("Successfully started calibration", NotifyType.StatusMessage);
						}
						catch (Exception ex) {
							NotifyUser("Failed to start calibration on Device " + knownDevice.Address + ": " + ex.Message, NotifyType.ErrorMessage);
						}
					}
				}

				NotifyUser("End of mass calibration (see log). Wait till all boards stop flashing!", NotifyType.StatusMessage);
			}
		}

		private async void CalibrationResetButton_Click(object sender, RoutedEventArgs e) {
			StopBleDeviceWatcher();

			// Ask user if he really wants to do this
			MessageDialog dialog = new MessageDialog("This will start a calibration cycle on every currently shown Bluetooth device and can not be undone. Are You sure?", "Start Calibration");
			dialog.Commands.Add(new UICommand("Yes"));
			dialog.Commands.Add(new UICommand("No"));
			dialog.DefaultCommandIndex = 1; // Set the command that will be invoked by default
			dialog.CancelCommandIndex = 1; // Set the command to be invoked when escape is pressed
			var result = await dialog.ShowAsync();

			if (result.Label == "Yes") {

				// Reset current view
				try { await DisconnectAndResetContentView(); }
				catch { }

				// Connect with every device and reset calibration
				using (BleModel BleObj = new BleModel()) {
					foreach (var knownDevice in _knownDevices) {
						try {
							NotifyUser("Try to reset calibration (reference value to 0 and flag to 255) on Device: " + knownDevice.Address, NotifyType.StatusMessage);

							// Try to connect with Ble device
							await BleObj.ConnectBleDevice(knownDevice.Id);

							// Write reference Value
							byte[] refData = BitConverter.GetBytes((short)0);
							await BleObj.WriteValueAsync(ConfigServiceUuid, Config_CalibrationReferenceValueCharacteristic, refData);

							// Start calibration
							byte[] calibData = new byte[] { 255 };
							await BleObj.WriteValueAsync(ConfigServiceUuid, Config_EnableSensorCalibrationCharacteristic, calibData);

							// Disconnect device - Try to reset internal variables (Device, Characteristics, etc)
							BleObj.ResetBleDevice();

							// Disconnect device - Try to reset internal variables (Device, Characteristics, etc)
							await GraphView.ViewModel.ResetBleDevice();

						}
						catch (Exception ex) {
							NotifyUser("Failed to reset calibration on Device " + knownDevice.Address + ": " + ex.Message + ", " + ex.StackTrace, NotifyType.ErrorMessage);
						}
					}
				}

				NotifyUser("End of mass calibration reset (see log)", NotifyType.StatusMessage);
			}
		}
		#endregion


	}

	public class UserNotification
	{
		public NotifyType Type;
		public string Text;
		public SolidColorBrush Brush;
		public DateTime Time;
		public string Format = "dd.MM.yyyy hh:mm:ss";

		public UserNotification(string text, NotifyType type) {
			Type = type;
			Text = text;
			Time = DateTime.Now;

			switch (type) {
				case NotifyType.StatusMessage:
					Brush = new SolidColorBrush(Windows.UI.Colors.Green);
					break;
				case NotifyType.ErrorMessage:
					Brush = new SolidColorBrush(Windows.UI.Colors.Red);
					break;
				default:
					Brush = new SolidColorBrush(Windows.UI.Colors.Black);
					break;
			}
		}

	}

	public enum NotifyType
	{
		StatusMessage,
		ErrorMessage
	};

	public class BLE_Application
	{
		public string DeviceName;
		public string HeaderName;
		public ApplicationTypes ApplicationType;

		public enum ApplicationTypes
		{
			BLE_LineGraph_Common
		}

		public BLE_Application(string deviceName, string headerName, ApplicationTypes applicationType) {
			DeviceName = deviceName;
			HeaderName = headerName;
			ApplicationType = applicationType;
		}
	}

}




