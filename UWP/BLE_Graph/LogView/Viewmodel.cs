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

namespace Log
{
	public class Viewmodel {
		private MainPage rootPage = MainPage.Current;

		public ObservableCollection<UserNotification> UserNotifications; // Used to store all user notifications raised


		public Viewmodel()
        {
			UserNotifications = new ObservableCollection<UserNotification>();
		}


	

	}
  
}















































//public class Command : ICommand
//{
//    private readonly Action<object> _command;
//    public event EventHandler CanExecuteChanged;

//    public Command(Action<object> command)
//    {
//        _command = command;
//    }

//    public bool CanExecute(object parameter)
//    {
//        return true;
//    }

//    public void Execute(object parameter)
//    {
//        _command(parameter);
//    }
//}

//public void AddItem() {
//	//var randomValue = _random.Next(1, 10);
//	//lock (Sync) {
//	//	MainPage.DataPoints.Add(
//	//	new ObservablePoint { X = _index++, Y = randomValue });
//	//}
//}

//public void AddItem(ObservableCollection<ObservablePoint> list, ObservablePoint obj) {
//	//var randomValue = _random.Next(1, 10);
//	//TODO: only lock if the series is shown
//	//lock (Sync) {

//	list.Add(obj);
//	//MainPage.DataPoints.Add(
//	//new ObservablePoint { X = _index++, Y = randomValue });
//	//}
//}

//public void RemoveFirstItem() {
//	//if (MainPage.DataPoints.Count == 0) return;
//	//MainPage.DataPoints.RemoveAt(0);
//}

//public void UpdateLastItem() {
//	//var randomValue = _random.Next(1, 10);
//	//MainPage.DataPoints[MainPage.DataPoints.Count - 1].Y = randomValue;
//}

//public void ReplaceRandomItem() {
//	//var randomValue = _random.Next(1, 10);
//	//var randomIndex = _random.Next(0, MainPage.DataPoints.Count - 1);
//	//MainPage.DataPoints[randomIndex] =
//	//    new ObservablePoint { X = MainPage.DataPoints[randomIndex].X, Y = randomValue };
//}

//public void AddSeries() {
//	//  for this sample only 5 series are supported.
//	if (Series.Count == 5) return;

//	Series.Add(
//		new LineSeries<int> {
//			Values = new List<int> { _random.Next(0, 10), _random.Next(0, 10), _random.Next(0, 10) }
//		});
//}

//public void RemoveLastSeries() {
//	if (Series.Count == 1) return;

//	Series.RemoveAt(Series.Count - 1);
//}

// The next commands are only to enable XAML bindings
// they are not used in the WinForms sample
//public ICommand AddItemCommand => new Command(o => AddItem());
//public ICommand RemoveItemCommand => new Command(o => RemoveFirstItem());
//public ICommand UpdateItemCommand => new Command(o => UpdateLastItem());
//public ICommand ReplaceItemCommand => new Command(o => ReplaceRandomItem());
//public ICommand AddSeriesCommand => new Command(o => AddSeries());
//public ICommand RemoveSeriesCommand => new Command(o => RemoveLastSeries());