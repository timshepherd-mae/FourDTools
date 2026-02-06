// Views/CodeDefWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using FourDTools.Services;
using FourDTools.ViewModels;

namespace FourDTools.Views
{
    public partial class CodeDefWindow : Window
    {
        private Editor _ed;
        private readonly CodeDefViewModel _vm;

        public CodeDefWindow(CodeDefViewModel vm)
        {
            // Ensure Murphy theme is merged BEFORE parsing XAML (resources resolve safely)
            TryMergeTheme(Resources);

            InitializeComponent();

            _vm = vm;
            DataContext = vm;

            Loaded += (s, e) =>
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                _ed = doc?.Editor;
                if (_ed != null)
                {
                    _ed.SelectionAdded += OnSelectionAdded; // drawing -> row
                }
                _vm.LoadFromSelection();
            };

            Unloaded += (s, e) => Cleanup();
            Closed += (s, e) => Cleanup();
        }

        private void Cleanup()
        {
            try
            {
                if (_ed != null)
                {
                    _ed.SelectionAdded -= OnSelectionAdded;
                    _ed = null;
                }
            }
            catch { }

            try
            {
                TransientOverlayService.ClearAll();
            }
            catch { }
        }

        // Map drawing selection to row (bi-directional selection)
        private void OnSelectionAdded(object sender, SelectionAddedEventArgs e)
        {
            try
            {
                if (e?.AddedObjects == null || e.AddedObjects.Count == 0) return;
                var addedId = e.AddedObjects[0].ObjectId;

                var idx = -1;
                for (int i = 0; i < _vm.Items.Count; i++)
                {
                    if (_vm.Items[i].Id == addedId) { idx = i; break; }
                }
                if (idx < 0) return;

                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    _vm.CurrentIndex = idx; // triggers zoom + flash + tooltip
                }));
            }
            catch { }
        }

        // Merge theme dictionary with multiple reliable fallbacks (Civil3D-safe)
        private static void TryMergeTheme(ResourceDictionary target)
        {
            foreach (var rd in target.MergedDictionaries)
            {
                if (rd.Source != null && rd.Source.OriginalString.IndexOf("MurphyTheme.xaml", StringComparison.OrdinalIgnoreCase) >= 0)
                    return;
            }

            if (!TryAdd(target, new Uri("/Themes/MurphyTheme.xaml", UriKind.Relative)))
            {
                if (!TryAdd(target, new Uri("pack://application:,,,/Themes/MurphyTheme.xaml", UriKind.Absolute)))
                {
                    // Update assembly name here if changed
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

        // Title bar handlers
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
            Close();
        }
    }
}