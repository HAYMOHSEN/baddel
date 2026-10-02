using System;
using System.Reflection;

namespace Baddel.Services;

/// <summary>Whether Baddel runs from its Store (MSIX) package or as a plain .exe during development.</summary>
internal static class PackageInfo
{
    private static readonly Lazy<bool> Packaged = new(() =>
    {
        int length = 0;
        return Native.GetCurrentPackageFullName(ref length, null) != Native.APPMODEL_ERROR_NO_PACKAGE;
    });

    public static bool IsPackaged => Packaged.Value;

    public static string FamilyName
    {
        get
        {
            try { return IsPackaged ? Windows.ApplicationModel.Package.Current.Id.FamilyName : string.Empty; }
            catch (Exception) { return string.Empty; }
        }
    }

    public static string DisplayVersion
    {
        get
        {
            try
            {
                if (IsPackaged)
                {
                    Windows.ApplicationModel.PackageVersion v = Windows.ApplicationModel.Package.Current.Id.Version;
                    return $"{v.Major}.{v.Minor}.{v.Build}";
                }
            }
            catch (Exception)
            {
                // fall back to the assembly version
            }
            System.Version? version = Assembly.GetEntryAssembly()?.GetName().Version;
            return version is null ? "1.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }
}
