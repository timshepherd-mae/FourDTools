// ViewModels/CodeDefViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
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
                if (value < 0) value = 0;
                if (value >= Items.Count) value = Items.Count - 1;
                _currentIndex = value;
                RaiseCanExecutes();
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
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    // Use current selection; if empty, prompt for polylines
                    PromptSelectionResult psr = ed.SelectImplied();
                    if (psr.Status != PromptStatus.OK || psr.Value.Count == 0)
                    {
                        var tvs = new TypedValue[]
                        {
                            new TypedValue((int)DxfCode.Start, "LWPOLYLINE,POLYLINE,POLYLINE3D")
                        };
                        var sf = new SelectionFilter(tvs);
                        psr = ed.GetSelection(sf);
                        if (psr.Status != PromptStatus.OK)
                        {
                            ed.WriteMessage("\nNo polylines selected.");
                            return;
                        }
                    }

                    Items.Clear();

                    foreach (SelectedObject so in psr.Value)
                    {
                        if (so == null) continue;
                        var ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;

                        bool isInXref = IsEntityInXref(tr, ent);

                        string ct, val;
                        XDataService.TryRead(ent, out ct, out val);

                        var item = new CodeDefItem
                        {
                            Id = ent.ObjectId,
                            Handle = ent.Handle.ToString(),
                            IsInXref = isInXref,
                            CodeType = ct,
                            CodeValue = val,
                            Location = GetEntityLocation(ent),
                            IsDirty = false
                        };

                        Items.Add(item);
                    }

                    if (Items.Count == 0)
                        ed.WriteMessage("\nNo qualifying polylines found in selection.");

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

            if (string.IsNullOrWhiteSpace(item.CodeType) || string.IsNullOrWhiteSpace(item.CodeValue))
            {
                ed.WriteMessage($"\nItem {item.Handle}: CodeType/Value cannot be empty.");
                return;
            }

            // Optional: enforce allowed CodeType list
            if (!AllowedTypes.Contains(item.CodeType))
            {
                ed.WriteMessage($"\nItem {item.Handle}: CodeType '{item.CodeType}' not in allowed list.");
                return;
            }

            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var ent = tr.GetObject(item.Id, OpenMode.ForWrite) as Entity;
                    if (ent == null)
                    {
                        ed.WriteMessage($"\nItem {item.Handle}: entity not found.");
                        return;
                    }

                    XDataService.Write(ent, item.CodeType, item.CodeValue, tr);
                    item.IsDirty = false;

                    tr.Commit();
                }

                ed.WriteMessage($"\nSaved XData on {item.Handle} (Type='{item.CodeType}', Value='{item.CodeValue}').");
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
    }
}