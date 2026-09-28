using System;
using System.ComponentModel.Composition;
using System.IO;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace TypescriptBridge.Vsix
{
    // Listens to text view creation events and logs which files are opened.
    // This is used for diagnostics only.
    [Export(typeof(ITextViewCreationListener))]
    [ContentType("json")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class BridgeSchemaProvider : ITextViewCreationListener
    {
        // Path to the diagnostic log file.
        private static readonly string LogPath = Path.Combine(
            Path.GetTempPath(),
            "TypescriptBridge.Vsix.log");

        // Called when a text view is created.
        public void TextViewCreated(ITextView textView)
        {
            try
            {
                var message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] TextViewCreated. ContentType: {textView.TextBuffer.ContentType.TypeName}";
                File.AppendAllText(LogPath, message + Environment.NewLine);
            }
            catch
            {
                // Diagnostics must never break VS.
            }
        }
    }
}
