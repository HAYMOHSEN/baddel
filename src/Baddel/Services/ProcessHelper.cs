using System;
using System.Diagnostics;

namespace Baddel.Services;

internal static class ProcessHelper
{
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private const uint TOKEN_QUERY = 0x0008;
    private const int TokenElevation = 20;

    public static string GetName(uint processId)
    {
        try
        {
            using Process process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>True when the process runs as administrator (Windows then blocks our keystrokes).</summary>
    public static bool IsElevated(uint processId)
    {
        IntPtr process = Native.OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (process == IntPtr.Zero) return false;
        try
        {
            if (!Native.OpenProcessToken(process, TOKEN_QUERY, out IntPtr token)) return true;
            try
            {
                return Native.GetTokenInformation(token, TokenElevation, out int elevated, sizeof(int), out _) && elevated != 0;
            }
            finally
            {
                Native.CloseHandle(token);
            }
        }
        finally
        {
            Native.CloseHandle(process);
        }
    }
}
