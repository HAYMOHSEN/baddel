using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;

namespace Baddel.Services;

internal enum StartupState
{
    Disabled,
    Enabled,
    DisabledByUser,
    DisabledByPolicy,
    Unavailable,
}

/// <summary>Start with Windows: a StartupTask in the Store package, a Run key during development.</summary>
internal static class StartupService
{
    public const string StartupArgument = "--startup";
    private const string TaskId = "BaddelStartup";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "Baddel";

    public static bool WasLaunchedAtStartup(string[] args)
    {
        if (args.Any(a => string.Equals(a, StartupArgument, StringComparison.OrdinalIgnoreCase))) return true;
        if (!PackageInfo.IsPackaged) return false;
        try
        {
            return AppInstance.GetActivatedEventArgs()?.Kind == ActivationKind.StartupTask;
        }
        catch (Exception ex)
        {
            Logger.Error("The activation kind could not be read.", ex);
            return false;
        }
    }

    public static async Task<StartupState> GetStateAsync()
    {
        if (PackageInfo.IsPackaged)
        {
            try
            {
                StartupTask task = await StartupTask.GetAsync(TaskId);
                return Map(task.State);
            }
            catch (Exception ex)
            {
                Logger.Error("The startup task could not be read.", ex);
                return StartupState.Unavailable;
            }
        }
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(RunValueName) is string ? StartupState.Enabled : StartupState.Disabled;
    }

    public static async Task<StartupState> SetEnabledAsync(bool enabled)
    {
        if (PackageInfo.IsPackaged)
        {
            try
            {
                StartupTask task = await StartupTask.GetAsync(TaskId);
                if (!enabled)
                {
                    task.Disable();
                    return Map(task.State);
                }
                return Map(await task.RequestEnableAsync());
            }
            catch (Exception ex)
            {
                Logger.Error("The startup task could not be changed.", ex);
                return StartupState.Unavailable;
            }
        }
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (enabled) key.SetValue(RunValueName, $"\"{Environment.ProcessPath}\" {StartupArgument}");
            else key.DeleteValue(RunValueName, throwOnMissingValue: false);
            return enabled ? StartupState.Enabled : StartupState.Disabled;
        }
        catch (Exception ex)
        {
            Logger.Error("The startup entry could not be changed.", ex);
            return StartupState.Unavailable;
        }
    }

    private static StartupState Map(StartupTaskState state) => state switch
    {
        StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy => StartupState.Enabled,
        StartupTaskState.DisabledByUser => StartupState.DisabledByUser,
        StartupTaskState.DisabledByPolicy => StartupState.DisabledByPolicy,
        _ => StartupState.Disabled,
    };
}
