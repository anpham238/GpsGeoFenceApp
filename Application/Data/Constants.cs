using System.IO;
using Microsoft.Maui.Storage;
namespace MauiApp1.Data;

public static class Constants
{
    // Ten file SQLite - dung thong nhat 1 file duy nhat
    public const string DatabaseFilename = "smarttourism.db3";

    // Connection string day du cho Microsoft.Data.Sqlite
    public static string ConnectionString =>
        $"Data Source={Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename)}";

    // Duong dan file (dung de debug/log)
    public static string DatabasePath =>
        Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename);
}