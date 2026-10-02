using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Windows.Services.Store;

namespace Baddel.Services;

internal static class StoreService
{
    /// <summary>Shows the Store's rating dialog inside the app; falls back to the Store page.</summary>
    public static async Task RequestRateAndReviewAsync(Window owner)
    {
        if (PackageInfo.IsPackaged)
        {
            try
            {
                StoreContext context = StoreContext.GetDefault();
                WinRT.Interop.InitializeWithWindow.Initialize(context, new WindowInteropHelper(owner).Handle);
                StoreRateAndReviewResult result = await context.RequestRateAndReviewAppAsync();
                if (result.Status != StoreRateAndReviewStatus.Error) return;
            }
            catch (Exception ex)
            {
                Logger.Error("In-app rating failed; opening the Store instead.", ex);
            }
        }
        OpenStorePage();
    }

    public static void OpenStorePage()
    {
        string uri = !string.IsNullOrEmpty(AppInfo.StoreProductId)
            ? $"ms-windows-store://review/?ProductId={AppInfo.StoreProductId}"
            : PackageInfo.IsPackaged ? $"ms-windows-store://review/?PFN={PackageInfo.FamilyName}" : "ms-windows-store://home";
        OpenUri(uri);
    }

    public static void OpenUri(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Error("Could not open " + uri, ex);
        }
    }
}
