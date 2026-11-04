# Redragon K636 Kitava for OpenRGB

This repo adds lighting control for the **Redragon K636 Kitava** keyboard (USB `258a:0049`) to [OpenRGB](https://gitlab.com/CalcProgrammer1/OpenRGB). It contains:

- the driver, as patches for OpenRGB;
- the protocol notes and test tools it is based on.

Upstream OpenRGB doesn't support this keyboard yet. The protocol has been reported in [OpenRGB issue #5588](https://gitlab.com/CalcProgrammer1/OpenRGB/-/issues/5588).

> **Read this first.**
>
> - USB ID `258a:0049` is shared by several keyboards. OpenRGB's older driver for it was disabled because it bricked Redragon K618 and K621 boards.
> - This driver first asks the keyboard for its model. It continues only if the keyboard answers as a K636 (model byte `5D`); every other keyboard is left alone.
> - Don't remove that check.
> - It has been tested on one K636 (bcdDevice 1.01) on Linux. Use it at your own risk.

## What works

- The keyboard shows up in OpenRGB as **Redragon K636 Kitava**, with all 94 keys.
- **Direct** mode: live per-key colours at about 60 fps. Works with the OpenRGB Effects plugin.
- **20 onboard effects:**
  - Static, Breathing, Spectrum Cycle, Reactive Line, Raindrop, Pinwheel, Reactive Ripple, Starlight, Reverse Reactive, Snake;
  - Wave, Reactive, Rainbow Wave, Sweep, Spin, Vertical Wave, Outward Wave, Inward Wave, Crossing Sweep, Reactive Cross.

  Each has speed (0–4), brightness (1–4), and any colour or random colours.
- Opening OpenRGB changes nothing on the keyboard: it reads the current state first, and only writes when you change something.

Not supported yet:

- the keyboard's saved per-key "custom" mode;
- Windows;
- other K6** models.

## Build from scratch

The patches apply to OpenRGB commit `c6fc8016` (master, 30 Oct 2026); that's what was tested.

### Arch Linux, CachyOS, Manjaro: build a package

```sh
git clone https://github.com/tdulshan3/redragon-k636-openrgb.git
cd redragon-k636-openrgb/pkg
makepkg -si
```

This builds OpenRGB with the K636 driver and installs it in place of the `openrgb` package. The package includes udev rules, so your user can reach the keyboard without root.

To go back to the distribution's version: `sudo pacman -S openrgb`.

### Any Linux distribution: build by hand

1. **Install OpenRGB's build dependencies:** Qt with qmake, hidapi, libusb-1.0, mbedtls, git, and a C++17 compiler.
   - Arch: `sudo pacman -S --needed base-devel git qt6-base hidapi libusb mbedtls3`
   - Other distributions: see OpenRGB's [Compiling guide](https://gitlab.com/CalcProgrammer1/OpenRGB/-/blob/master/Documentation/Compiling.md).

2. **Get OpenRGB at the tested commit and apply the patches:**

   ```sh
   git clone https://github.com/tdulshan3/redragon-k636-openrgb.git
   git clone https://gitlab.com/CalcProgrammer1/OpenRGB.git
   cd OpenRGB
   git checkout c6fc8016d215cca17d660c47746a08d7ea6a9175
   git apply ../redragon-k636-openrgb/patches/*.patch
   ```

3. **Build:**

   ```sh
   mkdir build && cd build
   qmake6 ../OpenRGB.pro        # "qmake" on systems with Qt 5
   make -j"$(nproc)"
   ```

   If you run qmake again later, run `rm -rf openrgb.gch` before `make`. Otherwise the build fails with errors like `stray '\177' in program`, because gcc reads the old `openrgb` binary as a header.

4. **Give your user access to the keyboard.** Run this from the repo folder:

   ```sh
   sudo cp 61-k636.rules /etc/udev/rules.d/
   sudo udevadm control --reload-rules && sudo udevadm trigger
   ```

   For all of OpenRGB's devices instead, run this from the `build` folder:

   ```sh
   sudo ./openrgb --generate-udev-rules /etc/udev/rules.d/60-openrgb.rules
   ```

   followed by the same two `udevadm` commands.

5. **Run it** from the `build` folder: `./openrgb`

## Using it

- **Effect changes are saved in the keyboard**, like with the Redragon app. They're written once you stop adjusting a slider or the colour wheel for 0.3 s.
- **Direct mode is a saved mode too.** If you close OpenRGB while in Direct, the keyboard stays dark, or shows the last frame, until OpenRGB sets a mode again. Pick an effect before closing OpenRGB if you want lighting without it.
- **Use one program at a time.** Don't run OpenRGB at the same time as the Redragon app (for example under Wine) or `k636.py`. Commands from two programs can interleave, which locks up the keyboard's lighting controller until you unplug and replug it. Typing keeps working.
- **OpenRGB minimises to the tray** when you close its window. Quit it from the tray icon.

## What's in this repo

| Path | What it is |
|---|---|
| [`PROTOCOL.md`](PROTOCOL.md) | The full protocol: identification, config, palette and per-key blocks, effects, live frames, key matrix, timing pitfalls |
| [`patches/`](patches) | The OpenRGB driver (`Controllers/SinowealthController/SinowealthKeyboard49Controller/` plus a detector) |
| [`pkg/PKGBUILD`](pkg/PKGBUILD) | Arch package: upstream OpenRGB at the tested commit, plus the patches |
| [`61-k636.rules`](61-k636.rules) | udev rule giving the logged-in user access to `258a:0049` |
| [`k636_matrix.json`](k636_matrix.json) | The 6 × 21 key matrix, with the per-key and live-frame offset formulas |
| [`k636.py`](k636.py) | Test tool used to work out the protocol (see below) |
| [`analysis/`](analysis) | USBPcap decoder and offline checks against the app's captures |
| [`perkey/`](perkey), [`palette/`](palette) | The app's per-key and palette data (restore images), and test frames |
| [`shaders/`](shaders) | Fourteen calm animated scenes for the OpenRGB Effects plugin's Shaders effect, laid out for the `dexmap` Visual Map. See [Shaders](#shaders) below |

## Shaders

Each file in [`shaders/`](shaders) is a scene drawn across the `dexmap` Visual Map: keyboard bottom-left, mouse beside it, the two RAM sticks upper right with the D_LED ring next to them. Copy the `.fs` files into `~/.config/OpenRGB/plugins/settings/effect-shaders/`, add a Shaders effect, pick one, and apply it to the `dexmap` virtual controller only. Speed 1000 runs them in real time.

| Shader | Scene |
|---|---|
| `koi-pond.fs` | Koi swim loops over the keyboard, along the RAM sticks and around the ring; rain ripples, lily pads |
| `shoreline.fs` | Swells roll past the RAM and break on the keyboard; foam, wet sand drying |
| `firefly-meadow.fs` | Fireflies over a night meadow, the ring is the moon, a group flash every two minutes |
| `lava-lamp.fs` | Wax rises and sinks in the keyboard, each RAM stick and the ring |
| `aurora.fs` | Aurora curtains over the RAM and ring, mirrored in a frozen lake on the keyboard |
| `jellyfish.fs` | Glowing jellyfish pulse upward through the keyboard, the RAM and the ring |
| `lighthouse.fs` | RAM is the striped tower, the ring is the turning lamp, its beam sweeps the sea on the keyboard |
| `snowfall.fs` | Flakes drift down, snow piles up on the keyboard over minutes, then melts |
| `hourglass.fs` | Sand drains from the RAM onto a growing mound on the keyboard; the ring is a ten-minute dial |
| `embers.fs` | The keyboard is a bed of slowly breathing coals; sparks rise up the RAM |
| `sakura.fs` | RAM and ring are cherry blossom; petals blow across the keyboard and gather on the bottom rows |
| `star-trails.fs` | The sky turns around the ring like a long exposure; hills and windows on the bottom rows |
| `wheat-field.fs` | Wind gusts roll across a golden field; sunset sky on the RAM, the sun's glow on the ring |
| `rainy-window.fs` | Drops run down the glass over out-of-focus city lights |

Device positions are `#define`s at the top of each file, in Visual Map grid units. Update them if you move a device in the map. Each file also has a short tuning block.

## Test tool: `k636.py`

You don't need this to use the driver; it's for protocol work.

- It talks to the keyboard directly over hidraw, needs only Python 3, and finds the right device node by itself.
- It refuses to run unless the keyboard answers the model probe as a K636, or while another program has the keyboard open.
- **Some commands write to the keyboard's memory.** Run `backup` first.

```sh
python3 k636.py probe                    # model probe: should print "... 01 5d (K636)"
python3 k636.py backup backups           # save the config and keymap blocks
python3 k636.py dump                     # show the config block and every effect's settings
python3 k636.py diff backups/config-<time>.bin
python3 k636.py restore-config backups/config-<time>.bin
```

The other commands are `set-mode`, `set-mode-params`, `read-palette`, `set-palette`, `set-perkey`, `stream` and `stream-anim`. `python3 k636.py -h` lists them. `PROTOCOL.md` explains what each block does.

## Offline checks

These need no keyboard. They compare what the tools and the driver would send with what the Redragon app actually sent:

- `python3 analysis/check_k636_packets.py "redragon rgb cap.pcapng"` replays the app's capture into `k636.py`.
- `OPENRGB=/path/to/patched/OpenRGB analysis/driver_check/build.sh && analysis/driver_check/driver_check analysis/driver_check/vectors.txt` compiles the OpenRGB controller against a fake hidapi.
- `python3 analysis/capture.py summary|config-diff|blocks FILE` decodes a capture.

The capture files are attached to [#5588](https://gitlab.com/CalcProgrammer1/OpenRGB/-/issues/5588).

## License

GPL-2.0-or-later, the same as OpenRGB. See [`LICENSE`](LICENSE).

This is an independent project, not affiliated with Redragon or the OpenRGB project.
