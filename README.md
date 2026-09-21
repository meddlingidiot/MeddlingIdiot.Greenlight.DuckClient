# MeddlingIdiot.Greenlight.DuckClient

The rubber duck off the Meddling Idiot badge, sitting on your desktop and quacking when the
build changes its mind.

A runnable reference consumer of the [Greenlight](https://github.com/meddlingidiot/MeddlingIdiot.Greenlight)
SDK, and a demonstration of how little an app needs to do to use it. He is **green** while
everything passes and **yellow** while a pull request wants you. When a pipeline breaks he goes
**red**, grows a pair of horns and his eye lights up like a coal — and every time any of that
changes he says so, in a speech balloon rather than out loud. A red one goes on asking every
fourteen seconds until somebody fixes it. While a build is running he breathes and bobs. With
no Greenlight on the machine at all he goes grey and says nothing — a green duck on a
four-minute-old snapshot would be lying to you.

The horns grow rather than appear, and retract before he fades out again. Horns that were
simply there on the first red frame read as a second duck having been swapped in; horns that
push out of his skull over half a second read as him having taken it badly.

The balloon is drawn, not played. A desk toy that made an actual noise every time a pipeline
changed its mind would be uninstalled before lunch, and it would be useless in the one room
where this is most wanted, which is an open-plan one.

The point of it is what it does **not** have. No Azure DevOps client, no GitHub client, no
token, no polling loop — everything he knows arrives through the SDK, from the Greenlight
already running on the machine. Strip out the drawing and the tray icon and the integration is
about twenty lines, all of them in [`App.cs`](Greenlight.DuckClient/App.cs).

## Running it

```bash
dotnet run --project Greenlight.DuckClient
```

Windows only: the click-through overlay and the work-area maths are Win32. The SDK itself is
not — it is plain .NET, and the same twenty lines work anywhere.

## Moving him about

The duck ignores the mouse the rest of the time — a window you cannot click is a window that
never steals your caret — so moving him is a mode you turn on from the tray: **Move and resize
him…**. While it is on, the overlay answers the mouse and the widget grows a dashed frame,
four corner handles and two buttons. He stays quiet for the duration, because the balloon
lands exactly where those buttons go:

- **Drag the middle** to carry him anywhere on the desktop. He is clamped so the whole of him
  stays on screen; the arrow keys nudge him a pixel at a time, Shift ten.
- **Drag a corner** to resize. The opposite corner stays where it is and the box stays square.
  The wheel over him does the same thing about his middle.
- **✓** keeps the arrangement and writes it to the file. So do Enter, a click on bare desktop,
  and turning the mode off from the tray.
- **✕** puts him back exactly where he was when you turned the mode on. So does Escape.

Nothing is written to disk until the mode ends, so dragging him across the desk costs one
write rather than a few hundred.

## The tray

Everything else lives on the mascot in the notification area:

- **Duck on the desktop** — take him away and bring him back. Clicking the icon does the same.
- **Move and resize him…** — the mode above.
- **How big**, **How much glow**, **How solid** — the ordinary sizes, and how much light he
  throws onto the wallpaper.
- **Where he may stand** — the work area, or the whole screen including the taskbar.
- **Dark disc behind him** — the disc he floats on. On by default: a yellow duck laid straight
  over a photograph is unreadable, and the disc is what stops the wallpaper deciding whether
  the build is passing.
- **Leave him grey when Greenlight is away** — a grey duck rather than nothing at all.
- **Let him quack** — off leaves the colour and the pulse, which is the whole of the
  information; the balloon is the part that catches the eye.
- **Keep quacking while it is broken** — the nag. On by default, and the only nagging this toy
  does: a broken pipeline that announced itself once at 9:15 and then sat there quietly red is
  a broken pipeline somebody walks past all afternoon.
- **Start with Windows** — read from the registry every time it is shown, so it agrees with
  Task Manager's Startup tab rather than with what we last wrote there.
- **Quack** — makes him say his piece now. The only way to find out where the balloon lands
  without waiting for somebody to break a pipeline.
- **Edit the colours…** — opens `duck.json`. **Reload the file** picks up hand edits without a
  restart.

Every setting is written straight back to the file, so the menu and the JSON are never two
different sets of settings.

## The file

`%AppData%\Greenlight.Duck\duck.json`, written with the defaults on first run. Each state gets
the body, the bill, the light he throws and the horns — because a glow is not simply the body
at a lower alpha, and the bill is what keeps a green blob reading as a rubber duck rather than
as a green blob:

```json
{
  "Duck": { "AnchorX": 0.9, "AnchorY": 0.16, "Size": 0.2 },
  "WhenGreen": { "Body": "#5CFF8A", "Bill": "#F08A1C", "Glow": "#17C24B" },
  "WhenAmber": { "Body": "#FFD23A", "Bill": "#F08A1C", "Glow": "#E39B10" },
  "WhenRed":   { "Body": "#FF5A48", "Bill": "#F08A1C", "Glow": "#B01208", "Horn": "#8E1A10" }
}
```

Only red grows any, so `Horn` is only ever read from `WhenRed` — it sits on the others because
they are all the same shape of thing, not because a green duck has horns you could recolour.
His eye is deliberately not in here: it is fire, and fire has its own colours.

A file that cannot be parsed falls back to the defaults rather than refusing to start.

## How it is put together

| | |
|---|---|
| [`App.cs`](Greenlight.DuckClient/App.cs) | The whole Greenlight integration, and what to do when edit mode ends |
| [`DuckScene.cs`](Greenlight.DuckClient/DuckScene.cs) | Where he is, what he is saying, how long the balloon has left, what the mouse is over. No Avalonia, so it is testable |
| [`DuckCanvas.cs`](Greenlight.DuckClient/DuckCanvas.cs) | The drawing: the duck, the balloon, and the edit chrome |
| [`DuckWindow.cs`](Greenlight.DuckClient/DuckWindow.cs) | The click-through overlay, and the mouse and keyboard in edit mode |
| [`DuckTray.cs`](Greenlight.DuckClient/DuckTray.cs) | The tray icon and its menu |
| [`ClickThroughNative.cs`](Greenlight.DuckClient/ClickThroughNative.cs) | The four window styles that make it furniture, and the one edit mode takes back |

The duck is traced off the one on the Meddling Idiot badge, in units of his own radius, and the
numbers in [`DuckCanvas.cs`](Greenlight.DuckClient/DuckCanvas.cs) are the measurements rather
than a tidied version of them, so they can be checked against the badge again. Three things
carry the likeness and none of them is the colour: the heavy ink outline, a head very nearly as
big as the body, and a long flat bill. Drawn to the usual cartoon proportions instead — small
head, round body, little bill, no outline — you get a chick.

He is one closed path rather than a head circle overlapping a body ellipse. That is the obvious
way to draw a rubber duck and the wrong one here: the widget is drawn through a fade, and two
overlapping shapes of the same colour are one shape only while the opacity is 1. At anything
less the overlap is denser than the rest of him and the join shows as a seam across his neck.

The eye socket widens as the fire comes up, and that is the whole trick of the glowing eye: a
red light lit on a red duck is a red dot on a red duck, which is to say nothing at all. What
reads as glowing is the dark it is glowing in.

The scene is deliberately free of Avalonia so the quack timing, the clamping, the balloon's
placement and the edit-mode geometry can be tested without a window — a save button that sits
somewhere other than where it was drawn is not something anybody would catch by looking at a
screenshot.

```bash
dotnet test
```

## Licence

The code is MIT — see [LICENSE](LICENSE).

**The logo is not.** The MeddlingIdiot mascot icon in
[`Greenlight.DuckClient/Assets`](Greenlight.DuckClient/Assets) is all rights reserved: it is
not under the MIT License, and it is not sharable or reusable in forks or anything else. Fork
the code, bring your own icon. See [TRADEMARKS.md](TRADEMARKS.md).
