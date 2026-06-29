using System;
using System.IO;
using Velopack;

namespace Infinity.Shell.WinUI;

public static class UninstallCleanup
{
    public static void Run(SemanticVersion version)
    {
        try
        {
            string applicationData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Infinity");

            if (Directory.Exists(applicationData))
            {
                Directory.Delete(applicationData, recursive: true);
            }
        }
        catch
        {
        }
    }
}