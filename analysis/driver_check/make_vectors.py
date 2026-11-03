#!/usr/bin/env python3
"""Write test vectors from a capture for driver_check.cpp.

  make_vectors.py CAPTURE OUT [PALETTE_REPLY]

PALETTE_REPLY is a saved reply to the palette read (05 88 b8 00 40 00) from the keyboard.

Each case is the app's config read reply and the config write that followed it, kept only when the
write changes nothing but [0x14], [0x15] and the active effect's two table bytes (what the driver can
express). Also writes the probe reply and the app's report 8 frame.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from capture import reports  # noqa: E402

pkts = reports(sys.argv[1])
out = open(sys.argv[2], "w")
probe = next(d for _, k, r, d in pkts if k == "GET" and r == 5)
out.write(f"PROBE {probe.hex()}\n")
frame = next(d for _, k, r, d in pkts if k == "SET" and r == 8)
out.write(f"FRAME {frame.hex()}\n")
reads = [(t, d) for t, k, r, d in pkts if k == "GET" and r == 6 and d[1] == 0x83]
writes = [(t, d) for t, k, r, d in pkts if k == "SET" and r == 6 and d[1] == 0x03]
cases = 0
for t_read, rd in reads:
    t_w, wr = next(((t, d) for t, d in writes if t > t_read), (None, None))
    if wr is None:
        continue
    mode = wr[0x15]
    if not 0 <= mode <= 20 or wr[0x14] != 0:
        continue
    allowed = {0x14, 0x15}
    if mode:
        allowed |= {0x26 + 2 * (mode - 1), 0x27 + 2 * (mode - 1)}
    if any(rd[i] != wr[i] for i in range(2, len(rd)) if i not in allowed):
        continue
    flag, sb = (wr[0x26 + 2 * (mode - 1)], wr[0x27 + 2 * (mode - 1)]) if mode else (0, 0)
    out.write(f"CASE {t_read:.2f} {mode} {flag} {sb >> 4} {sb & 15}\nREAD {rd.hex()}\nEXPECT {wr.hex()}\n")
    cases += 1
# palette: config read, then 08 b8, then the config written back unchanged
pcases = 0
block_writes = [(t, d) for t, k, r, d in pkts if k == "SET" and r == 6]
for t_read, rd in reads:
    later = [d for t, d in block_writes if t > t_read]
    if not later or later[0][1:3] != b"\x08\xb8":
        continue
    b8 = later[0]
    wr = next(d for d in later if d[1] == 0x03)
    if wr[2:len(rd)] != rd[2:] or not 1 <= wr[0x15] <= 20 or wr[0x14]:
        continue
    mode = wr[0x15]
    flag, sb = wr[0x26 + 2 * (mode - 1)], wr[0x27 + 2 * (mode - 1)]
    out.write(f"PCASE {t_read:.2f} {mode} {flag} {sb >> 4} {sb & 15}\nREAD {rd.hex()}\nPALETTE {b8.hex()}\nEXPECT {wr.hex()}\n")
    pcases += 1
if len(sys.argv) > 3:
    out.write(f"PALETTEREPLY {open(sys.argv[3], 'rb').read().hex()}\n")
print(f"{cases} config cases and {pcases} palette cases written to {sys.argv[2]}")
