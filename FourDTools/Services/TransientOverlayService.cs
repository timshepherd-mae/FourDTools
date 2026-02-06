// Services/TransientOverlayService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;

namespace FourDTools.Services
{
    /// <summary>
    /// Short-lived transient overlays for visual feedback (flash outline + tooltip).
    /// - Non-blocking (DispatcherTimer)
    /// - Tracks & erases all transients
    /// - Safe for Civil 3D / AutoCAD hosted WPF
    /// </summary>
    public static class TransientOverlayService
    {
        private sealed class TransientItem : IDisposable
        {
            public Drawable Drawable { get; }
            public IntegerCollection SubDrawables { get; } = new IntegerCollection();
            public TransientItem(Drawable d) => Drawable = d;
            public void Dispose() => (Drawable as IDisposable)?.Dispose();
        }

        private static readonly List<TransientItem> _active = new List<TransientItem>();
        // Murphy green: RGB(0,87,63)
        private static readonly Color MurphyGreen = Color.FromRgb(0, 87, 63);

        /// <summary>
        /// Erase and dispose every transient left in the session.
        /// Call this when the window closes.
        /// </summary>
        public static void ClearAll()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var tm = TransientManager.CurrentTransientManager;
            doc.Editor?.UpdateScreen();

            foreach (var it in _active.ToList())
            {
                try { tm.EraseTransient(it.Drawable, it.SubDrawables); } catch { }
                it.Dispose();
                _active.Remove(it);
            }
            doc.Editor?.UpdateScreen();
        }

        /// <summary>
        /// Flash a colored outline of the entity (non-blocking).
        /// </summary>
        public static void FlashOutlineAsync(ObjectId id, int milliseconds = 120, Color colorOverride = null)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                using (doc.LockDocument())
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null) return;

                    var clone = ent.Clone() as Entity;
                    if (clone == null) return;

                    var color = colorOverride ?? MurphyGreen;
                    clone.Color = color;
                    clone.Transparency = new Transparency(80); // 0..255 (higher => more transparent)
                    clone.LineWeight = LineWeight.LineWeight050;

                    var item = new TransientItem(clone);
                    try
                    {
                        var tm = TransientManager.CurrentTransientManager;
                        tm.AddTransient(clone, TransientDrawingMode.DirectShortTerm, 1, item.SubDrawables);
                        doc.Editor.UpdateScreen();

                        var t = new DispatcherTimer(DispatcherPriority.Background)
                        {
                            Interval = TimeSpan.FromMilliseconds(milliseconds)
                        };
                        t.Tick += (s, e) =>
                        {
                            try { tm.EraseTransient(clone, item.SubDrawables); } catch { }
                            item.Dispose();
                            _active.Remove(item);
                            doc.Editor.UpdateScreen();
                            (s as DispatcherTimer)?.Stop();
                        };
                        _active.Add(item);
                        t.Start();
                    }
                    catch
                    {
                        item.Dispose();
                    }

                    tr.Commit();
                }
            });
        }

        /// <summary>
        /// Show a transient tooltip (MText) near the entity location (non-blocking).
        /// </summary>
        public static void ShowTooltipAsync(Point3d location, string text, int milliseconds = 900, double textHeight = 2.5)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                using (doc.LockDocument())
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var mtx = new MText
                    {
                        Location = location,
                        Contents = text ?? "",
                        TextHeight = textHeight,
                        Attachment = AttachmentPoint.MiddleCenter,
                        BackgroundFill = true,
                        BackgroundFillColor = Color.FromRgb(255, 255, 255),
                        Color = Color.FromRgb(30, 30, 30),
                        LineSpacingFactor = 1.0
                    };

                    var item = new TransientItem(mtx);
                    try
                    {
                        var tm = TransientManager.CurrentTransientManager;
                        tm.AddTransient(mtx, TransientDrawingMode.DirectShortTerm, 1, item.SubDrawables);
                        doc.Editor.UpdateScreen();

                        var t = new DispatcherTimer(DispatcherPriority.Background)
                        {
                            Interval = TimeSpan.FromMilliseconds(milliseconds)
                        };
                        t.Tick += (s, e) =>
                        {
                            try { tm.EraseTransient(mtx, item.SubDrawables); } catch { }
                            item.Dispose();
                            _active.Remove(item);
                            doc.Editor.UpdateScreen();
                            (s as DispatcherTimer)?.Stop();
                        };
                        _active.Add(item);
                        t.Start();
                    }
                    catch
                    {
                        item.Dispose();
                    }

                    tr.Commit();
                }
            });
        }
    }
}
