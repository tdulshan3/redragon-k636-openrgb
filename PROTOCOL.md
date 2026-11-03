# Redragon K636 Kitava (258a:0049) lighting protocol

This was decoded from USBPcap captures of the official Redragon app and verified on a K636 from Linux. The captures are attached to [OpenRGB issue #5588](https://gitlab.com/CalcProgrammer1/OpenRGB/-/issues/5588).

- Every packet sent during testing had the same shape as one the app sends. The one exception is a palette read, marked below.
- Config writes were always read-modify-write.

Several Redragon K6** keyboards share this USB ID, and so do Sinowealth keyboards whose protocol can brick them. The app's startup probe tells them apart: it gets a model byte back (K636 = `5D`, K628 Pro = `53`). Check it before sending anything else.

## Identification

- `258a:0049`, bcdDevice 1.01. Lighting is on **interface 1**, usage page `0xFF00`. Its four vendor collections all have usage `0x01`.
- Feature reports on interface 1 (sizes include the report ID):
  - 5: 6 bytes, commands;
  - 6: 1032 bytes, data blocks;
  - 8: 382 bytes, live frames.
- **Model probe**, sent by the app at launch: SET 5 `05 05 81 00 00 00`, then GET 5 with **wLength 8**. The K636 replies `05 01 02 00 00 00 01 5D`.
  - The last byte is the model: K636 = `5D`, K628 Pro = `53` (capture in #4352).
  - PID 0049 is shared, and the F11 detector was disabled after bricking K618/K621. A detector should continue only on a known model byte.

## Config block (address B6)

- Read: SET 5 `05 83 B6 00 00 00`, then GET 6 (1032). The reply is 262 bytes starting `06 83 B6`.
- Write: SET 6 with the same bytes, byte 1 changed to `03`, zero-padded to 1032.
- **Only ever read-modify-write.** The F11 driver's fixed image has `00 10` where the K636 has the `5A A5` marker at 0x4E, with every later byte shifted by 2. That is the most likely cause of the bricking.

| Offset (incl. report ID) | Meaning |
|---|---|
| 0x0E, 0x4E, 0x89 | `5A A5` markers |
| 0x14 | `01` in custom per-key mode, otherwise `00` |
| 0x15 | Mode: `00` Direct (dark, shows live frames), `01`–`14` effects, `15` custom per-key |
| 0x26 + 2·(m−1) | Effect m: colour byte, speed/brightness byte |

- **Colour byte:** `0`–`6` picks a colour from the effect's palette entry; `7` is multicolour.
- **Speed/brightness byte:** high nibble = speed 0–4 (higher is faster), low nibble = brightness 1–4. Confirmed by pressing the keyboard's own Fn speed and brightness keys and reading the block back.

Effects, named from watching each one:

| Mode | Name | What it does |
|---|---|---|
| 01 | Static | Whole board one colour, no movement |
| 02 | Breathing | Whole board fades in and out |
| 03 | Spectrum Cycle | Whole board cycles through colours |
| 04 | Reactive Line | Light runs sideways along the row from the pressed key |
| 05 | Raindrop | Drops of light fall across the keys |
| 06 | Pinwheel | Colours rotate around a centre |
| 07 | Reactive Ripple | A ring spreads out from the pressed key |
| 08 | Starlight | Random keys light up and fade |
| 09 | Reverse Reactive | Board lit; the pressed key dims, then lights again |
| 0A | Snake | A line of light travels around the keys |
| 0B | Wave | A wave sweeps across the board |
| 0C | Reactive | Only the pressed key lights, then fades |
| 0D | Rainbow Wave | Rainbow bands move across the board |
| 0E | Sweep | A band sweeps back and forth |
| 0F | Spin | Colours rotate like a fan (similar to 06) |
| 10 | Vertical Wave | A wave moves vertically/diagonally |
| 11 | Outward Wave | Colours flow out from the centre |
| 12 | Inward Wave | Colours flow into the centre |
| 13 | Crossing Sweep | Two bands sweep towards each other and cross |
| 14 | Reactive Cross | Light runs along the pressed key's row and column |

## Palette block (address B8)

- **Write:** SET 6 `06 08 B8 00 40 00 00 00` + 1024 bytes. The app sends it before every config write.
  - Entry m (effect m) = 7 RGB colours at offset 21·m after the 8-byte header.
  - The default for every effect is `FF0000 0000FF 00FF00 FFFF00 FF00FF 00FFFF FFFFFF`.
- **Read** (*not in the app captures*): SET 5 `05 88 B8 00 40 00`, the form the PID 0016 driver uses, then GET 6.
  - The reply header is `06 88 00 00 00 00 00 00`; the address isn't echoed.
  - The 1024 data bytes start at byte 8, and they were exactly what had been written.
- **Any colour works:** put it into the effect's entry, point the colour byte at that slot, then write the palette and then the config. Tested with FF8000 on effect 0B.

## Per-key custom mode (addresses BC + C0)

- SET 6 `06 09 BC 00 40 00 00 00` + 1024, then `06 09 C0 00 40 00 00 00` + 1024. Together they form one 2048-byte region.
- Layout: 5 slots × R, G, B planes × 126 bytes (the 6 × 21 key matrix, row-major). The byte for key (r, c), channel ch, slot s is at `378·s + 126·ch + 21·r + c`.
- Then a config write with [0x14] = `01` and [0x15] = `15`. Custom mode shows slot 0.
- The app's default slots are its game presets: slot 0 is WASD + arrows, slot 3 is 1–7 / QWER / ASDF, and so on.
- Tested by lighting one extra key and then restoring the app's data.

## Live frames (Direct)

- SET 8 `08 0A 7A 01` + 378 bytes = 126 × RGB, **column-major**: key (r, c) is at `3·(6c + r)`. This is not the per-key blocks' layout.
- Frames are shown only in mode `00`. The keyboard keeps showing the last frame, so no keepalive is needed.
- The app only ever sent all-zero frames, at about 28 Hz. The keyboard takes a frame in about 4 ms and displays at least 100 fps (checked with a camera). Streaming at 60 fps for several minutes worked, and typing stayed normal.

## Key matrix

6 × 21; columns 17–20 are unused; 94 keys. Mapped by lighting one column at a time.

| r\c | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 | 16 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | Esc | | F1 | F2 | F3 | F4 | F5 | F6 | F7 | F8 | F9 | F10 | F11 | F12 | PrtSc | KP+ | KP− |
| 1 | \` | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 0 | - | = | Bksp | NumLk | KP/ | KP* |
| 2 | Tab | Q | W | E | R | T | Y | U | I | O | P | [ | ] | \\ | KP7 | KP8 | KP9 |
| 3 | Caps | A | S | D | F | G | H | J | K | L | ; | ' | | Enter | KP4 | KP5 | KP6 |
| 4 | LShift | Z | X | C | V | B | N | M | , | . | / | RShift | | Up | KP1 | KP2 | KP3 |
| 5 | LCtrl | Win | LAlt | | | Space | | | RAlt | RCtrl | Fn | | Left | Down | Right | KP0 | KP. |

## Timing and pitfalls

- **Block writes:** the app waits about 20 ms after each packet and about 60 ms after a block write (B6, B8, BC, C0). After a block write the keyboard drops packets for a while:
  - a config write sent 20 ms after a palette write was lost;
  - live frames sent within about 50 ms of a config write were dropped.

  Waiting 100 ms after block writes, and 200 ms before frames, has been reliable.
- **Don't interleave packets from two programs.** A config read (`05 83 B6`, then GET) collided with another process's live frames, and the GET failed with EPIPE. After that the lighting controller timed out on every SET until the keyboard was replugged. Typing kept working, and nothing was damaged.
- **Linux vs Windows:** on Linux all four vendor collections share one hidraw path, so a detector registered on 0xFF00/0x01 runs once per collection and needs to deduplicate. On Windows they are separate paths, and reports 5, 6 and 8 are probably on different collections. Windows is untested.

## Tested on a K636 (Linux)

- the probe;
- config read and write;
- all 20 effects;
- colour, speed and brightness, including reading back changes made with the Fn keys;
- palette read and write, including a colour that isn't in the palette;
- per-key custom mode;
- live frames, including mapping the full key matrix.

Apart from the palette read, every packet matches the app's captures byte for byte. The keyboard works normally afterwards.
