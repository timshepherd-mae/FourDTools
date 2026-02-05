// Commands/CodeDefEditorCommand.cs
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using FourDTools.ViewModels;
using FourDTools.Views;

[assembly: CommandClass(typeof(FourDTools.Commands.CodeDefEditorCommand))]

namespace FourDTools.Commands
{
    public class CodeDefEditorCommand
    {
        [CommandMethod("4DEDITCODEDEF", CommandFlags.Session)]
        public void EditCodeDef()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;

            // Create VM and window
            CodeDefWindow wnd = null;
            var vm = new CodeDefViewModel(() => { if (wnd != null) wnd.Close(); });

            // Use AutoCAD ShowModelessWindow for focus-friendly behavior
            wnd = new CodeDefWindow(vm);
            Application.ShowModelessWindow(wnd);
        }
    }
}