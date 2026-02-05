// XDataService.cs
using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace FourDTools.Services
{
    public static class XDataService
    {
        public const string AppName = "4D_CODEDEF";
        public const string Key_CodeType = "4D_Code_Type";
        public const string Key_CodeValue = "4D_Code_Value";

        private const short XDT_APP = 1001;   // app name
        private const short XDT_STR = 1000;   // string

        public static void EnsureRegAppRegistered(Database db, Transaction tr)
        {
            RegAppTable rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (!rat.Has(AppName))
            {
                rat.UpgradeOpen();
                RegAppTableRecord rec = new RegAppTableRecord { Name = AppName };
                rat.Add(rec);
                tr.AddNewlyCreatedDBObject(rec, true);
            }
        }

        public static bool TryRead(Entity ent, out string codeType, out string codeValue)
        {
            codeType = null;
            codeValue = null;

            try
            {
                ResultBuffer rb = ent.GetXDataForApplication(AppName);
                if (rb == null) return false;

                string lastKey = null;

                foreach (TypedValue tv in rb)
                {
                    if (tv.TypeCode != XDT_STR) continue;
                    string s = Convert.ToString(tv.Value ?? "");
                    if (s.StartsWith("Key=", StringComparison.OrdinalIgnoreCase))
                    {
                        lastKey = s.Substring(4).Trim();
                    }
                    else if (s.StartsWith("Value=", StringComparison.OrdinalIgnoreCase))
                    {
                        string val = s.Substring(6).Trim();
                        if (string.Equals(lastKey, Key_CodeType, StringComparison.OrdinalIgnoreCase))
                            codeType = val;
                        else if (string.Equals(lastKey, Key_CodeValue, StringComparison.OrdinalIgnoreCase))
                            codeValue = val;
                    }
                }

                return !string.IsNullOrWhiteSpace(codeType) && !string.IsNullOrWhiteSpace(codeValue);
            }
            catch { return false; }
        }

        public static void Write(Entity ent, string codeType, string codeValue, Transaction tr)
        {
            if (!ent.IsWriteEnabled) ent.UpgradeOpen();

            Database db = ent.Database;
            EnsureRegAppRegistered(db, tr);

            using (var rb = new ResultBuffer(
                new TypedValue(XDT_APP, AppName),
                new TypedValue(XDT_STR, "Key=" + Key_CodeType),
                new TypedValue(XDT_STR, "Value=" + (codeType ?? "")),
                new TypedValue(XDT_STR, "Key=" + Key_CodeValue),
                new TypedValue(XDT_STR, "Value=" + (codeValue ?? ""))
            ))
            {
                ent.XData = rb;
            }
        }
    }
}