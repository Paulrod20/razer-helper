using RazerHelper.UI;
using RazerHelper.UI.Forms;

namespace RazerHelper
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            using var trayPopup = new TrayPopupForm();
            using var trayHost = new TrayIconHost(trayPopup);

            Application.Run();
        }
    }
}