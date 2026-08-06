using System.Diagnostics;

namespace MiraFolio.Core.Utilities;

public static class FileExplorerHelper
{
    /// <summary>Opens Windows Explorer with the specified file pre-selected.</summary>
    public static void OpenAndSelectFile(string filePath)
    {
        if (!File.Exists(filePath)) return;
        Process.Start("explorer.exe", $"/select,\"{filePath}\"");
    }

    /// <summary>Opens Windows Explorer at the specified folder.</summary>
    public static void OpenFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return;
        Process.Start("explorer.exe", $"\"{folderPath}\"");
    }
}
