# Changelog

## 1.0.0

The first release. Built and tested on a **Razer Blade 16 (2023)** running Windows 11. Other models are not supported yet.

### What it does

- **Performance modes** (Balanced, Silent, Custom with CPU and GPU boost), with separate profiles for plugged in and on battery that switch automatically when you plug or unplug.
- **Fans:** live CPU and GPU fan speed, and a Max fan speed button.
- **Temperatures:** GPU (from the graphics driver) and CPU (from a sensor in the laptop's controller), read only while the window is open.
- **Battery charge limit** (60%, 80% or 100%) and **display refresh rate** (60 Hz, 120 Hz or Auto, following the power source).
- **Lighting:** keyboard effects and brightness, and the Razer logo on the lid.
- **Razer background software:** shows what is running, and can stop it and turn off its start-at-login entry, then restore everything exactly as it was. Nothing is ever force-closed or uninstalled.
- **Free up GPU:** lists apps keeping the dedicated GPU awake and asks before closing them, on demand or automatically when you unplug (off by default).
- **Open from anywhere:** press **Fn+Del** in any program, even a game, to bring the window to the front. An **Always on top** setting is also available.
- **Readable on high-resolution screens:** the window grows with Windows' display scaling (about 30% larger at 225%), and the size can be set by hand with `WindowScale` in `settings.json`.
- **Settings:** start at login, automatic profile switching, hide when clicking away, and **Reset to defaults**.
- Lives in the tray, needs no account, no cloud, no driver and no background service of its own. It does nothing until you open it.

### Installing

- Requires the **.NET 10 Desktop Runtime**. The installer checks for it and refuses to install until it is present, then points you to the download.
- Installs for the current user only; no administrator prompt. The app asks for administrator approval only when you press Stop on Razer's services.
- The installer and the app are **not code-signed**, so Windows may show a SmartScreen warning ("More info", then "Run anyway") and "Unknown publisher".

### Known limits

- **The CPU die temperature is not shown.** Windows does not expose it without a kernel driver, and RazerHelper installs none. The CPU figure comes from a sensor in the laptop's controller. Compared with MSI Afterburner under load on a Blade 16 (2023) it was very close, but it updates more slowly, so Afterburner's number moves faster.
- **No choice of keyboard color or per-key lighting.** On the Blade 16 the laptop only honors a chosen color in a mode that also turns off the Fn media keys, so RazerHelper offers the built-in effects (including a static Razer green) instead.
- **Games in true exclusive fullscreen** can cover the window, and opening it may make such a game minimize. Borderless and windowed games work.
- **While RazerHelper runs, the Insert key** (Fn+Del on the Blade) opens the window instead of toggling overwrite mode.
- Manual fan curves and the CPU overclock toggle are not included yet.

### Use at your own risk

RazerHelper writes to the laptop's embedded controller and can stop and disable Windows services. It only sends commands that were verified on a Blade 16 (2023), but it is provided as is, without warranty. See the disclaimer in the README.
