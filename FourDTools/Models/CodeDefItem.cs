// Models/CodeDefItem.cs
using System.ComponentModel;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace FourDTools.Models
{
    public class CodeDefItem : INotifyPropertyChanged
    {
        public ObjectId Id { get; set; }
        public string Handle { get; set; }
        public bool IsInXref { get; set; }
        public Point3d Location { get; set; } // for user hint, optional

        private string _codeType;
        public string CodeType
        {
            get => _codeType;
            set { if (_codeType != value) { _codeType = value; IsDirty = true; OnPropertyChanged(nameof(CodeType)); } }
        }

        private string _codeValue;
        public string CodeValue
        {
            get => _codeValue;
            set { if (_codeValue != value) { _codeValue = value; IsDirty = true; OnPropertyChanged(nameof(CodeValue)); } }
        }

        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            set { if (_isDirty != value) { _isDirty = value; OnPropertyChanged(nameof(IsDirty)); } }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
