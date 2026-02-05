// Views/CodeDefWindow.xaml.cs
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using FourDTools.ViewModels;

namespace FourDTools.Views
{
    public partial class CodeDefWindow : Window
    {
        public CodeDefWindow(CodeDefViewModel vm)
        {

            // ---- Ensure theme is merged BEFORE XAML is parsed ----
            TryMergeTheme(Resources); // window-local scope (safest in Civil 3D)


            InitializeComponent();
            DataContext = vm;

            Loaded += (s, e) => vm.LoadFromSelection();
        }


        private static void TryMergeTheme(ResourceDictionary target)
        {
            // Avoid duplicate merges if the window is recreated
            foreach (var rd in target.MergedDictionaries)
            {
                if (rd.Source != null && rd.Source.OriginalString.IndexOf("MurphyTheme.xaml", StringComparison.OrdinalIgnoreCase) >= 0)
                    return;
            }

            // Try the simplest same-assembly relative URI first
            if (!TryAdd(target, new Uri("/Themes/MurphyTheme.xaml", UriKind.Relative)))
            {
                // Fallback 1: explicit component URI without assembly name
                if (!TryAdd(target, new Uri("pack://application:,,,/Themes/MurphyTheme.xaml", UriKind.Absolute)))
                {
                    // Fallback 2: include assembly name (update if AssemblyName ever changes)
                    TryAdd(target, new Uri("pack://application:,,,/FourDTools;component/Themes/MurphyTheme.xaml", UriKind.Absolute));
                }
            }
        }

        private static bool TryAdd(ResourceDictionary target, Uri uri)
        {
            try
            {
                var dict = new ResourceDictionary { Source = uri };
                target.MergedDictionaries.Add(dict);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void MinButton_OnClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

    }

}