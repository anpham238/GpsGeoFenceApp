namespace MauiApp1.Data;

/// <summary>
/// Hang so chung cho Data layer.
/// </summary>
public static class Constants
{
    // Ten file SQLite offline cache (tren thiet bi Android)
    public const string DatabaseFilename = "smarttourism_offline.db3";

    // Duong dan day du den file SQLite
    public static string DatabasePath =>
        $"Data Source={Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename)}";
} 