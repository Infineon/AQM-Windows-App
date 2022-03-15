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

namespace Log
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

	}


}
