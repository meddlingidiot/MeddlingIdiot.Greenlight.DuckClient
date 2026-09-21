# Changelog

All notable changes to this project are documented here.

## [Unreleased]

### Added

- First cut: one rubber duck on the desktop, coloured from the Greenlight running on the
  machine. Green while everything passes, yellow while a pull request wants you, red when a
  pipeline is broken, and grey when there is no Greenlight to ask.
- The duck himself, traced off the one on the Meddling Idiot badge rather than drawn from a
  general idea of a rubber duck: heavy ink outline, a head very nearly as big as the body, a
  long flat bill, a wide body sitting low and a short tail flipped up at the back. The
  measurements are kept as measured so they can be checked against the badge again.
- A quack for every change of colour, drawn as a speech balloon rather than played as a sound.
  A desk toy that made an actual noise every time a pipeline changed its mind would be
  uninstalled before lunch, and useless in an open-plan room besides. The balloon pops out of
  the bill, holds, and fades; the duck turns round to face it when there is no room on the
  side he is looking.
- Horns and a burning eye for red. Both grow in over half a second and retract before he fades
  out again, so the changeover stays one movement rather than a duck losing his horns half a
  second after he has gone. The eye socket widens as the fire comes up, which is the whole
  trick of it - a red light lit on a red duck is a red dot on a red duck.
- A nag for red. A broken pipeline that announced itself once at 9:15 and then sat there
  quietly red is a broken pipeline somebody walks past all afternoon, so he asks again every
  fourteen seconds until it is fixed. Switchable from the tray, as is quacking altogether.
- A build pulse. He breathes and bobs while a build is running, spending most of it in the
  halo and the float rather than in his own brightness, because "dimming" is uncomfortably
  close to "going out" and that already means something else here.
- Edit mode: drag him anywhere, drag a corner or roll the wheel to resize him, and keep it
  with the tick or put it back with the cross. Enter, Escape, the arrow keys and a click on
  bare desktop all do what they look like they should. He stays silent for the duration - the
  balloon lands exactly where the two buttons go. The file is written once, when the mode ends.
- A tray menu for everything the running overlay can absorb - size, glow, opacity, which part
  of the screen he may stand on, the disc behind him, whether he quacks at all - each written
  straight back to `duck.json`. Plus a Quack item, which is the only way to see where the
  balloon lands without waiting for somebody to break a pipeline.
- "Start with Windows" in the tray menu, registering the stable shim beside the install rather
  than the versioned copy an update would move.
- One duck per session. A second launch - the one somebody started by hand beside the one
  Windows started - leaves quietly, rather than sitting exactly on top of the first and
  saying everything twice.
