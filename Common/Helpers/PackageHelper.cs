using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ResourceAnalyzer.Helpers
{
    /// <summary>
    /// Detects whether the current process is running inside an MSIX package container
    /// (e.g. distributed via Microsoft Store) using official Win32 package query APIs,
    /// and provides helpers for MSIX StartupTask activation and registration.
    /// </summary>
    public static class PackageHelper
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, char[]? packageFullName);

        private const int ERROR_INSUFFICIENT_BUFFER = 122;
        private const int APPMODEL_ERROR_NO_PACKAGE = 15700;

        private static bool? _isPackaged;

        public enum StartupTaskResult
        {
            Success,
            DisabledByUser,
            DisabledByPolicy,
            NotPackaged,
            Failed
        }

        /// <summary>
        /// Returns true if the application has a package identity (running as MSIX / Store app).
        /// Returns false if running as a standalone unpackaged .exe (e.g. from GitHub / WinGet).
        /// </summary>
        public static bool IsStorePackage()
        {
            if (_isPackaged.HasValue) return _isPackaged.Value;

            if (!OperatingSystem.IsWindowsVersionAtLeast(10))
            {
                _isPackaged = false;
                return false;
            }

            try
            {
                uint length = 0;
                int result = GetCurrentPackageFullName(ref length, null);
                _isPackaged = (result == ERROR_INSUFFICIENT_BUFFER);
            }
            catch
            {
                _isPackaged = false;
            }

            return _isPackaged.Value;
        }

        /// <summary>
        /// Detects whether this process was launched automatically by Windows at logon via an MSIX StartupTask.
        /// </summary>
        public static bool IsStartupTaskActivated()
        {
            if (!IsStorePackage()) return false;

            try
            {
                var activatedArgs = Windows.ApplicationModel.AppInstance.GetActivatedEventArgs();
                return activatedArgs?.Kind == Windows.ApplicationModel.Activation.ActivationKind.StartupTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to query MSIX activation kind: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Queries the Windows StartupTask registration state for the current MSIX package.
        /// Returns true if enabled by Windows, false if disabled, or null if unpackaged or error.
        /// </summary>
        public static async Task<bool?> GetPackagedStartupTaskStateAsync()
        {
            if (!IsStorePackage()) return null;

            try
            {
                var tasks = await Windows.ApplicationModel.StartupTask.GetForCurrentPackageAsync();
                var task = tasks?.FirstOrDefault();
                if (task != null)
                {
                    return task.State == Windows.ApplicationModel.StartupTaskState.Enabled ||
                           task.State == Windows.ApplicationModel.StartupTaskState.EnabledByPolicy;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to query MSIX startup task state: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Requests enabling or disables the Windows StartupTask registration for this MSIX package.
        /// </summary>
        public static async Task<StartupTaskResult> SetPackagedStartupTaskStateAsync(bool enable)
        {
            if (!IsStorePackage()) return StartupTaskResult.NotPackaged;

            try
            {
                var tasks = await Windows.ApplicationModel.StartupTask.GetForCurrentPackageAsync();
                var task = tasks?.FirstOrDefault();
                if (task == null) return StartupTaskResult.Failed;

                if (enable)
                {
                    if (task.State == Windows.ApplicationModel.StartupTaskState.Disabled)
                    {
                        var newState = await task.RequestEnableAsync();
                        return newState switch
                        {
                            Windows.ApplicationModel.StartupTaskState.Enabled or
                            Windows.ApplicationModel.StartupTaskState.EnabledByPolicy => StartupTaskResult.Success,
                            Windows.ApplicationModel.StartupTaskState.DisabledByUser => StartupTaskResult.DisabledByUser,
                            Windows.ApplicationModel.StartupTaskState.DisabledByPolicy => StartupTaskResult.DisabledByPolicy,
                            _ => StartupTaskResult.Failed
                        };
                    }
                    else if (task.State == Windows.ApplicationModel.StartupTaskState.DisabledByUser)
                    {
                        return StartupTaskResult.DisabledByUser;
                    }
                    else if (task.State == Windows.ApplicationModel.StartupTaskState.DisabledByPolicy)
                    {
                        return StartupTaskResult.DisabledByPolicy;
                    }

                    return StartupTaskResult.Success;
                }
                else
                {
                    if (task.State == Windows.ApplicationModel.StartupTaskState.Enabled ||
                        task.State == Windows.ApplicationModel.StartupTaskState.EnabledByPolicy)
                    {
                        task.Disable();
                    }

                    return StartupTaskResult.Success;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set MSIX startup task state: {ex.Message}");
                return StartupTaskResult.Failed;
            }
        }
    }
}
