using System;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.Security.Cryptography;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using LiveChartsCore.Defaults;
using SkiaSharp;
using Windows.UI.Xaml.Media;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using System.Threading;

namespace BLE_Graph
{

	/// <summary>
	///     This enumeration defines possible formats in which the data of a characteristic can be interpreted as
	/// </summary>
	public enum CharacteristicDataFormats
	{
		VarInt,
		VarUInt,
		VarFloat,
		Int2B_Point_Int2B
	}

	/// <summary>
	///     This class is used to make a list of known characteristics - meaning to provide a set of information to be used when a specific characteristic is found (id, display name, type, format of the data, unit, etc)
	/// </summary>
	public class WellKnownCharacteristic
	{
		public WellKnownCharacteristic(Guid guid, TypeCharacteristic type, string name, CharacteristicDataFormats dataformat, string unit, double? minValue, double? maxValue, bool isVisible) {
			ID_Guid = guid;
			Type = type;
			Name = name;
			DataFormat = dataformat;
			Unit = unit;
			MinValue = minValue;
			MaxValue = maxValue;
			IsVisible = isVisible;
		}

		public enum TypeCharacteristic
		{
			MCI_MeasurementCharacteristic,
			MCI_ConfigCharacteristic
		}

		public readonly Guid ID_Guid;
		public readonly TypeCharacteristic Type;
		public readonly string Name;
		public readonly CharacteristicDataFormats DataFormat;
		public readonly string Unit;
		public readonly double? MinValue;
		public readonly double? MaxValue;
		public readonly bool IsVisible;
	}

	/// <summary>
	///     This class is used for characteristics whose data shall be set or get. 
	///     It implements the parse of the Descriptor or the use of a well known list to identify Name, DataFormat, Unit etc of the data.
	///     The Interface INotifyPropertyChanged is implemented to allow changing values to be displayed at the UI.
	///		Use this as base class for more specific characteristic types
	/// </summary>
	/// <param name="SourceChr">The actual GattCharacteristic to be attached</param>
	/// <param name="ListIndex">If used in a list use this as index</param>
	/// <param name="wellKnownCharacteristics">A Well known list to help interpret Name, DataFormat, Unit etc</param>
	public class MCI_Characteristic : INotifyPropertyChanged
	{
		private int _objIndex;
		private readonly GattCharacteristic _gattCharacteristic;
		private readonly WellKnownCharacteristic _wellKnownCharacteristic = null;

		// Info for and from characteristic descriptor
		private bool _isDescriptorParsed;
		private string _descriptorString;
		private string _name;             // The name of the chr., split from descriptor (or from well known)
		private CharacteristicDataFormats _dataFormat;  // The format in which the chr. data shall be represented, split from descriptor (or from well known)
		private string _unit;             // The unit of the chr. data, split from descriptor (or from well known)

		private double _curValue;

		private double? _minValue;
		private double? _maxValue;

		[Description("The size of the data field of the characteristic in byte. Is written when GetFormattedValue() is executed!")]
		public int DataSize {
			get;
			private set;
		}


		public MCI_Characteristic(GattCharacteristic SourceChr, int ListIndex, List<WellKnownCharacteristic> wellKnownCharacteristics) {
			// Set index of object (used when object is part of an list)
			_objIndex = ListIndex;

			// Store base object of characteristic
			_gattCharacteristic = SourceChr;

			// Default Values
			_isDescriptorParsed = false;
			DataSize = 0;
			_curValue = double.NaN;
			_minValue = null;
			_maxValue = null;

			// List of known characteristics which is used to get name, data format and unit of these
			if (wellKnownCharacteristics != null) {
				_wellKnownCharacteristic = wellKnownCharacteristics.Find(x => x.ID_Guid == SourceChr.Uuid);
			}
		}

		public int ObjIndex { get => _objIndex; }
		public GattCharacteristic GattCharacteristic { get => _gattCharacteristic; }
		public WellKnownCharacteristic WellKnownCharacteristic { get => _wellKnownCharacteristic; }
		public bool IsDescriptorParsed { get => _isDescriptorParsed; }
		public string DescriptorString { get => _descriptorString; }

		public double CurValue { get => _curValue; }

		public double? MinValue {
			get => _minValue;
			set {
				_minValue = value;
				NotifyPropertyChanged("MinValue"); // This seems not to work for some reason
			}
		}
		public double? MaxValue {
			get => _maxValue;
			set {
				_maxValue = value;
				NotifyPropertyChanged("MaxValue"); // This seems not to work for some reason
			}
		}

		[Description("Name of the characteristic. Ensure that ParseCharacteristicMainDescriptor() runs first or use NameAsync()!")]
		public string Name {
			get => _name;
			private set {
				_name = value;
				NotifyPropertyChanged("Name");
			}
		}

		[Description("DataFormat of the characteristic (how to parse). Ensure that ParseCharacteristicMainDescriptor() runs first or use DataFormatAsync()!")]
		public CharacteristicDataFormats DataFormat {
			get => _dataFormat;
			private set {
				_dataFormat = value;
				NotifyPropertyChanged("Name");
			}
		}

		[Description("Unit of the characteristic data. Ensure that ParseCharacteristicMainDescriptor() runs first or use DataFormatAsync()!")]
		public string Unit {
			get => _unit;
			private set {
				_unit = value;
				NotifyPropertyChanged("Unit");
			}
		}
		public async Task<string> NameAsync() {
			if (_isDescriptorParsed == false) { await ParseCharacteristicMainDescriptor(); }
			return Name;
		}
		public async Task<CharacteristicDataFormats> DataFormatAsync() {
			if (_isDescriptorParsed == false) { await ParseCharacteristicMainDescriptor(); }
			return DataFormat;
		}
		public async Task<string> UnitAsync() {
			if (_isDescriptorParsed == false) { await ParseCharacteristicMainDescriptor(); }
			return Unit;
		}


		/// <summary>
		///     This function is used to get the description of the GattCharacteristic and parse it to the Name, Format, etc
		/// </summary>
		public async Task<GattCommunicationStatus> ParseCharacteristicMainDescriptor() {
			var descriptors = await _gattCharacteristic.GetDescriptorsAsync();
			if (descriptors.Status == GattCommunicationStatus.Success && descriptors.Descriptors.Count >= 1) {
				// Read raw bytes 
				var dsc = descriptors.Descriptors[0];
				var RawValue = await dsc.ReadValueAsync();

				// Only go on if a Vale was retrieved
				//if (!string.IsNullOrWhiteSpace(dsc.Uuid.ToString())) {
				if (RawValue.Status == GattCommunicationStatus.Success) {

					// Convert raw data to byte array
					byte[] data;
					CryptographicBuffer.CopyToByteArray(RawValue.Value, out data);

					// UTF conversion - String from bytes  
					_descriptorString = Encoding.UTF8.GetString(data, 0, data.Length);

					// Split the descriptor in its parts if possible
					//Debug.WriteLine("\tDes: " + _descriptorString + ", " + dsc.Uuid.ToString());
					string[] descriptorParts = _descriptorString.Split(";");
					// A Descriptor with meta data was found!
					if (descriptorParts.Length >= 2) {
						Name = descriptorParts[0];
						DataFormat = CharacteristicDataFormats.VarInt; //Todo: Parse Type from descriptorParts[1]
						Unit = descriptorParts[2];
						MinValue = Convert.ToDouble(descriptorParts[3]); // Untested - might be trouble with . or ,. Also: null needs to be implemented
						MaxValue = Convert.ToDouble(descriptorParts[4]);
					}
					else if (descriptorParts.Length >= 1) {
						// If a well known measurement characteristic was found use it to determine properties, else use whole descriptor as name and assume its an integer
						if (WellKnownCharacteristic != null) {
							Name = WellKnownCharacteristic.Name;
							DataFormat = WellKnownCharacteristic.DataFormat;
							Unit = WellKnownCharacteristic.Unit;
							MinValue = WellKnownCharacteristic.MinValue;
							MaxValue = WellKnownCharacteristic.MaxValue;
						}
						else {
							Name = descriptorParts[0];
							DataFormat = CharacteristicDataFormats.VarInt;
							Unit = "?";
							MinValue = null;
							MaxValue = null;
						}

					}

					// Parse of descriptor is finished!
					_isDescriptorParsed = true;
				}
				else {
					Debug.WriteLine($"\t\t\tGetDescriptorsAsync - Error while retrieving the RawValue of the descriptor {0}", RawValue.Status);
				}
			}
			else {
				Debug.WriteLine($"\t\t\tGetDescriptorsAsync failed {0}", descriptors.Status);
			}

			Debug.WriteLine($"\t\t\ttGetDescriptorsAsync End");
			return descriptors.Status;
		}

		/// <summary>
		///     This function is used to receive and format the current value
		/// </summary>
		public async Task<double> GetFormattedValue() {
			// Read receive data and convert it to a byte array
			var value = await _gattCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
			byte[] data;
			CryptographicBuffer.CopyToByteArray(value.Value, out data);

			// Store size of data-field
			DataSize = data.Length;

			// Convert receive byte array to a double value based on given Format
			double curDoubleValue = 0;
			switch (_dataFormat) {
				case CharacteristicDataFormats.VarInt:
					if (data.Length == 1) {
						curDoubleValue = data[0];
					}
					else if (data.Length == 2) {
						curDoubleValue = BitConverter.ToInt16(data, 0);
					}
					else if (data.Length == 4) {
						curDoubleValue = BitConverter.ToInt32(data, 0);
					}
					else if (data.Length == 8) {
						curDoubleValue = BitConverter.ToInt64(data, 0);
					}
					else { throw new Exception(string.Format($"Data format (integer) and data size did not correspond! (is {0}, should be 1, 2, 4 or 8)", data.Length)); }
					break;
				case CharacteristicDataFormats.VarUInt:
					if (data.Length == 1) {
						curDoubleValue = data[0];
					}
					else if (data.Length == 2) {
						curDoubleValue = BitConverter.ToUInt16(data, 0);
					}
					else if (data.Length == 4) {
						curDoubleValue = BitConverter.ToUInt16(data, 0);
					}
					else if (data.Length == 8) {
						curDoubleValue = BitConverter.ToUInt16(data, 0);
					}
					else { throw new Exception(string.Format($"Data format (integer) and data size did not correspond! (is {0}, should be 1, 2, 4 or 8)", data.Length)); }
					break;
				case CharacteristicDataFormats.VarFloat:
					if (data.Length == 4) {
						curDoubleValue = BitConverter.ToSingle(data, 0);
					}
					else if (data.Length == 8) {
						curDoubleValue = BitConverter.ToDouble(data, 0);
					}
					else { throw new Exception(string.Format($"Data format (floating) and data size did not correspond! (is {0}, should be 4 or 8)", data.Length)); }
					break;
				case CharacteristicDataFormats.Int2B_Point_Int2B:
					if (data.Length == 4) {
						// Get value of main digits and decimals and combine it to double
						short vDigit = BitConverter.ToInt16(data, 0);
						ushort vDecimal = BitConverter.ToUInt16(data, 2);

						curDoubleValue = (double)vDigit + (((double)vDecimal)/1000);
					}
					else { throw new Exception(string.Format($"Data format (Int2B.Int2B) and data size did not correspond! (is {0}, should be 4)", data.Length)); }
					break;
				default:
					break;
			}

			_curValue = curDoubleValue;
			return curDoubleValue;
		}

		/// <summary>
		///     This function is used to convert a value into the specific data format an write it back to the BLE device characteristic
		/// </summary>
		public async Task SetFormattedValue(double value) {
			// Create byte array, same size as BLE data field (NOTE: DataSize is received when GetFormattedValue() is executed - this MUST run first!)
			byte[] data = new byte[DataSize];

			// Convert given value to array of bytes based on given Format
			try {
				switch (DataFormat) {
					case CharacteristicDataFormats.VarInt:
						if (DataSize == 1) {
							var tempData = BitConverter.GetBytes((byte)value);
							if (tempData.Length == data.Length) {
								data = tempData;
							}
							else { throw new Exception(string.Format($"The detected data field size does not correspond with the size of the value!")); }
						}
						else if (DataSize == 2) {
							var tempData = BitConverter.GetBytes((short)value);
							if (tempData.Length == data.Length) {
								data = tempData;
							}
							else { throw new Exception(string.Format($"The detected data field size does not correspond with the size of the value!")); }
						}
						else if (DataSize == 4) {
							var tempData = BitConverter.GetBytes((int)value);
							if (tempData.Length == data.Length) {
								data = tempData;
							}
							else { throw new Exception(string.Format($"The detected data field size does not correspond with the size of the value!")); }
						}
						else if (DataSize == 8) {
							var tempData = BitConverter.GetBytes((long)value);
							if (tempData.Length == data.Length) {
								data = tempData;
							}
							else { throw new Exception(string.Format($"The detected data field size does not correspond with the size of the value!")); }
						}
						else { throw new Exception(string.Format($"The detected data size of this integer is not implemented! (is {0}, can be 1, 2, 4 or 8)", data.Length)); }
						break;
					case CharacteristicDataFormats.VarUInt:
						if (DataSize == 1) {
							var tempData = BitConverter.GetBytes((byte)value);
							if (tempData.Length == data.Length) {
								data = tempData;
							}
							else { throw new Exception(string.Format($"The detected data field size does not correspond with the size of the value!")); }
						}
						else if (DataSize == 2) {
							var tempData = BitConverter.GetBytes((ushort)value);
							if (tempData.Length == data.Length) {
								data = tempData;
							}
							else { throw new Exception(string.Format($"The detected data field size does not correspond with the size of the value!")); }
						}
						else if (DataSize == 4) {
							var tempData = BitConverter.GetBytes((uint)value);
							if (tempData.Length == data.Length) {
								data = tempData;
							}
							else { throw new Exception(string.Format($"The detected data field size does not correspond with the size of the value!")); }
						}
						else if (DataSize == 8) {
							var tempData = BitConverter.GetBytes((ulong)value);
							if (tempData.Length == data.Length) {
								data = tempData;
							}
							else { throw new Exception(string.Format($"The detected data field size does not correspond with the size of the value!")); }
						}
						else { throw new Exception(string.Format($"The detected data size of this integer is not implemented! (is {0}, can be 1, 2, 4 or 8)", data.Length)); }
						break;
					case CharacteristicDataFormats.VarFloat:
						if (DataSize == 4) {
							var tempData = BitConverter.GetBytes((float)value);
							if (tempData.Length == data.Length) {
								data = tempData;
							}
							else { throw new Exception(string.Format($"The detected data field size does not correspond with the size of the value!")); }
						}
						else if (DataSize == 8) {
							var tempData = BitConverter.GetBytes((double)value);
							if (tempData.Length == data.Length) {
								data = tempData;
							}
							else { throw new Exception(string.Format($"The detected data field size does not correspond with the size of the value!")); }
						}
						else { throw new Exception(string.Format($"The detected data size of this floating point is not implemented! (is {0}, should be 4 or 8)", DataSize)); }
						break;
					case CharacteristicDataFormats.Int2B_Point_Int2B:
						if (DataSize == 4) {
							short vDigit = (short)value;
							short vDecimal = (short)((value - vDigit) * 100);

							var tempDataDigit = BitConverter.GetBytes(vDigit);
							var tempDataDecimal = BitConverter.GetBytes(vDecimal);
							if (tempDataDigit.Length + tempDataDecimal.Length == data.Length) {
								data[0] = tempDataDigit[0];
								data[1] = tempDataDigit[1];
								data[2] = tempDataDecimal[0];
								data[3] = tempDataDecimal[1];
							}
							else { throw new Exception(string.Format($"The detected data field size does not correspond with the size of the value!")); }
						}
						else { throw new Exception(string.Format($"The detected data size of this Int2B.Int2B is not implemented! (is {0}, should be 4)", DataSize)); }
						break;
					default:
						break;
				}
			}
			catch (Exception ex) {
				var msg = "Error in SetFormattedValue, Convert given value to byte-array: " + ex.Message;
				Debug.WriteLine(msg);
				throw new Exception(msg);
			}

			// Convert and write data
			if (data != null && data.Length >= 1) {
				var writeBuffer = CryptographicBuffer.CreateFromByteArray(data);
				try {
					// BT_Code: Writes the value from the buffer to the characteristic.
					var result = await _gattCharacteristic.WriteValueWithResultAsync(writeBuffer);

					if (result.Status == GattCommunicationStatus.Success) {
						Debug.WriteLine("Successfully wrote value to device");
					}
					else {
						Debug.WriteLine($"Write failed: {result.Status}");
					}
				}
				catch (Exception ex) when (ex.HResult == BLE_ErrorCodes.E_BLUETOOTH_ATT_INVALID_PDU) {
					var msg = "Error in SetFormattedValue: E_BLUETOOTH_ATT_INVALID_PDU " + ex.Message;
					Debug.WriteLine(msg);
					throw new Exception(msg, ex.InnerException);
				}
				catch (Exception ex) when (ex.HResult == BLE_ErrorCodes.E_BLUETOOTH_ATT_WRITE_NOT_PERMITTED || ex.HResult == BLE_ErrorCodes.E_ACCESSDENIED) {
					// This usually happens when a device reports that it support writing, but it actually doesn't.
					var msg = "Error in SetFormattedValue: E_BLUETOOTH_ATT_WRITE_NOT_PERMITTED " + ex.Message;
					Debug.WriteLine(msg);
					throw new Exception(msg, ex.InnerException);
				}
			}
			else {
				Debug.WriteLine("SetFormattedValue: Data array was null or empty");
			}
		}



		// Implementation of INotifyPropertyChanged
		public event PropertyChangedEventHandler PropertyChanged;
		public void NotifyPropertyChanged([CallerMemberName] string propertyName = "") {
			if (PropertyChanged != null) {
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}
	}




	/// <summary>
	///     This class is used for characteristics whose value shall periodically be received (StoreFormattedNumValue) and stored (in DataPoints).
	///     The name, data format and unit is gathered from the descriptor of the characteristic (base class)
	///     Use AttachNotification() to receive notifications and attach a handler
	///     Use StoreFormattedNumValue() to receive the value, format (DataFormat) and store (DataPoints) it
	/// </summary>
	public class MeasurementCharacteristic : MCI_Characteristic, INotifyPropertyChanged
	{
		// Data points, their acquisition and representation
		private bool _isShownByGraph;
		private ObservableCollection<ObservablePoint> _dataPoints;
		private int _dataPointIndex;   // The index of the most recent data point
		private DateTime _firstDataPointTime; // The time when the measurement was started
		private DateTime _lastDataPointTime; // The time when the last measurement was executed
		private bool _isNotifyEnabled; // Bool if a Notification is registered on this characteristic
		private Windows.Foundation.TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> _valueChangedEventHandler; // The handler used when a notification arrives - used for detach
		private SolidColorBrush _colorBrush;

		public MeasurementCharacteristic(GattCharacteristic SourceChr, int ListIndex, List<WellKnownCharacteristic> wellKnownCharacteristics, SolidColorBrush TextCol_Brush) : base(SourceChr, ListIndex, wellKnownCharacteristics) {
			// Default Values
			_isShownByGraph = false;
			_dataPointIndex = -1;
			_firstDataPointTime = DateTime.Now;
			_lastDataPointTime = DateTime.Now;
			_dataPoints = new ObservableCollection<ObservablePoint>();
			_isNotifyEnabled = false;

			// Colors
			if (TextCol_Brush != null)
				_colorBrush = TextCol_Brush;
			else
				_colorBrush = new SolidColorBrush(Windows.UI.Colors.Black);
		}

		~MeasurementCharacteristic() {
			if (_isNotifyEnabled == true) {
//#pragma warning disable 4014
//				DetachNotification();
//#pragma warning restore 4014
			}
			Debug.WriteLine("MeasurementCharacteristic Destructor called");
		}

		public bool IsShownByGraph { get => _isShownByGraph; set => _isShownByGraph = value; }
		public ObservableCollection<ObservablePoint> DataPoints { get => _dataPoints; }//private set => _dataPoints = value;//NotifyPropertyChanged("DataPoints");
		public int DataPointIndex { get => _dataPointIndex; }//private set => _dataPointIndex = value;//NotifyPropertyChanged("DataPointIndex");
		
		public bool IsNotifyEnabled { get => _isNotifyEnabled; }
		public string LatestValueString {
			get {
				double? val;
				if (_dataPointIndex >= 0)
					val = DataPoints[_dataPointIndex].Y;
				else
					val = CurValue;

				if (val != null) {
					if (DataFormat == CharacteristicDataFormats.VarFloat || DataFormat == CharacteristicDataFormats.Int2B_Point_Int2B) {
						return string.Format("{0:0.0}", val); // DataPoints[_dataPointIndex].Y.ToString();
					}
					else {
						return val.ToString();
					}
				}
				else
					return "-";
			}
		}
		public SolidColorBrush ColorBrush { get => _colorBrush; }




		/// <summary>
		///     This function is used to attach (add) a notification handler to a characteristic
		/// </summary>
		/// <param name="eventHandler">The handler called when a new notification/value arrives </param>
		public async Task<GattCommunicationStatus> AttachNotification(Windows.Foundation.TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> eventHandler) {
			GattCommunicationStatus status = GattCommunicationStatus.Unreachable;
			if (GattCharacteristic != null && _isNotifyEnabled == false) {
				// Try to add an ValueChanged event handler
				bool eventAddSuccess = false;
				try {
					GattCharacteristic.ValueChanged += eventHandler;
					_valueChangedEventHandler = eventHandler;
					eventAddSuccess = true;
				}
				catch { eventAddSuccess = false; }

				// Try to subscribe to notifications of this chr
				try {
					status = await GattCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
				}
				catch (Exception e) {
					// In this application (cypress 20719 uC) this function throws following exception even if everything is fine: "System.Exception: Das Attribut muss vor dem Lesen oder Schreiben zunächst authentifiziert werden. (Exception from HRESULT: 0x80650005)" with RESULT -2140864507
					// Ignore this exception but pass everything else on
					if (e.HResult == -2140864507) { status = GattCommunicationStatus.Success; }
					else { throw e; }
				}

				// Check if everything went fine
				if (status == GattCommunicationStatus.Success && eventAddSuccess == true) {
					_isNotifyEnabled = true;
				}
				else {
					_isNotifyEnabled = false;
					// Try to remove event hander if possible
					try {
						GattCharacteristic.ValueChanged -= eventHandler;
						_valueChangedEventHandler = null;
					}
					catch { }
					throw new Exception("Attaching of notification handler failed!");
				}
			}
			else if (GattCharacteristic != null && _isNotifyEnabled == true) {
				//Debug.WriteLine("Notification is already attached for this GattCharacteristic!");
				throw new Exception("Notification is already attached for this GattCharacteristic!");
			}
			else {
				//Debug.WriteLine("Target characteristic not found!");
				throw new Exception("The attached GattCharacteristic of this object is Null!");
			}
			return status;
		}

		/// <summary>
		///     This function is used to detach (remove) a notification handler to a characteristic
		/// </summary>
		public async Task<GattCommunicationStatus> DetachNotification() {
			GattCommunicationStatus status = GattCommunicationStatus.Unreachable;
			if (GattCharacteristic != null && _isNotifyEnabled == true) {
				// Try to remove an ValueChanged event handler
				bool eventRemoveSuccess = false;
				try {
					GattCharacteristic.ValueChanged -= _valueChangedEventHandler;
					_valueChangedEventHandler = null;
					eventRemoveSuccess = true;
				}
				catch { eventRemoveSuccess = false; }

				// Try to un-subscribe notifications of this chr
				try {
					status = await GattCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);
				}
				catch (Exception e) {
					// In this application (cypress 20719 uC) this function throws following exception even if everything is fine: "System.Exception: Das Attribut muss vor dem Lesen oder Schreiben zunächst authentifiziert werden. (Exception from HRESULT: 0x80650005)" with RESULT -2140864507
					// Ignore this exception but pass everything else on
					if (e.HResult == -2140864507) { status = GattCommunicationStatus.Success; }
					else { throw e; }
				}


				// Check if everything went fine
				if (status == GattCommunicationStatus.Success && eventRemoveSuccess == true) {
					_isNotifyEnabled = false;
				}
				else {
					_isNotifyEnabled = true;
					throw new Exception("Detaching of notification handler failed!");
				}
			}
			else if (GattCharacteristic != null && _isNotifyEnabled == false) {
				throw new Exception("Notification is not attached for this GattCharacteristic!");
			}
			else {
				throw new Exception("The attached GattCharacteristic of this object is Null!");
			}
			return status;
		}

		/// <summary>
		///     This function is used to receive and format the current value as well as automatically store it as data point
		/// </summary>
		public async Task StoreFormattedNumValue(double DataPointsTimeMultiplicator_Sec) {
			// Get current value and add a data point
			double? result = null;
			try {
				result = await GetFormattedValue();
			}
			catch {
				Debug.WriteLine("Unable to get current value! Setting value null");
			}

			// Increase DataPoint Counter
			_dataPointIndex++;

			// If the limits are not null (= auto scaling), check if the current value is below or above limits and change accordingly
			if(MinValue != null && MaxValue != null) {
				// Check if min or max value is exceeded
				if (result < MinValue) {
					MinValue = result * 0.95;
				}
				if (result > MaxValue) {
					MaxValue = (result + 1) * 1.05; // +1 to ensure its always bigger than min value (graph bug)
				}
			}
			else if((MinValue == null || MaxValue == null) && MinValue != MaxValue) {
				throw new Exception("If one (MinValue or MaxValue) in null (auto scaling), both must be null or the graph will run into bugs!");
			}
			
			// Add new DataPoint based on first and last point time
			_lastDataPointTime = _lastDataPointTime.AddSeconds(DataPointsTimeMultiplicator_Sec);
			_dataPoints.Add(new ObservablePoint((_lastDataPointTime - _firstDataPointTime).TotalSeconds, result));

			// Add new DataPoint based on index
			//_dataPoints.Add(new ObservablePoint(_dataPointIndex * DataPointsTimeMultiplicator_Sec, result)); 

			// Notify UI that the values have changed
			NotifyPropertyChanged("LatestValueString");
		}

		/// <summary>
		///     This function is used to reset the data points list and its index back to initial value
		/// </summary>
		public void ResetDataPoints() {
			_dataPointIndex = -1;
			DataPoints.Clear();
		}

		/// <summary>
		///     This method is used to get all GattCharacteristic's of an service and generate a ObservableCollection of MeasurementCharacteristic's from it. Returns null if service was found but there are characteristics.
		///     Passes all exceptions on, but ignores unusable characteristics
		/// </summary>
		public static async Task<ObservableCollection<MeasurementCharacteristic>> GenerateMeasurementCharacteristicsAsync(BleModel BleObject, Guid MeasurementServiceUuid, List<SolidColorBrush> TextColors_Brush, int Colors_index, TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> MeasurementCharacteristic_ValueChanged) {
			Debug.WriteLine("GenerateMeasurementCharacteristicsAsync ---");
			// Initialize return object
			ObservableCollection<MeasurementCharacteristic> MeasurementCharacteristics = new ObservableCollection<MeasurementCharacteristic>();

			// If device is connected, get wanted service (MeasurementServiceUuid)
			IReadOnlyList<GattCharacteristic> characteristics = await BleObject.GetAllCharacteristicsAsync(MeasurementServiceUuid);

			int chrIndex = 0;
			if (characteristics != null) {
				foreach (var charact in characteristics) {
					// Try to use characteristic for MeasurementCharacteristic generation and add to collection - NOTE: if an error occurs at one chr it will be ignored
					try {
						if (charact != null) {
							// Determine color (get next color, and if its out of index start from beginning)
							Colors_index++;
							if (Colors_index >= TextColors_Brush.Count)
								Colors_index = 0;

							// Create a new measurement characteristic (graphable characteristic) and parse descriptor (name, data format, etc)
							MeasurementCharacteristic MeasChr = new MeasurementCharacteristic(charact, chrIndex, MainPage.WellKnownCharacteristics, TextColors_Brush[Colors_index]);
							GattCommunicationStatus statusParse = await MeasChr.ParseCharacteristicMainDescriptor();
							Debug.WriteLine(string.Format("\tAdding Measurement Characteristic {0}: {1}, {2}", MeasChr.ObjIndex, await MeasChr.NameAsync(), MeasChr.GattCharacteristic.Uuid.ToString()));

							// If parse was successful attach value changed notification and add to list
							if (statusParse == GattCommunicationStatus.Success) {
								var statusAttNotify = await MeasChr.AttachNotification(MeasurementCharacteristic_ValueChanged);
								if (statusAttNotify == GattCommunicationStatus.Success) {
									Debug.WriteLine("\t\tNotification of characteristic registered!");

									// Add measurement characteristic to list
									MeasurementCharacteristics.Add(MeasChr);

									// Get value the first time
									await MeasChr.GetFormattedValue();

									// Increment characteristic index (list index)
									chrIndex++;
								}
								else {
									Debug.WriteLine("\t\tNotification registration of characteristic failed!");
								}

							}
						}
						else {
							Debug.WriteLine("\tCharacteristic" + chrIndex.ToString() + " was null!");
						}
					}
					catch (Exception ex) {
						Debug.WriteLine("\tError while handling characteristic '" + chrIndex.ToString() + "': " + ex.Message);
					}
				}
			}

			// If at least one measurement characteristics was found return it
			if (chrIndex <= 0) {
				throw new Exception("\tNo characteristics found'");
			}
			else {
				// Initialization successful
				return MeasurementCharacteristics;
			}
		}

	}


	/// <summary>
	///     This class is used for characteristics whose value shall be configurable at the UI.
	///     The name, data format and unit is gathered from the descriptor of the characteristic (base class)
	/// </summary>
	public class ConfigCharacteristic : MCI_Characteristic, INotifyPropertyChanged
	{
		
		private double _value;

		public double Value {
			get => _value;
			private set {
				_value = value;
			}
		}

		//public string ValueSetter {
		//	get => _value.ToString("0.##");
		//	set {
		//		//double dValue = Double.Parse(value);
		//		//if (dValue >= MinValue && dValue < MaxValue) {
		//			Debug.WriteLine("Setting value to: " + value);
		//		//}
		//	}
		//}

		public string ValueString {
			get => _value.ToString("0.##");
		}

		public ConfigCharacteristic(GattCharacteristic SourceChr, int ListIndex, List<WellKnownCharacteristic> wellKnownCharacteristics) : base(SourceChr, ListIndex, wellKnownCharacteristics) {
			_value = double.NaN;
		}

		~ConfigCharacteristic() {
			Debug.WriteLine("ConfigCharacteristic Destructor called");
		}

		public async Task RefeshValueFromSource(double? compareValue) {
			try {
				double val = await GetFormattedValue();
				Debug.WriteLine("RefeshValueFromSource: " + val.ToString("0.##"));

				// If the currently stored value or the shown/compare value differ -> refresh (compareValue=null acts as "Force")
				if (val != Value || val != compareValue) {
					Value = val;
					NotifyPropertyChanged("Value"); // Does not Work - Todo: Move this to base or implement notify interface here
					NotifyPropertyChanged("ValueString");
				}
			}
			catch (Exception ex) {
				Debug.WriteLine("RefeshValueFromSource failed: " + ex.Message);
			}
			
		}

		public async Task<string> WriteConfigToSource(string value) {
			// Try to parse the textbox
			double dValue;
			try {
				dValue = Double.Parse(value);
			}
			catch (Exception ex) {
				Debug.WriteLine("WriteConfigCharacteristicValue - Invalid input: " + ex.Message);
				return "Invalid number";
			}

			// Check allowed limits and write value if everything is OK
			if (dValue >= MinValue && dValue <= MaxValue) {
				Debug.WriteLine("Setting value to: " + value);
				try {
					await SetFormattedValue(dValue);
				}
				catch (Exception ex) {
					Debug.WriteLine("WriteConfigCharacteristicValue - Write Error: " + ex.Message);
					return "Write back failed";
				}
			}
			else {
				Debug.WriteLine("WriteConfigCharacteristicValue - Value out of limits (Range: " + MinValue.ToString() + " to " + MaxValue.ToString() + ")");
				return "Value out of limits (Range: " + MinValue + "- ...)";
			}

			// Everything went fine! Refresh from source and return null (no Error)
			await RefeshValueFromSource(null);
			return null;
		}

		/// <summary>
		///     This method is used to get all GattCharacteristic's of an service and generate a ObservableCollection of ConfigCharacteristic's from it. Returns null if service was found but there are characteristics.
		///     Passes all exceptions on, but ignores unusable characteristics
		/// </summary>
		public static async Task<ObservableCollection<ConfigCharacteristic>> GenerateConfigCharacteristicsAsync(BleModel BleObject, Guid ConfigServiceUuid, List<WellKnownCharacteristic> WellKnownCharacteristics) {
			Debug.WriteLine("GenerateConfigCharacteristicsAsync ---");
			// Initialize return object
			ObservableCollection<ConfigCharacteristic> ConfigCharacteristics = new ObservableCollection<ConfigCharacteristic>();

			// If device is connected, get wanted service (ConfigServiceUuid)
			IReadOnlyList<GattCharacteristic> characteristics = await BleObject.GetAllCharacteristicsAsync(ConfigServiceUuid);

			int chrIndex = 0;
			if (characteristics != null) {
				foreach (var charact in characteristics) {
					// Try to use characteristics for ConfigCharacteristic generation and add to collection - NOTE: if an error occurs at one chr it will be ignored 
					try {
						if (charact != null) {
							// Create a new config characteristic (characteristic bound to an configuration value) and parse descriptor (name, data format, etc)
							ConfigCharacteristic ConfigChr = new ConfigCharacteristic(charact, chrIndex, WellKnownCharacteristics);
							GattCommunicationStatus statusParse = await ConfigChr.ParseCharacteristicMainDescriptor();
							Debug.WriteLine(string.Format("\tAdding Config Characteristic {0}: {1}, {2}", ConfigChr.ObjIndex, await ConfigChr.NameAsync(), ConfigChr.GattCharacteristic.Uuid.ToString()));

							// If descriptor was successfully parsed add the characteristic to the list of config Characteristics
							if (statusParse == GattCommunicationStatus.Success) {
								// Get initial value
								await ConfigChr.RefeshValueFromSource(null);

								// Add measurement characteristic to list
								ConfigCharacteristics.Add(ConfigChr);

								// Increment characteristic index (list index)
								chrIndex++;
							}
						}
						else {
							Debug.WriteLine("\tCharacteristic" + chrIndex.ToString() + " was null!");
						}
					}
					catch (Exception ex) {
						Debug.WriteLine("\tError while handling characteristic '" + chrIndex.ToString() + "': " + ex.Message);
					}
				}
			}

			// If at least one config characteristics was found return it
			if (chrIndex <= 0) {
				throw new Exception("\tNo characteristics found'");
			}
			else {
				// Initialization successful
				return ConfigCharacteristics;
			}
		}

	}


	public class BleModel : IDisposable
	{
		private BluetoothLEDevice _bluetoothLeDevice = null; // selected device and connection object
		private GattDeviceServicesResult _bluetoothLeServicesResult = null; // services of connected device
		private List<GattDeviceService> _bluetoothLeServices;
		private bool _isConnected;

		public BleModel() {
			_bluetoothLeServices = new List<GattDeviceService>();
			_isConnected = false;
		}

		~BleModel() {
			try {
				ResetBleDevice();
			}
			catch { }

			_bluetoothLeServices = null;
			_bluetoothLeServicesResult = null;
			_bluetoothLeDevice.Dispose();
			_bluetoothLeDevice = null;

		}

		public BluetoothLEDevice BluetoothLeDevice { get => _bluetoothLeDevice;}
		public GattDeviceServicesResult BluetoothLeServicesResult { get => _bluetoothLeServicesResult; }
		public List<GattDeviceService> BluetoothLeServices { get => _bluetoothLeServices; }
		public bool IsConnected { get => _isConnected; }


		/// <summary>
		///     This method is used to connect the class instance with and BLE device.
		///     Must be wrapped in try/catch
		/// </summary>
		public async Task ConnectBleDevice(string SelectedBleDeviceId) {
			Debug.WriteLine("ConnectBleDevice ---");

			// Only connect if not already connected
			if (_isConnected != true) {
				// BT_Code: BluetoothLEDevice.FromIdAsync must be called from a UI thread because it may prompt for consent.
				// NOTE: For this to work the Bluetooth Capabilities must be activated: Solution-Project-Properties(DoubleClick)-Capabilities-Check Bluetooth
				int tryCount = 0;
				do {
					try {
						if (_isConnected != true) {
							_bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(SelectedBleDeviceId);

							if (_bluetoothLeDevice == null) {
								_isConnected = false;

								Debug.WriteLine("\tDevice connection returned null");
								if (tryCount >= 5)
									throw new Exception("Device connection failed: Object was null");

								tryCount++;
								continue;
							}
							else {
								_isConnected = true;
								_bluetoothLeDevice.ConnectionStatusChanged += _bluetoothLeDevice_ConnectionStatusChanged;
							}
						}
					}
					catch (Exception ex) when (ex.HResult == BLE_ErrorCodes.E_DEVICE_NOT_AVAILABLE) {
						// Error
						throw new Exception("Bluetooth radio is not on");
					}
					catch (Exception ex) {
						// Error

						Debug.WriteLine("\tDevice connection failed: " + ex.Message);
						if (tryCount >= 5)
							throw new Exception("Device connection failed: " + ex.Message);

						tryCount++;
						continue;
					}


					// Error
					if (_isConnected == true){ 
						// Note: BluetoothLEDevice.GattServices property will return an empty list for unpaired devices. For all uses we recommend using the GetGattServicesAsync method.
						// BT_Code: GetGattServicesAsync returns a list of all the supported services of the device (even if it's not paired to the system).
						// If the services supported by the device are expected to change during BT usage, subscribe to the GattServicesChanged event.
						_bluetoothLeServicesResult = await _bluetoothLeDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);

						// Success
						if (_bluetoothLeServicesResult.Status == GattCommunicationStatus.Success) {
							foreach (var service in _bluetoothLeServicesResult.Services) {
								// Add service to list of services
								_bluetoothLeServices.Add(service);
							}
							Debug.WriteLine("\tFound " + _bluetoothLeServices.Count.ToString() + " Services!");
							
							// Successfully finished
							break;
						}
						else {
							Debug.WriteLine("\tGet services Failed! Result was: " + _bluetoothLeServicesResult.Status.ToString());
							if (tryCount >= 5)
								throw new Exception("Connect Failed. Service result status: " + _bluetoothLeServicesResult.Status.ToString());

							tryCount++;
							continue;
						}
					}

					Thread.Sleep(1000);
				} while (tryCount <= 5);

			}
			else {
				Debug.WriteLine("\tCan't connect - object is already connected");
			}
		}

		private void _bluetoothLeDevice_ConnectionStatusChanged(BluetoothLEDevice sender, object args) {
			Debug.WriteLine("Device status changed:");
			if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected) {
				Debug.WriteLine("\t Disconnected");
				ResetBleDevice();
			}
		}

		public void ResetBleDevice() {
			// Reset services
			if(_bluetoothLeServices != null)
				_bluetoothLeServices.Clear();
			if (_bluetoothLeServicesResult != null) {
				foreach (var service in _bluetoothLeServices) {
					service?.Dispose();
				}
				_bluetoothLeServicesResult = null;
			}

			// Reset ble connection object
			if (_bluetoothLeDevice != null) {
				// Note: for proper disconnect is may be needed to dispose all open services first
				_bluetoothLeDevice?.Dispose();
				_bluetoothLeDevice = null;
			}

			_isConnected = false;
		}

		/// <summary>
		///     This method is used to get a IReadOnlyList of GattCharacteristic's to be iterated. Returns null if service was found but there are characteristics.
		///     Passes all exceptions on (device not connected, service not found, general).
		/// </summary>
		public async Task<IReadOnlyList<GattCharacteristic>> GetAllCharacteristicsAsync(Guid ServiceUuid) {
			Debug.WriteLine("GetAllCharacteristicsAsync ---");

			// If device is connected, get wanted service (ServiceUuid) and return characteristics
			if (IsConnected == true && _bluetoothLeServicesResult.Status == GattCommunicationStatus.Success) {

				// Get wanted service
				bool serviceFound = false;
				foreach (var service in _bluetoothLeServices) {
					if (service.Uuid == ServiceUuid) {
						serviceFound = true;

						// Ensure we have access to the device.
						var accessStatus = await service.RequestAccessAsync();
						if (accessStatus == DeviceAccessStatus.Allowed) {
							// BT_Code: Get all the child characteristics of a service. Use the cache mode to specify uncached characteristics only 
							// and the new async functions to get the characteristics of unpaired devices as well. 
							var resultC = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
							if (resultC.Status == GattCommunicationStatus.Success) {
								// Successful
								return resultC.Characteristics;
							}
							else {
								// On error, act as if there are no characteristics.
								return null;

								// Note: If no characteristics should result in error use this:
								//throw new Exception("Unable to get characteristics of service " + ServiceUuid.ToString() + " not found");
							}
						}
						else {
							Debug.WriteLine("\tCan't access device");
						}

						// Found service - stop search
						break;
					}
				}

				// Check if service was found and throw error if not
				if(serviceFound == true) {
					throw new Exception("Service (" + ServiceUuid.ToString() + ") not found");
				}
			}
			else {
				throw new Exception("\tDevice is not connected");
			}

			// Error
			return null;
		}


		/// <summary>
		///     This method is used to write a byte array to an characteristic
		///     Passes all exceptions on (device not connected, service not found, characteristic not found, general).
		///     Return 0 if successful and exception at error
		/// </summary>
		public async Task WriteValueAsync(Guid ServiceUuid, Guid Config_Guid, byte[] data) {
			Debug.WriteLine("WriteValueAsync ---");

			// If device is connected, get wanted service (ServiceUuid)
			IReadOnlyList<GattCharacteristic> characteristics = await GetAllCharacteristicsAsync(ServiceUuid);

			if (characteristics != null) {
				foreach (var charact in characteristics) {
					// If right characteristic was found - write value
					if (charact != null && charact.Uuid == Config_Guid) {
						var writeBuffer = CryptographicBuffer.CreateFromByteArray(data);
						// BT_Code: Writes the value from the buffer to the characteristic.
						var result = await charact.WriteValueWithResultAsync(writeBuffer);

						// Check if write was successful
						if (result.Status == GattCommunicationStatus.Success) {
							Debug.WriteLine("Successfully wrote value (" + data.Length.ToString() + "Byte) to device");
							return;
						}
						else {
							Debug.WriteLine($"Write failed: {result.Status}");
							throw new Exception("Write to characteristics failed: " + result.Status.ToString() + " ProtocolError=" + result.ProtocolError.ToString());
						}
					}

				}
			}

			// If this code is reached the characteristic was not found
			throw new Exception("Write failed: Characteristic not found");
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing) {
			if (disposing) {
				// free managed resources
			}
			/// free native resources if there are any.

			// Disconnect and dispose BLE object
			try {
				ResetBleDevice();
			}
			catch { }
		}
	}
}


