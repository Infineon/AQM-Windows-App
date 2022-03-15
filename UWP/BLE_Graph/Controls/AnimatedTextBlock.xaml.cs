using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace BLE_Graph.Views.Controls {

public sealed partial class AnimatedTextBlock : UserControl {

    public AnimatedTextBlock() {
        this.InitializeComponent();
    }

    public string Text {
        get { return (string)GetValue(TextProperty); }
        set { SetValue(TextProperty, value); }
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string),
            typeof(AnimatedTextBlock),
            new PropertyMetadata(null, OnTextPropertyChanged));

    private static void OnTextPropertyChanged(DependencyObject d,
                            DependencyPropertyChangedEventArgs e) {
        var source = d as AnimatedTextBlock;
        if (source != null) {
            var value = (string)e.NewValue;
            if (e.OldValue != null) {
                source.textFrame.Value = value;
                source.Storyboard1.Begin();
            } else {
                source.innerTextBlock.Text = value;
            }
        }
    }

    public TextWrapping TextWrapping {
        get { return (TextWrapping)GetValue(TextWrappingProperty); }
        set { SetValue(TextWrappingProperty, value); }
    }

    public static readonly DependencyProperty TextWrappingProperty =
        DependencyProperty.Register(nameof(TextWrapping), typeof(TextWrapping),
            typeof(AnimatedTextBlock), new PropertyMetadata(TextWrapping.NoWrap));

}
}
