using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string targetApp = Path.Combine(baseDir, "app", "FigmaDesktopApp.exe");

            if (!File.Exists(targetApp))
            {
                MessageBox.Show(
                    "Could not find 'app\\FigmaDesktopApp.exe'.\nPlease extract all files from the zip before running.",
                    "Gemini Studio Launcher",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = targetApp,
                WorkingDirectory = Path.Combine(baseDir, "app"),
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Error launching Gemini Studio: " + ex.Message,
                "Gemini Studio Launcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
