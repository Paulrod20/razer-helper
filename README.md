# razer-helper

A lightweight, open-source replacement for Razer Synapse on Razer Blade laptops.

It lives in the system tray, talks to the laptop's controller directly, and needs no account, no cloud and no background services of its own.

> **Status: v1.0.** Built and tested on a **Razer Blade 16 (2023)** running Windows 11. Other models are not supported yet. This project is not affiliated with Razer.

## What it does today

- **Performance modes:** Balanced, Silent and Custom, with CPU and GPU boost levels in Custom.
- **Power profiles:** separate settings for plugged in and on battery, applied automatically when you plug or unplug. On battery only Balanced is offered, as in Synapse.
- **Battery charge limit:** 60%, 80% or 100% (no limit).
- **Display refresh rate:** 60 Hz, 120 Hz, or Auto, which follows the power source.
- **Fans:** live CPU and GPU fan speed, and **Max** fan speed (both fans flat out). Max is a one-off that needs Custom mode and AC power; **Auto** turns it off, and it clears by itself when you leave Custom.
- **Lighting:** the keyboard backlight (Off, Static green, Spectrum, Wave, Breathing) and the Razer logo on the lid (Off, On, Breathing), each with a brightness slider. Always available, on battery or plugged in.
- **Razer background services:** shows how many are running, and can stop and restore them (see below).
- **Settings** (link at the bottom right): start at login, switch profile automatically when you plug in or unplug, hide the window when you click away, close apps using the dedicated GPU when you unplug (see below), shortcuts to Razer's drivers and support page and to the log folder, and **Reset to defaults**.

**Reset to defaults** (in Settings) puts the app back the way it was the first time you opened it, if something ever seems stuck. After you confirm, it clears your saved settings, turns off Start at login, sets the laptop to Balanced mode with no battery charge limit, and restarts. It does not touch Razer's background services (the record of what they were set to is kept, so Start can still restore them) or anything else on your PC.

## Not yet

Manual fan control, the CPU overclock toggle, other Blade models, and Razer mice, keyboards and headsets.

**Choosing a keyboard color and per-key lighting are not available.** On the Blade 16 the laptop only honors a chosen color in a "driver mode" that also switches off the Fn media keys (volume, screen and keyboard brightness), and razer-helper does not trade those away. In normal mode a static effect always shows Razer green, which is why the option is called **Static green**. Effects the laptop does not run on its own, such as Wheel, are not offered either. The built-in effects listed above are the ones the laptop runs by itself.

## Closing apps that use the dedicated GPU

An app that keeps the dedicated GPU awake drains the battery. There are two ways to deal with that:

- **Free up GPU** (link at the bottom of the window) lists those apps and asks whether to close them. Use it whenever you like, plugged in or not, for example after unplugging an external monitor.
- **Close apps using the dedicated GPU when unplugged** (in Settings, off by default) does the same automatically when you unplug the charger.

It is deliberately cautious:

- **It always asks first**, and the default answer is "Not now". After an unplug, if you plug the charger back in, the question goes away.
- **It never force-closes anything.** It asks each app's main window to close, the same as clicking the X, so an app with unsaved work can still ask you to save.
- **It does nothing while an external display is connected.** On this laptop the external ports are wired to the dedicated GPU, so it stays on regardless of which apps are closed.
- **It leaves alone** Windows itself, graphics drivers, Razer software, this app, other users' processes, background helpers with no window, and terminals, editors and the Claude desktop app.
- **Your own never-close list:** add program names to `NeverCloseApps` in `%LOCALAPPDATA%\RazerHelper\settings.json`, for example `"NeverCloseApps": ["blender", "obs64"]`. Names are the process name without `.exe`.

## Requirements

- Windows 11
- Razer Blade 16 (2023), USB product ID `0x029F`
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)

## Getting started

There are no prebuilt releases yet. To run from source:

```
git clone https://github.com/Paulrod20/razer-helper.git
cd razer-helper/src/RazerHelper
dotnet run
```

To run the tests (they use a fake laptop, so no hardware is needed):

```
cd src/RazerHelper.Tests
dotnet test
```

The app is not code-signed, so Windows will show "Unknown publisher" when it asks for administrator approval.

## Razer Synapse and Razer's background software

Razer's software runs several background services that use memory and can contend with this app for the laptop's controller. razer-helper gives you two ways to deal with that.

**Stop them from the app.** When Razer's software is installed, the bottom of the window shows `Razer Software Running: N` (its services plus its own programs, such as Synapse) with a **Stop** button. Hover the number to see exactly what it counts (services, programs, and whether Razer starts at login) and when it was last checked.

- Stop asks Windows to stop every Razer service and turns off their automatic startup, so they stay off after a restart. Windows asks for administrator approval, but only when there are services left to stop.
- Stop also **turns off Razer's start-at-login entry**, the same as switching it off in Task Manager's Startup tab. Razer's own entry is left in place; only its on/off switch changes.
- Stop **asks Razer's running programs to close**, the same as clicking their X. Nothing is force-closed. Synapse lives in the tray and may ignore the request; quit it from its tray icon.
- **Start** restores each service to exactly the startup type it had before, and puts the login entry's switch back exactly as it was.
- Nothing runs automatically. The app only changes these when you press the button, and it never deletes or uninstalls anything. This app's own start-at-login entry is never touched.

**For the cleanest experience, uninstall Razer Synapse.** Removing it takes away Razer's background services, its startup entry and its helper processes in one step, instead of switching them off. It is optional.

Before you do:

- Synapse is also how Razer **mice, keyboards and headsets** are configured (button remapping, macros, lighting effects, DPI). Those features go with it.
- razer-helper controls the laptop directly and does not use Synapse. It has so far been used with Synapse installed, **not yet with Synapse fully uninstalled**. If you try it, create a Windows restore point first, and please report how it went.
- Leave Razer's drivers alone. They are part of how Windows talks to your keyboard hardware, not background apps.

## How it works

The app sends commands to the laptop's embedded controller over a standard Windows HID interface. The command set was worked out by the community; see the credits below. Writes are checked: the controller echoes what it accepted, and the app treats anything else as a failure.

## Credits

- [razer-ctl](https://github.com/tdakhran/razer-ctl) by tdakhran, and its actively maintained continuation [sqmagellan/razer-ctl](https://github.com/sqmagellan/razer-ctl) (MIT): the documented Blade command set this project builds on.
- [OpenRazer](https://github.com/openrazer/openrazer): the USB protocol reverse-engineering work behind all of the above.
- [G-Helper](https://github.com/seerge/g-helper): inspiration for the approach to power profiles and service handling. No G-Helper code is used.

## Disclaimer

This app changes how your laptop behaves: it writes to the laptop's embedded controller, and it can stop and disable Windows services. It only sends commands that were verified on a Blade 16 (2023), but **you use it at your own risk.**

This software is provided "as is", without warranty of any kind. **The author is not responsible for anything that breaks, stops working, or is lost as a result of using it**, including damage to your laptop, changes to performance, battery or thermal behavior, problems with Razer software or Razer devices, or data loss. This is the same no-warranty and no-liability position the [MIT license](LICENSE) already sets out; this section is a plain-language reminder of it. If that isn't acceptable to you, please don't use the app.

## License

[MIT](LICENSE)
