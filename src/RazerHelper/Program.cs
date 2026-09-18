using RazerHelper.UI;
using RazerHelper.UI.Forms;

namespace RazerHelper
{
    internal static class Program
    {
        // Session-local, so each signed-in user can run their own tray app.
        private const string SingleInstanceMutexName = @"Local\RazerHelper.SingleInstance";

        [STAThread]
        static void Main()
        {
            // Two instances would compete for the shared HID command channel
            // and overwrite each other's settings file.
            using var singleInstanceMutex = new Mutex(
                initiallyOwned: true,
                SingleInstanceMutexName,
                out var isFirstInstance);

            if (!isFirstInstance)
                return;

            ApplicationConfiguration.Initialize();

            using var trayPopup = new TrayPopupForm();
            using var trayHost = new TrayIconHost(trayPopup);

            Application.Run();
        }
    }
}