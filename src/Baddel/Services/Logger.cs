using System;
using System.IO;
using System.Text;

namespace Baddel.Services;

/// <summary>Small local error log. It never records the text the user fixes.</summary>
internal static class Logger
{
    private const long MaxBytes = 512 * 1024;
    private static readonly object Gate = new();

    public static string Folder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Baddel");

    private static string FilePath => Path.Combine(Folder, "baddel.log");

    public static void Info(string message) => Write("INFO ", message, null);

    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Folder);
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > MaxBytes)
                {
                    File.Copy(FilePath, FilePath + ".old", overwrite: true);
                    File.Delete(FilePath);
                }
                var line = new StringBuilder()
                    .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                    .Append(' ').Append(level).Append(' ').Append(message);
                if (exception is not null) line.AppendLine().Append(exception);
                line.AppendLine();
                File.AppendAllText(FilePath, line.ToString(), Encoding.UTF8);
            }
        }
        catch (Exception)
        {
            // Logging must never break the app.
        }
    }
}
