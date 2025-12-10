using System;
using System.Windows.Forms;

namespace GameOfLife;

internal static class ApplicationConfiguration
{
    public static void Initialize()
    {
        ApplicationConfiguration.InitializeHighDpiSettings();
    }

    private static void InitializeHighDpiSettings()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
    }
}
