using SQLite;

namespace RosyCrow.Models;

internal static class Constants
{
    public const string InternalScheme = "rosy-crow";
    public const string LogDirectory = "logs";
    public const string CertificateDirectory = "certificates";
    public const string DatabaseName = "app.db";
    public const int MaxRequestAttempts = 6;
    public const SQLiteOpenFlags SQLiteFlags =
        SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache;
    public const string GeminiScheme = "gemini";
    public const string TitanScheme = "titan";
}