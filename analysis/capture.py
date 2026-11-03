#!/usr/bin/env python3
"""Decode USBPcap (.pcapng, linktype 249) captures of HID feature-report traffic.

  capture.py summary FILE       SET/GET report sequence (repeated report 8 packets collapsed)
  capture.py config-diff FILE   K636 config block (report 6, 03 b6) changes, write by write
  capture.py blocks FILE        K636 colour palettes (08 b8) and per-key planes (09 bc + 09 c0)
  capture.py perkey-image FILE OUT   save the app's 2048-byte per-key image (bc + c0 data) to OUT
  capture.py palette-image FILE OUT  save the app's 1024-byte colour palette block (08 b8 data) to OUT
"""
import argparse
import struct


def read_packets(path):
    """Yield (timestamp, usbpcap_packet) for every Enhanced Packet Block."""
    data = open(path, "rb").read()
    off, endian, linktypes = 0, "<", []
    while off + 12 <= len(data):
        btype, blen = struct.unpack_from(endian + "II", data, off)
        if btype == 0x0A0D0D0A:
            endian = "<" if data[off + 8:off + 12] == b"\x4d\x3c\x2b\x1a" else ">"
            btype, blen = struct.unpack_from(endian + "II", data, off)
        body = data[off + 8:off + blen - 4]
        if btype == 1:
            linktypes.append(struct.unpack_from(endian + "H", body, 0)[0])
        elif btype == 6:
            iface, tsh, tsl, caplen, _ = struct.unpack_from(endian + "IIIII", body, 0)
            if linktypes[iface] != 249:
                raise SystemExit(f"linktype {linktypes[iface]} is not USBPcap")
            yield ((tsh << 32) | tsl) / 1e6, body[20:20 + caplen]
        if blen == 0:
            break
        off += blen


def reports(path, device=None):
    """Return [(t, 'SET'|'GET', report_id, bytes)] for HID SET_REPORT/GET_REPORT control transfers.

    device filters on the USBPcap device address (e.g. 3) when a capture has several devices.
    """
    out, pending, t0 = [], {}, None
    for ts, pkt in read_packets(path):
        hlen, _, _, _, info, bus, dev, _, transfer = struct.unpack_from("<HQIHBHHBB", pkt, 0)
        if transfer != 2 or (device is not None and dev != device):
            continue
        t0 = ts if t0 is None else t0
        stage, payload = pkt[27], pkt[hlen:]
        if stage == 0:  # setup stage: 8-byte setup packet, then any OUT data
            setup, rest = payload[:8], payload[8:]
            if setup[0] == 0x21 and setup[1] == 0x09:
                if rest:
                    out.append((ts - t0, "SET", setup[2], bytes(rest)))
                    pending.pop(dev, None)
                else:
                    pending[dev] = ("SET", setup[2])
            elif setup[0] == 0xA1 and setup[1] == 0x01:
                pending[dev] = ("GET", setup[2])
            else:
                pending.pop(dev, None)
        elif dev in pending and payload:
            kind, rid = pending.pop(dev)
            out.append((ts - t0, kind, rid, bytes(payload)))
        else:
            pending.pop(dev, None)
    return out


def summary(path):
    last, repeats = None, 0
    for t, kind, rid, d in reports(path):
        key = (kind, rid, d)
        if key == last:
            repeats += 1
            continue
        if repeats:
            print(f"{'':8} ... same packet repeated {repeats} more times")
        last, repeats = key, 0
        more = " ..." if len(d) > 40 else ""
        print(f"{t:8.3f} {kind} rpt={rid:#04x} n={len(d):4d} {d[:40].hex(' ')}{more}")
    if repeats:
        print(f"{'':8} ... same packet repeated {repeats} more times")


def config_diff(path):
    pkts = reports(path)
    prev = next((d for _, k, r, d in pkts if k == "GET" and r == 6 and d[1] == 0x83), None)
    if prev is None:
        raise SystemExit("no config read (05 83 ..) in this capture")
    for t, kind, rid, d in pkts:
        if kind != "SET" or rid != 6 or d[1] != 0x03:
            continue
        d = d[:len(prev)]
        diffs = [f"[{i:#05x}]{prev[i]:02x}->{d[i]:02x}" for i in range(2, len(prev)) if prev[i] != d[i]]
        print(f"t={t:7.2f} mode[0x15]={d[0x15]:#04x} flag[0x14]={d[0x14]:#04x} changed: {' '.join(diffs) or 'nothing'}")
        prev = d


BLOCK_HEADER, BLOCK_DATA = 8, 1024   # SET 6 writes: 06 cmd addr 00 40 00 00 00, then 1024 data bytes
ROWS, COLS = 6, 21                   # key matrix; 126 keys, as in the report 8 stream


def blocks(path):
    writes = {}
    for _, kind, rid, d in reports(path):
        if kind == "SET" and rid == 6 and d[1] in (0x08, 0x09):
            writes.setdefault(d[2], set()).add(d)
    for addr, ws in sorted(writes.items()):
        print(f"block {addr:02x}: {len(ws)} distinct write(s), header {next(iter(ws))[:BLOCK_HEADER].hex(' ')}")
    data = lambda addr: next(iter(writes[addr]))[BLOCK_HEADER:BLOCK_HEADER + BLOCK_DATA]

    if 0xB8 in writes:
        print("\n08 b8 palettes: entry m = 7 colours x 3 bytes at data offset 21*m (byte order unconfirmed)")
        pal, last = data(0xB8), None
        for m in range(BLOCK_DATA // 21):
            entry = pal[m * 21:(m + 1) * 21]
            if entry != last:
                print(f"  entry {m:2d}+: " + " ".join(entry[i:i + 3].hex() for i in range(0, 21, 3)))
            last = entry

    if 0xBC in writes and 0xC0 in writes:
        print(f"\n09 bc + 09 c0 per-key: one {2 * BLOCK_DATA}-byte region, planes of {ROWS}x{COLS} bytes,"
              " read as slot = plane // 3, channel = plane % 3 (hypothesis); empty planes skipped")
        keys = data(0xBC) + data(0xC0)
        size = ROWS * COLS
        for p in range(len(keys) // size):
            plane = keys[p * size:(p + 1) * size]
            if not any(plane):
                continue
            print(f"  plane {p:2d} (slot {p // 3}, channel {p % 3}), values {sorted(set(plane) - {0})}")
            for r in range(ROWS):
                print("    " + "".join("#" if b else "." for b in plane[r * COLS:(r + 1) * COLS]))
        tail = keys[(len(keys) // size) * size:]
        print(f"  bytes after the last whole plane: {'non-zero' if any(tail) else 'all zero'}")


def perkey_image(path, out):
    first = {}
    for _, kind, rid, d in reports(path):
        if kind == "SET" and rid == 6 and d[1] == 0x09:
            first.setdefault(d[2], d)
    image = b"".join(first[a][BLOCK_HEADER:BLOCK_HEADER + BLOCK_DATA] for a in (0xBC, 0xC0))
    open(out, "wb").write(image)
    print(f"saved {len(image)} bytes to {out}")


def palette_image(path, out):
    block = next(d for _, kind, rid, d in reports(path) if kind == "SET" and rid == 6 and d[1] == 0x08)
    image = block[BLOCK_HEADER:BLOCK_HEADER + BLOCK_DATA]
    open(out, "wb").write(image)
    print(f"saved {len(image)} bytes to {out}")


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["summary", "config-diff", "blocks", "perkey-image", "palette-image"])
    ap.add_argument("file")
    ap.add_argument("out", nargs="?")
    args = ap.parse_args()
    if args.cmd in ("perkey-image", "palette-image"):
        image_cmd = perkey_image if args.cmd == "perkey-image" else palette_image
        image_cmd(args.file, args.out or ap.error(f"{args.cmd} needs OUT"))
    else:
        {"summary": summary, "config-diff": config_diff, "blocks": blocks}[args.cmd](args.file)
