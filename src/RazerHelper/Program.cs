using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Services;
using RazerHelper.Helpers;
using RazerHelper.UI;
using RazerHelper.UI.Forms;

namespace RazerHelper
{
    internal static class Program
    {
        // Session-local, so each signed-in user can run their own tray app.
        private const string SingleInstanceMutexName = @"Local\RazerHelper.SingleInstance";

        [STAThread]
        static int Main(string[] args)
        {
            // The elevated helper that stops or restores Razer's services. It
            // does its one job and exits, so it must come before the
            // single-instance check (the normal app is already running) and
            // before any window exists.
            if (RazerServiceCommand.TryRun(args, new WindowsServiceControl()) is int exitCode)
                return exitCode;

            // Two instances would compete for the shared HID command channel
            // and overwrite each other's settings file.
            using var singleInstanceMutex = new Mutex(
                initiallyOwned: true,
                SingleInstanceMutexName,
                out var isFirstInstance);

            if (!isFirstInstance)
            {
                AppLog.Info("Another RazerHelper is already running for this user; exiting.");
                return 0;
            }

            AppDiagnostics.LogStart();
            AppDiagnostics.InstallExceptionHandling(ShowError);

            ApplicationConfiguration.Initialize();

            using var trayPopup = new TrayPopupForm();
            using var trayHost = new TrayIconHost(trayPopup);

            Application.Run();

            AppDiagnostics.LogExit();
            return 0;
        }

        private static void ShowError(string message) =>
            MessageBox.Show(message, "RazerHelper", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
