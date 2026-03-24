using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Microsoft.Maui.Storage;
namespace MauiApp1.Data;
public static class DbConstants
{
    // ── Ket noi SQL Server LOCAL ─────────────────────────────────────────
    // Dung khi chay tren may tinh Windows (Debug tren may that)
    private const string LocalConnection =
        @"Server=localhost\SQLEXPRESS01;" +
        "Database=SmartTourismDB;" +
        "Trusted_Connection=True;" +
        "TrustServerCertificate=True;" +
        "Connection Timeout=10;";

    // ── Ket noi tu Android Emulator → may host Windows ──────────────────
    // Android Emulator KHONG the dung "localhost" - phai dung 10.0.2.2
    private const string EmulatorConnection =
        @"Server=10.0.2.2\SQLEXPRESS01;" +
        "Database=SmartTourismDB;" +
        "Trusted_Connection=False;" +
        "User ID=smarttourism_user;" +
        "Password=SmartTour@2025!;" +
        "TrustServerCertificate=True;" +
        "Connection Timeout=10;";

    // ── Tu dong chon theo platform ───────────────────────────────────────
    public static string AzureSqlConnectionString =>
#if ANDROID
        EmulatorConnection;
#else
        LocalConnection;
#endif

    // ── SQLite offline path ───────────────────────────────────────────────
    public static string SqlitePath =>
        Path.Combine(
            FileSystem.AppDataDirectory,
            "smarttourism_offline.db3");
}