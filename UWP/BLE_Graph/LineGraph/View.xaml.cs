using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
//using ViewModelsSamples.Lines.AutoUpdate;
using Windows.UI.Xaml.Input;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using System.Windows.Input;
using Windows.UI.Xaml;
using BLE_Graph;
using Windows.UI.Xaml.Data;
using System.Diagnostics;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using LiveChartsCore;
using System.Collections.Generic;
using System.ComponentModel;
using System;

namespace LineGraph
{
	public sealed partial class View : UserControl
    {
		private MainPage rootPage = MainPage.Current;
		public Viewmodel ViewModel { get; set; }

		public View()
        {
			this.ViewModel = new Viewmodel();
			this.InitializeComponent();
			this.DataContext = ViewModel;
		}

		public void ToggleConfigPane() {
			Splitter.IsPaneOpen = !Splitter.IsPaneOpen;
		}

		private async void MeasureListView_ItemClick(object sender, ItemClickEventArgs e) {
			// If there are MeasurementCharacteristics clear all and set clicked one
			if (ViewModel.MeasurementCharacteristics != null && ViewModel.MeasurementCharacteristics.Count >= 1) {
				// Remove all Series
				ViewModel.Graph_ClearSeries();

				//// Set clicked MeasurementCharacteristic as shown MeasurementCharacteristic
				await ViewModel.Graph_AddNewSeries((MeasurementCharacteristic)e.ClickedItem);
			}
		}

		private async void TextBox_GotFocus(object sender, RoutedEventArgs e) {
			TextBox textBox = sender as TextBox;
			try { 
				if (textBox.Tag != null && (int)textBox.Tag >= 0 && (int)textBox.Tag < ViewModel.ConfigCharacteristics.Count) {
					ConfigCharacteristic configChr = ViewModel.ConfigCharacteristics[(int)textBox.Tag];
					if (!string.IsNullOrWhiteSpace(textBox.Text)) {
						await configChr.RefeshValueFromSource(Convert.ToDouble(textBox.Text));
					}
					else {
						await configChr.RefeshValueFromSource(null);
					}
				}

				// Get current stack panel, find button and set it visible
				var myStackpanel = (StackPanel)textBox.Parent;
				foreach (object child in myStackpanel.Children) {
					string childname = null;
					if (child is FrameworkElement) {
						childname = (child as FrameworkElement).Name;
						if (childname == "Btn_Apply") {
							var btn = (Button)child;
							btn.Visibility = Visibility.Visible;
							break;
						}
					}
				}
			}
			catch (Exception ex) {
				Debug.WriteLine("TextBox_GotFocus failed: " + ex.Message);
			}
		}

		//ToDo: "ViewModel.ConfigCharacteristics[(int)textBox.Tag];" might fail if the focus changes directly to a disconnect
		private async void TextBox_LostFocus(object sender, RoutedEventArgs e) {
			TextBox textBox = sender as TextBox;
			try {
				// Refresh from source only if the button is not the next focused element (prevent short reset to old value before the new arrives and therefore flickering)
				var FocusedElement = FocusManager.GetFocusedElement();
				if ((FocusedElement as FrameworkElement).Name != "Btn_Apply") {
					if (textBox.Tag != null && (int)textBox.Tag >= 0 && (int)textBox.Tag < ViewModel.ConfigCharacteristics.Count) {
						// Get corresponding ConfigCharacteristic and refresh from source
						ConfigCharacteristic configChr = ViewModel.ConfigCharacteristics[(int)textBox.Tag];
						if (!string.IsNullOrWhiteSpace(textBox.Text)) {
							await configChr.RefeshValueFromSource(Convert.ToDouble(textBox.Text));
						}
						else {
							await configChr.RefeshValueFromSource(null);
						}
					}
				}

				// Get current stack panel, find button and set it invisible
				var myStackpanel = (StackPanel)textBox.Parent;
				foreach (object child in myStackpanel.Children) {
					string childname = null;
					if (child is FrameworkElement) {
						childname = (child as FrameworkElement).Name;
						if (childname == "Btn_Apply") {
							var btn = (Button)child;
							btn.Visibility = Visibility.Collapsed;
							break;
						}
					}
				}
			}
			catch (Exception ex) {
				Debug.WriteLine("TextBox_LostFocus failed: " + ex.Message);
			}
		}

		private async void Tbx_Value_KeyDown(object sender, KeyRoutedEventArgs e) {
			if (e.Key == Windows.System.VirtualKey.Enter) {
				// Get textbox object
				TextBox textBox = sender as TextBox;

				// Get the corresponding ConfigCharacteristic object (The ObjIdx of the ConfigCharacteristic is bound to the Tag of th textbox)
				ConfigCharacteristic ConfigChr = ViewModel.ConfigCharacteristics[(int)textBox.Tag];

				// Try to parse and write back value
				await SetConfigCharacteristic(ConfigChr, textBox.Text);

				// Write value back to source (actual change of the value). Needed if UpdateSourceTrigger=Explicit
				//BindingExpression be = textBox.GetBindingExpression(TextBox.TextProperty);
				//if (be != null) {
				//	//string di = (string)be.DataItem;
				//	be.UpdateSource();
				//}
			}
		}

		private async void Btn_Apply_Click(object sender, RoutedEventArgs e) {
			var btn = (Button)sender;
			var myStackpanel = (StackPanel)btn.Parent;

			// Get corresponding textbox, its value/source (via Tag) and set config accordingly
			foreach (object child in myStackpanel.Children) {
				string childname = null;
				if (child is FrameworkElement) {
					childname = (child as FrameworkElement).Name;
					if (childname == "Tbx_Value") {
						var textBox = (TextBox)child;
						// Get the corresponding ConfigCharacteristic object (The ObjIdx of the ConfigCharacteristic is bound to the Tag of th textbox)
						ConfigCharacteristic ConfigChr = ViewModel.ConfigCharacteristics[(int)textBox.Tag];

						// Try to parse and write back value
						await SetConfigCharacteristic(ConfigChr, textBox.Text);

						// We did what we had to do - stop
						break;
					}
				}
			}
		}

		private async Task SetConfigCharacteristic(ConfigCharacteristic ConfigChr, string ValueString) {
			// Try to parse and write back value
			string result = await ConfigChr.WriteConfigToSource(ValueString);

			// Signal user if anything went wrong
			if (result != null) {
				rootPage.NotifyUser("Set Configuration: " + result, NotifyType.ErrorMessage);
			}
		}

		#region MASS CALIBRATION
		private void CalibrationCheckBox_Click(object sender, RoutedEventArgs e) {
			if (CalibrationCheckBox.IsChecked == false)
				CalibrationPanel.Visibility = Visibility.Collapsed;
			else
				CalibrationPanel.Visibility = Visibility.Visible;
		}

		private void CalibrationStartButton_Click(object sender, RoutedEventArgs e) {
			// Not implemented yet
		}

		private void CalibrationResetButton_Click(object sender, RoutedEventArgs e) {
			// Not implemented yet
		}
		#endregion
	}


}
