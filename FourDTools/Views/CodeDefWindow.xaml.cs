// Views/CodeDefWindow.xaml.cs
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using FourDTools.ViewModels;

namespace FourDTools.Views
{
    public partial class CodeDefWindow : Window
    {
        public CodeDefWindow(CodeDefViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;

            Loaded += (s, e) => vm.LoadFromSelection();
        }
    }

    // Simple bool inverter for XAML
    public class InvertBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;
    }
}