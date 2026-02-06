// ViewModels/CodeDefViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using FourDTools.Models;
using FourDTools.Services;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace FourDTools.ViewModels
{
    public class CodeDefViewModel
    {
        public ObservableCollection<CodeDefItem> Items { get; } = new ObservableCollection<CodeDefItem>();

        public string[] AllowedTypes { get; } = new[]
        {
            "4D_Region", "4D_Area", "4D_Zone", "4D_Package"
        };

        private int _currentIndex;
        public int CurrentIndex
        {
            get => _currentIndex;
            set
            {
                if (Items.Count == 0) { _currentIndex = -1; RaiseCanExecutes(); return; }
                if (value < 0) value = 0;
                if (value >= Items.Count) value = Items.Count - 1;
                _currentIndex = value;

                RaiseCanExecutes();

                if (Current != null)
                    OnNavigateToItem(Current);
            }
        }

        public CodeDefItem Current => (Items.Count > 0 && CurrentIndex >= 0) ? Items[CurrentIndex] : null;

        public ICommand CmdPrev { get; }
        public ICommand CmdNext { get; }
        public ICommand CmdSaveCurrent { get; }
        public ICommand CmdSaveAll { get; }
        public ICommand CmdReloadSelection { get; }
        public ICommand CmdClose { get; }

        private readonly Action _closeAction;

        public CodeDefViewModel(Action closeAction)
        {
            _closeAction = closeAction ?? (() => { });

            CmdPrev = new RelayCommand(() => CurrentIndex--, () => CurrentIndex > 0);
            CmdNext = new RelayCommand(() => CurrentIndex++, () => CurrentIndex < Items.Count - 1);
            CmdSaveCurrent = new RelayCommand(SaveCurrent, () => Current != null && !Current.IsInXref);
            CmdSaveAll = new RelayCommand(SaveAll, () => Items.Any(i => !i.IsInXref));
            CmdReloadSelection = new RelayCommand(LoadFromSelection);
            CmdClose = new RelayCommand(() => _closeAction());
        }

        private void RaiseCanExecutes()
        {
            (CmdPrev as RelayCommand)?.RaiseCanExecuteChanged();
            (CmdNext as RelayCommand)?.RaiseCanExecuteChanged();
            (CmdSaveCurrent as RelayCommand)?.RaiseCanExecuteChanged();
            (CmdSaveAll as RelayCommand)?.RaiseCanExecuteChanged();
        }

        public void LoadFromSelection()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            try
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    // Use pickfirst if present; otherwise prompt (no DXF filter; we'll filter in code)
                    PromptSelectionResult psr = ed.SelectImplied();
                    if (psr.Status != PromptStatus.OK || psr.Value == null || psr.Value.Count == 0)
                    {
                        psr = ed.GetSelection();
                        if (psr.Status != PromptStatus.OK)
                        {
                            ed.WriteMessage("\nNo objects selected.");
                            return;
                        }
                    }

                    Items.Clear();

                    foreach (SelectedObject so in psr.Value)
                    {
                        if (so == null) continue;
                        var ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;

                        // Accept: POLYLINE/LWPOLYLINE/2D/3D + Civil 3D FeatureLine
                        var tn = ent.GetType().Name.ToUpperInvariant();
                        if (tn != "POLYLINE" && tn != "POLYLINE2D" && tn != "POLYLINE3D" && tn != "LWPOLYLINE" && tn != "AECCDBFEATURELINE")
                            continue;

                        bool isInXref = IsEntityInXref(tr, ent);

                        string ct, val;
                        XDataService.TryRead(ent, out ct, out val);

                        var item = new CodeDefItem
                        {
                            Id = ent.ObjectId,
                            Handle = ent.Handle.ToString(),
                            IsInXref = isInXref,
                            CodeType = ct,         // you said step 6a is applied in the model setters
                            CodeValue = val,
                            Location = GetEntityLocation(ent),
                            IsDirty = false
                        };

                        Items.Add(item);
                    }

                    if (Items.Count == 0)
                        ed.WriteMessage("\nNo valid polylines or feature lines were selected.");

                    CurrentIndex = (Items.Count > 0) ? 0 : -1;

                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[4DEDITCODEDEF] LoadFromSelection failed: {ex.Message}");
            }
            finally
            {
                RaiseCanExecutes();
            }
        }

        private void SaveCurrent()
        {
            if (Current == null) return;
            if (Current.IsInXref)
            {
                AcadApp.DocumentManager.MdiActiveDocument.Editor.WriteMessage(
                    "\nSelected entity is inside an XREF — open the XREF DWG to edit its XData.");
                return;
            }

            SaveOne(Current);
        }

        private void SaveAll()
        {
            foreach (var item in Items.Where(i => !i.IsInXref))
                SaveOne(item);
        }

        private void SaveOne(CodeDefItem item)
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            // Validation (case-sensitive CodeTypes per your requirement)
            if (string.IsNullOrWhiteSpace(item.CodeType))
            {
                ed.WriteMessage($"\nItem {item.Handle}: CodeType cannot be empty (choose one of: {string.Join(", ", AllowedTypes)}).");
                return;
            }
            if (!AllowedTypes.Contains(item.CodeType)) // case-sensitive
            {
                ed.WriteMessage($"\nItem {item.Handle}: CodeType '{item.CodeType}' not in allowed list.");
                return;
            }
            if (string.IsNullOrWhiteSpace(item.CodeValue))
            {
                ed.WriteMessage($"\nItem {item.Handle}: CodeValue cannot be empty.");
                return;
            }

            // Canonicalize CodeType to the allowed canonical version (step 6c)
            string canonicalType = AllowedTypes.First(t => t.Equals(item.CodeType));

            try
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var ent = tr.GetObject(item.Id, OpenMode.ForWrite) as Entity;
                    if (ent == null)
                    {
                        ed.WriteMessage($"\nItem {item.Handle}: entity not found.");
                        return;
                    }

                    XDataService.Write(ent, canonicalType, item.CodeValue, tr);
                    item.IsDirty = false;

                    tr.Commit();
                }

                ed.WriteMessage($"\nSaved XData on {item.Handle} (Type='{canonicalType}', Value='{item.CodeValue}').");
            }
            catch (Autodesk.AutoCAD.Runtime.Exception aex)
            {
                ed.WriteMessage($"\nACAD error on {item.Handle}: {aex.ErrorStatus}");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\nError on {item.Handle}: {ex.Message}");
            }
        }

        private static bool IsEntityInXref(Transaction tr, Entity ent)
        {
            try
            {
                var ownerId = ent.OwnerId;
                if (ownerId.IsNull) return false;
                var ownerBtr = tr.GetObject(ownerId, OpenMode.ForRead) as BlockTableRecord;
                return ownerBtr != null && ownerBtr.IsFromExternalReference;
            }
            catch { return false; }
        }

        private static Autodesk.AutoCAD.Geometry.Point3d GetEntityLocation(Entity ent)
        {
            try
            {
                var ge = ent.GeometricExtents;
                return new Autodesk.AutoCAD.Geometry.Point3d(
                    (ge.MinPoint.X + ge.MaxPoint.X) * 0.5,
                    (ge.MinPoint.Y + ge.MaxPoint.Y) * 0.5,
                    (ge.MinPoint.Z + ge.MaxPoint.Z) * 0.5);
            }
            catch { return Autodesk.AutoCAD.Geometry.Point3d.Origin; }
        }

        // --- Stable view zoom (no ZOOM command) ---
        private void ZoomToEntity(ObjectId id)
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            using (doc.LockDocument())
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null) return;

                Extents3d ext;
                try { ext = ent.GeometricExtents; } catch { return; }

                using (var view = ed.GetCurrentView())
                {
                    var viewDir = view.ViewDirection;
                    var wcs2dcs =
                        Autodesk.AutoCAD.Geometry.Matrix3d.Displacement(view.Target - Autodesk.AutoCAD.Geometry.Point3d.Origin) *
                        Autodesk.AutoCAD.Geometry.Matrix3d.PlaneToWorld(viewDir) *
                        Autodesk.AutoCAD.Geometry.Matrix3d.Rotation(-view.ViewTwist, viewDir, view.Target);

                    var dcsExt = new Extents3d(
                        ext.MinPoint.TransformBy(wcs2dcs.Inverse()),
                        ext.MaxPoint.TransformBy(wcs2dcs.Inverse()));

                    var center = new Autodesk.AutoCAD.Geometry.Point2d(
                        (dcsExt.MinPoint.X + dcsExt.MaxPoint.X) * 0.5,
                        (dcsExt.MinPoint.Y + dcsExt.MaxPoint.Y) * 0.5);

                    var width = dcsExt.MaxPoint.X - dcsExt.MinPoint.X;
                    var height = dcsExt.MaxPoint.Y - dcsExt.MinPoint.Y;

                    const double pad = 1.1;
                    width *= pad; height *= pad;

                    double viewRatio = view.Width / view.Height;
                    double extRatio = (height == 0) ? viewRatio : (width / height);

                    if (extRatio > viewRatio)
                    {
                        view.Height = width / viewRatio;
                        view.Width = width;
                    }
                    else
                    {
                        view.Width = height * viewRatio;
                        view.Height = height;
                    }

                    view.CenterPoint = center;
                    ed.SetCurrentView(view);
                }

                tr.Commit();
            }
        }

        // --- Called when CurrentIndex changes ---
        private void OnNavigateToItem(CodeDefItem item)
        {
            if (item == null) return;

            try
            {
                // 1) Zoom first so tooltip lands on-screen
                ZoomToEntity(item.Id);

                // 2) Flash outline (transient)
                TransientOverlayService.FlashOutlineAsync(item.Id, milliseconds: 120);

                // 3) Tooltip with 4D info
                var tip = $"4D_Type: {item.CodeType ?? "(none)"} | 4D_Value: {item.CodeValue ?? "(none)"}";
                TransientOverlayService.ShowTooltipAsync(item.Location, tip, milliseconds: 900, textHeight: 2.5);
            }
            catch
            {
                // Visual sugar only — ignore failures
            }
        }
    }
}