using RazerHelper.Helpers;
using RazerHelper.UI.Forms;

namespace RazerHelper.UI;

public sealed class TrayIconHost : IDisposable
{
    private readonly ContextMenuStrip _menu;
    private readonly NotifyIcon _notifyIcon;
    private readonly TrayPopupForm _popup;

    public TrayIconHost(TrayPopupForm popup)
    {
        _popup = popup;

        _menu = new ContextMenuStrip();
        _menu.Items.Add("Open RazerHelper", null, (_, _) => TogglePopup());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = _menu,
            Icon = SystemIcons.Application,
            Text = "RazerHelper",
            Visible = true
        };

        _notifyIcon.MouseClick += OnTrayIconMouseClick;
    }

    public void Dispose()
    {
        _notifyIcon.MouseClick -= OnTrayIconMouseClick;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
    }

    private void OnTrayIconMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            TogglePopup();
    }

    private void TogglePopup()
    {
        if (_popup.Visible)
        {
            _popup.Hide();
            return;
        }

        _popup.Location = TaskbarPlacement.GetPopupLocation(_popup.Size);
        _popup.Show();
        _popup.Activate();
    }

    private void ExitApplication()
    {
        _popup.CloseForApplicationExit();
        Application.ExitThread();
    }
}
