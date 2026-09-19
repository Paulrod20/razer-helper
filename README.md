# razer-helper

A lightweight, open-source replacement for Razer Synapse on Razer Blade laptops.

It lives in the system tray, talks to the laptop's controller directly, and needs no account, no cloud and no background services of its own.

> **Status: early, pre-1.0.** Built and tested on a **Razer Blade 16 (2023)** running Windows 11. Other models are not supported yet. This project is not affiliated with Razer.

## What it does today

- **Performance modes:** Balanced, Silent and Custom, with CPU and GPU boost levels in Custom.
- **Power profiles:** separate settings for plugged in and on battery, applied automatically when you plug or unplug. On battery only Balanced is offered, as in Synapse.
- **Battery charge limit:** 60%, 80% or 100% (no limit).
- **Display refresh rate:** 60 Hz, 120 Hz, or Auto, which follows the power source.
- **Fans:** live CPU and GPU fan speed, and **Max** fan speed (both fans flat out). Max is a one-off that needs Custom mode and AC power; **Auto** turns it off, and it clears by itself when you leave Custom.
- **Razer background services:** shows how many are running, and can stop and restore them (see below).

## Not yet

Manual fan control, keyboard and logo lighting, the CPU overclock toggle, launching at login, other Blade models, and Razer mice, keyboards and headsets.

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

**Stop them from the app.** When Razer's software is installed, the bottom of the window shows `Razer Services Running: N` with a **Stop** button.

- Stop asks Windows to stop every Razer service and turns off their automatic startup, so they stay off after a restart. Windows asks for administrator approval once.
- **Start** restores each service to exactly the startup type it had before.
- Nothing runs automatically. The app only changes services when you press the button, and it never deletes or uninstalls anything.

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
