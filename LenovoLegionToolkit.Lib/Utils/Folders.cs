using System;
using System.IO;

namespace LenovoLegionToolkit.Lib.Utils;

public static class Folders
{
    public static string Program => AppDomain.CurrentDomain.SetupInformation.ApplicationBase ?? string.Empty;

    public static string AppData
    {
        get
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folderPath = Path.Combine(localAppData, "PonoToolkit");

            // One-shot migration from the v0.3.0-v0.3.5 shared LLT path. Runs only when the
            // Pono Toolkit folder does not exist yet AND the legacy folder does, so upstream
            // Lenovo Legion Toolkit users are untouched on first launch.
            if (!Directory.Exists(folderPath))
            {
                var legacyPath = Path.Combine(localAppData, "LenovoLegionToolkit");
                if (Directory.Exists(legacyPath))
                {
                    try
                    {
                        Directory.CreateDirectory(folderPath);
                        foreach (var srcFile in Directory.EnumerateFiles(legacyPath, "*", SearchOption.TopDirectoryOnly))
                        {
                            var destFile = Path.Combine(folderPath, Path.GetFileName(srcFile));
                            File.Copy(srcFile, destFile, overwrite: false);
                        }
                    }
                    catch
                    {
                        // Migration is best-effort. Fresh settings on failure beat
                        // stomping on the upstream LLT folder.
                    }
                }
            }

            Directory.CreateDirectory(folderPath);
            return folderPath;
        }
    }

    public static string Temp
    {
        get
        {
            var tempBase = Path.GetTempPath();
            var folderPath = Path.Combine(tempBase, "PonoToolkit");
            Directory.CreateDirectory(folderPath);
            return folderPath;
        }
    }
}
