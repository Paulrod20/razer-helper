# Mini-LED panel mode on the Blade 16 (2023)

The dual-mode panel has two native modes: 3840x2400 at 120 Hz (UHD+) and
1920x1200 at 240 Hz (FHD+). Selecting a different desktop resolution does not
switch the panel's native mode. The firmware selection takes effect after a
Windows restart. No Synapse installation or additional driver is needed.

RazerHelper offers the panel controls only for USB PID `029F` when the firmware's
SKU capability bit confirms Mini-LED support. Other models are intentionally
excluded until their protocol and hardware have been checked.

The highlighted button is the firmware selection, which can be waiting for a
restart. The ordinary Display status continues to show what Windows is currently
using. Selecting a panel mode is a manual action, never a power-profile setting.
It is not written at startup or reset by Reset to defaults. After confirming a
saved selection, the app asks whether to restart Windows now, with No selected
by default. Declining keeps the firmware selection saved for a later restart;
the Restart Windows link remains available. The app does not force-close programs.

## Protocol evidence

Inspected on 2026-10-20, without installing or running Synapse:

- [Razer's dual-mode display instructions](https://mysupport.razer.com/app/answers/detail/a_id/9671/~/how-to-switch-between-razer-dual-mode-displays)
  document the BIOS setting under Chipset > Max Refresh Rate Mode.
- [Product 671 manifest](https://apps.razer.com/synapse/products/671/mw/manifest.json):
  Blade 16, build `2610090414`, revision
  `dd891ee27b3290778f962f3f760eed2d25dbdca6`.
- [Vendor hardware protocol module](https://apps.razer.com/synapse/products/671/mw/rzDevice25.5e330aa9fdf6f29eec62.js):
  modules `94735` and `13546` define the commands and their request/response
  layouts. SHA-256:
  `41E7758114AD3605B5B3EE0E65E79AF3055124F775904902BA3285F710AF55C9`.
- [Vendor Blade wrapper](https://apps.razer.com/synapse/products/671/mw/rzDevice25LedMatrixBlade.44210ae56dcdfbe53eb0.js)
  exposes `getMiniLEDPanelResolution`, `setMiniLEDPanelResolution`, and
  `getSKUHardwareConfiguration` using that protocol module.
- [Vendor model middleware](https://apps.razer.com/synapse/products/671/mw/6737.e123a2b3f0516116025b.js)
  defines UHD = 0, FHD = 1 and calls the setter followed by `restartSystem`.

Only protocol facts are used. Vendor scripts and libraries are not included in
the application or loaded by it.

| Command | Request arguments | Reply arguments |
| --- | --- | --- |
| `0x0D8F` Get SKU hardware configuration | `[0]` | byte 0: bit `0x02` indicates Mini-LED support |
| `0x0D8E` Get Mini-LED panel resolution | `[0]` | byte 0: 0 = UHD+, 1 = FHD+ |
| `0x0D0E` Set Mini-LED panel resolution | `[mode]` | success status; setter does not promise an argument echo |

The service checks support again before writing, rejects unknown mode values,
skips writes when the selection already matches, and confirms writes using the
getter. A readback failure is reported as uncertain; it does not trigger restart.

## Physical validation and remaining check

Machine: Blade 16 RZ09-0483, BIOS 2.02, panel `BOE0B90`.

- BIOS switching was physically confirmed: Windows changed from 3840x2400 at
  120 Hz to 1920x1200 at 240 Hz, with the same active panel ID and changed EDID.
- The documented read commands were sent using the app's existing HID transport:
  `0x0D8F` returned `03` (DDS and Mini-LED); `0x0D8E` returned `01` (FHD+),
  agreeing with the BIOS choice and Windows display mode.
- The new `MiniLedPanelService.GetSelectedModeAsync` was also run against the
  physical laptop and correctly returned `Fhd240` without sending a mode write.
- The new setter and reboot flow still need a physical round-trip test through
  the app. Automated tests cover capability gating, packet bytes, readback
  confirmation, no-op selection, malformed replies and unsupported firmware.

To finish physical validation, select UHD+ in RazerHelper, check that the saved
selection is confirmed, save other work, and accept the restart prompt (or use
the Restart Windows link later). Confirm
3840x2400 at 120 Hz after boot, then repeat for FHD+ / 240 Hz. Check that the
ordinary refresh-rate buttons and Auto use the rates exposed after each reboot.
