#!/usr/bin/env python3
"""Talk to a Redragon K636 (258a:0049) lighting interface over hidraw.

Only sends packet shapes seen in captures of the official Redragon app:
  probe   05 05 81            -> GET report 5 (8 bytes)
  read    05 83 b6 / 05 84 d4 -> GET report 6 (1032 bytes)
  write   06 03 b6 + config   (read-modify-write of the block just read)
  write   06 09 bc / 06 09 c0 + 1024 bytes each (per-key image), then the config
          with [0x14]=01 [0x15]=15: the app's custom-mode sequence
  write   06 08 b8 + 1024 bytes (colour palettes), then the config written back unchanged:
          the app's sequence before every config change
  stream  08 0a 7a 01 + 378 bytes (report 8), every ~35 ms like the app; the app only
          ever sent all-zero frames, so non-zero frames are an experiment
One exception, not seen from the app:
  read    05 88 b8 00 40 00   -> GET report 6 (colour palettes; the PID 0016 driver's read)
Everything refuses to run unless the probe answers with the K636 model byte, or while
another process (e.g. OpenRGB) has the device open.
"""
import argparse
import colorsys
import fcntl
import glob
import os
import sys
import time

VID, PID = 0x258A, 0x0049
EXPECTED_PROBE = bytes.fromhex("05 01 02 00 00 00 01 5d")

REPORT_CMD, REPORT_DATA = 0x05, 0x06
DATA_LEN, PROBE_LEN = 1032, 8
BLOCK_SETTLE = 0.1  # after a block write the keyboard drops packets for a while; the app waits ~60 ms

CFG_ADDR, KEYMAP_ADDR = 0xB6, 0xD4
BLOCK_CMD = {CFG_ADDR: 0x03, KEYMAP_ADDR: 0x04}  # write command; read is 0x80 | cmd
CFG_MODE_FLAG = 0x14      # 0x01 when custom per-key mode is active
CFG_MODE = 0x15
CFG_MODE_TABLE = 0x26     # 20 modes x (colour flag, speed/brightness)
CFG_MARKERS = (0x0E, 0x4E, 0x89)  # each holds 5a a5
CUSTOM_MODE = 0x15

BLOCK_HEADER, BLOCK_DATA = 8, 1024  # 06 cmd addr 00 40 00 00 00, then data
PERKEY_ADDRS = (0xBC, 0xC0)         # one 2048-byte image split over two writes
PERKEY_USED = 5 * 3 * 126           # 5 slots x 3 channel planes x 126 keys; the rest stays zero
PALETTE_ADDR = 0xB8                 # entry m = 7 RGB colours at 21*m, for effect m

STREAM_HEADER = bytes([0x08, 0x0A, 0x7A, 0x01])  # report 8, 382 bytes in all
STREAM_DATA = 3 * 126
STREAM_PERIOD = 0.035                             # the app's pace (~28 Hz)


def _ioc(direction, nr, size):
    return (direction << 30) | (size << 16) | (ord("H") << 8) | nr


def HIDIOCSFEATURE(n):
    return _ioc(3, 0x06, n)


def HIDIOCGFEATURE(n):
    return _ioc(3, 0x07, n)


def find_node():
    for d in glob.glob("/sys/class/hidraw/hidraw*"):
        uevent = open(os.path.join(d, "device/uevent")).read()
        if f"HID_ID=0003:{VID:08X}:{PID:08X}" in uevent and "input1" in uevent:
            return "/dev/" + os.path.basename(d)
    sys.exit("K636 lighting interface (258a:0049 input1) not found")


def other_users(node):
    """PIDs of other processes holding node open (e.g. OpenRGB)."""
    pids = []
    for fd_dir in glob.glob("/proc/[0-9]*/fd"):
        pid = int(fd_dir.split("/")[2])
        if pid == os.getpid():
            continue
        try:
            if any(os.readlink(os.path.join(fd_dir, fd)) == node for fd in os.listdir(fd_dir)):
                pids.append(pid)
        except OSError:
            continue
    return pids


class K636:
    def __init__(self, node):
        # A command and its reply must not interleave with another program's packets: a config read
        # that collided with OpenRGB's live frames locked up the lighting controller until a replug.
        busy = other_users(node)
        if busy:
            sys.exit(f"{node} is open in another process (PID {', '.join(map(str, busy))}); close it first")
        self.fd = os.open(node, os.O_RDWR)

    def set_feature(self, data, wait=0.02):
        buf = bytearray(data)
        fcntl.ioctl(self.fd, HIDIOCSFEATURE(len(buf)), buf)
        time.sleep(wait)

    def get_feature(self, report_id, length):
        buf = bytearray(length)
        buf[0] = report_id
        n = fcntl.ioctl(self.fd, HIDIOCGFEATURE(length), buf, True)
        time.sleep(0.02)
        return bytes(buf[:n])

    def probe(self):
        self.set_feature(bytes([REPORT_CMD, 0x05, 0x81, 0, 0, 0]))
        return self.get_feature(REPORT_CMD, PROBE_LEN)

    def read_block(self, addr):
        read_cmd = 0x80 | BLOCK_CMD[addr]
        self.set_feature(bytes([REPORT_CMD, read_cmd, addr, 0, 0, 0]))
        data = self.get_feature(REPORT_DATA, DATA_LEN)
        if data[:3] != bytes([REPORT_DATA, read_cmd, addr]):
            sys.exit(f"unexpected read reply header {data[:6].hex(' ')}")
        return data

    def write_config(self, cfg):
        for off in CFG_MARKERS:
            if cfg[off:off + 2] != b"\x5a\xa5":
                sys.exit(f"refusing to write: config marker missing at {off:#x}")
        pkt = bytearray(DATA_LEN)
        pkt[:len(cfg)] = cfg
        pkt[1] = 0x03
        self.set_feature(pkt, BLOCK_SETTLE)

    def set_perkey(self, image):
        if len(image) != len(PERKEY_ADDRS) * BLOCK_DATA or any(image[PERKEY_USED:]):
            sys.exit(f"per-key image must be {len(PERKEY_ADDRS) * BLOCK_DATA} bytes, zero after byte {PERKEY_USED}")
        cfg = bytearray(self.read_block(CFG_ADDR))
        for i, addr in enumerate(PERKEY_ADDRS):
            pkt = bytearray(DATA_LEN)
            pkt[:5] = bytes([REPORT_DATA, 0x09, addr, 0x00, 0x40])
            pkt[BLOCK_HEADER:] = image[i * BLOCK_DATA:(i + 1) * BLOCK_DATA]
            self.set_feature(pkt, BLOCK_SETTLE)
        cfg[CFG_MODE_FLAG], cfg[CFG_MODE] = 0x01, CUSTOM_MODE
        self.write_config(cfg)

    def read_palette(self):
        # Not seen from the app: the read the OpenRGB driver for Sinowealth PID 0016 uses for its colour block
        self.set_feature(bytes([REPORT_CMD, 0x88, PALETTE_ADDR, 0x00, 0x40, 0x00]))
        data = self.get_feature(REPORT_DATA, DATA_LEN)
        if data[:2] != bytes([REPORT_DATA, 0x88]):  # the reply does not echo the address
            sys.exit(f"unexpected palette reply header {data[:8].hex(' ')} ({len(data)} bytes)")
        return data

    def set_palette(self, image):
        if len(image) != BLOCK_DATA:
            sys.exit(f"palette image must be {BLOCK_DATA} bytes")
        cfg = bytearray(self.read_block(CFG_ADDR))
        pkt = bytearray(DATA_LEN)
        pkt[:5] = bytes([REPORT_DATA, 0x08, PALETTE_ADDR, 0x00, 0x40])
        pkt[BLOCK_HEADER:] = image
        self.set_feature(pkt, BLOCK_SETTLE)
        self.write_config(cfg)

    def send_frame(self, frame, wait=0.02):
        if len(frame) != STREAM_DATA:
            sys.exit(f"stream frame must be {STREAM_DATA} bytes")
        self.set_feature(STREAM_HEADER + bytes(frame), wait)


def open_checked():
    kb = K636(find_node())
    reply = kb.probe()
    if reply != EXPECTED_PROBE:
        sys.exit(f"probe answered {reply.hex(' ')}, expected {EXPECTED_PROBE.hex(' ')}; not a known K636, stopping")
    return kb


def describe(cfg):
    print(f"mode={cfg[CFG_MODE]:#04x} custom_flag={cfg[CFG_MODE_FLAG]:#04x}")
    for m in range(20):
        flag, sb = cfg[CFG_MODE_TABLE + 2 * m], cfg[CFG_MODE_TABLE + 2 * m + 1]
        print(f"  mode {m + 1:#04x}: colour_flag={flag:#04x} speed/brightness={sb:#04x}")


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("probe")
    sub.add_parser("dump")
    b = sub.add_parser("backup")
    b.add_argument("dir")
    d = sub.add_parser("diff")
    d.add_argument("file")
    m = sub.add_parser("set-mode")
    m.add_argument("mode", type=lambda s: int(s, 0))
    p = sub.add_parser("set-mode-params")
    p.add_argument("mode", type=lambda s: int(s, 0))
    p.add_argument("--colour", type=lambda s: int(s, 0))
    p.add_argument("--speed-brightness", type=lambda s: int(s, 0))
    k = sub.add_parser("set-perkey")
    k.add_argument("file")
    rp = sub.add_parser("read-palette")
    rp.add_argument("out")
    pl = sub.add_parser("set-palette")
    pl.add_argument("file")
    s = sub.add_parser("stream")
    s.add_argument("file")
    s.add_argument("--seconds", type=float, default=3.0)
    a = sub.add_parser("stream-anim")
    a.add_argument("--period-ms", type=float, default=STREAM_PERIOD * 1000)
    a.add_argument("--seconds", type=float, default=5.0)
    a.add_argument("--pattern", choices=["rainbow", "bar", "runner"], default="rainbow")
    r = sub.add_parser("restore-config")
    r.add_argument("file")
    args = ap.parse_args()

    if args.cmd == "probe":
        kb = K636(find_node())
        reply = kb.probe()
        print(reply.hex(" "), "(K636)" if reply == EXPECTED_PROBE else "(UNKNOWN MODEL)")
        return

    kb = open_checked()
    if args.cmd == "dump":
        cfg = kb.read_block(CFG_ADDR)
        for i in range(0, len(cfg), 16):
            print(f"{i:04x}: {cfg[i:i + 16].hex(' ')}")
        describe(cfg)
    elif args.cmd == "backup":
        os.makedirs(args.dir, exist_ok=True)
        stamp = time.strftime("%Y%m%d-%H%M%S")
        for name, addr in (("config", CFG_ADDR), ("keymap", KEYMAP_ADDR)):
            path = os.path.join(args.dir, f"{name}-{stamp}.bin")
            open(path, "wb").write(kb.read_block(addr))
            print("saved", path)
    elif args.cmd == "diff":
        saved, now = open(args.file, "rb").read(), kb.read_block(CFG_ADDR)
        diffs = [f"[{i:#05x}]{saved[i]:02x}->{now[i]:02x}" for i in range(min(len(saved), len(now))) if saved[i] != now[i]]
        print(" ".join(diffs) or "identical")
    elif args.cmd == "set-mode":
        cfg = bytearray(kb.read_block(CFG_ADDR))
        print(f"mode {cfg[CFG_MODE]:#04x} -> {args.mode:#04x}")
        cfg[CFG_MODE] = args.mode
        kb.write_config(cfg)
    elif args.cmd == "set-mode-params":
        # stay inside the values seen in captures: flags 00-07, nibbles 0-4
        if not 1 <= args.mode <= 20:
            sys.exit("mode must be 0x01-0x14")
        if args.colour is not None and not 0 <= args.colour <= 7:
            sys.exit("colour flag must be 0-7")
        sb = args.speed_brightness
        if sb is not None and not (0 <= sb >> 4 <= 4 and sb & 0x0F <= 4):
            sys.exit("speed/brightness nibbles must be 0-4")
        cfg = bytearray(kb.read_block(CFG_ADDR))
        off = CFG_MODE_TABLE + 2 * (args.mode - 1)
        old = bytes(cfg[off:off + 2])
        if args.colour is not None:
            cfg[off] = args.colour
        if sb is not None:
            cfg[off + 1] = sb
        print(f"mode {args.mode:#04x} [{off:#04x}]: colour_flag {old[0]:#04x} -> {cfg[off]:#04x},"
              f" speed/brightness {old[1]:#04x} -> {cfg[off + 1]:#04x}")
        kb.write_config(cfg)
    elif args.cmd == "set-perkey":
        kb.set_perkey(open(args.file, "rb").read())
        print("per-key image written from", args.file, "and custom mode selected")
    elif args.cmd == "read-palette":
        data = kb.read_palette()
        open(args.out, "wb").write(data)
        print(f"{len(data)} bytes, header {data[:8].hex(' ')}; saved to {args.out}")
    elif args.cmd == "set-palette":
        kb.set_palette(open(args.file, "rb").read())
        print("palette written from", args.file, "(config written back unchanged)")
    elif args.cmd == "stream":
        frame = open(args.file, "rb").read()
        end, sent = time.monotonic() + args.seconds, 0
        while time.monotonic() < end:
            kb.send_frame(frame)
            sent += 1
            time.sleep(STREAM_PERIOD - 0.02)  # set_feature already waits 20 ms
        print(f"sent {sent} frames from {args.file}")
    elif args.cmd == "stream-anim":
        # rainbow: hues drifting across the columns; bar: a white column sweeping back and forth at
        # 40 columns/s. Both move at a fixed speed, so higher frame rates should look smoother.
        # runner: one white key on the number row, one column further every frame (camera rate test)
        period = args.period_ms / 1000
        if period < 0.004:
            sys.exit("period below 4 ms refused")
        start = next_at = time.monotonic()
        sends = []
        while (now := time.monotonic()) < start + args.seconds:
            frame = bytearray(STREAM_DATA)
            bar = round(abs(((now - start) * 40) % 32 - 16))
            for col in range(21):
                if args.pattern == "runner":
                    if col == len(sends) % 17:
                        frame[3 * (6 * col + 1):3 * (6 * col + 1) + 3] = b"\xff\xff\xff"
                    continue
                if args.pattern == "bar":
                    rgb = (1.0, 1.0, 1.0) if col == bar else (0.0, 0.0, 0.0)
                else:
                    rgb = colorsys.hsv_to_rgb((col / 17 + (now - start) * 0.5) % 1.0, 1.0, 1.0)
                for row in range(6):
                    i = 3 * (6 * col + row)
                    frame[i:i + 3] = bytes(int(v * 255) for v in rgb)
            t0 = time.monotonic()
            kb.send_frame(frame, wait=0)
            sends.append(time.monotonic() - t0)
            next_at += period
            time.sleep(max(0.0, next_at - time.monotonic()))
        elapsed = time.monotonic() - start
        print(f"{len(sends)} frames in {elapsed:.2f} s = {len(sends) / elapsed:.1f} fps;"
              f" send time mean {1000 * sum(sends) / len(sends):.2f} ms, max {1000 * max(sends):.2f} ms")
    elif args.cmd == "restore-config":
        saved = open(args.file, "rb").read()
        if saved[:3] != bytes([REPORT_DATA, 0x83, CFG_ADDR]):
            sys.exit("not a config backup")
        kb.write_config(saved)
        print("config restored from", args.file)


if __name__ == "__main__":
    main()
