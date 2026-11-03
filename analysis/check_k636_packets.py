#!/usr/bin/env python3
"""Offline safety check: replay a capture's device replies into k636.py and confirm every
config write it would send is byte-identical to the one the official Redragon app sent.

  check_k636_packets.py "~/Downloads/redragon/redragon rgb cap.pcapng"
"""
import importlib.util
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from capture import reports  # noqa: E402

spec = importlib.util.spec_from_file_location("k636", os.path.join(HERE, "..", "k636.py"))
k636 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(k636)

pkts = reports(sys.argv[1])
probe_reply = next(d for _, kind, rid, d in pkts if kind == "GET" and rid == 5)
config_reads = [(t, d) for t, kind, rid, d in pkts if kind == "GET" and rid == 6 and d[1] == 0x83]
config_writes = [(t, d) for t, kind, rid, d in pkts if kind == "SET" and rid == 6 and d[1] == 0x03]


class Replay(k636.K636):
    def __init__(self, config_reply):
        self.sent, self.config_reply = [], config_reply

    def set_feature(self, data, wait=0.02):
        self.sent.append(bytes(data))

    def get_feature(self, report_id, length):
        return probe_reply if report_id == 5 else self.config_reply


all_ok = True
for t_read, reply in config_reads:
    later = [(t, d) for t, d in config_writes if t > t_read]
    if not later:
        continue
    t_write, app_packet = later[0]
    kb = Replay(reply)
    assert kb.probe() == k636.EXPECTED_PROBE, "probe reply in capture is not the K636 one"
    cfg = bytearray(kb.read_block(k636.CFG_ADDR))
    # apply the same edits the app made, then compare the framing k636.py produces
    cfg[k636.CFG_MODE_FLAG] = app_packet[k636.CFG_MODE_FLAG]
    cfg[k636.CFG_MODE] = app_packet[k636.CFG_MODE]
    cfg[k636.CFG_MODE_TABLE:0x4E] = app_packet[k636.CFG_MODE_TABLE:0x4E]
    kb.write_config(cfg)
    same = kb.sent[-1] == app_packet
    all_ok &= same
    print(f"read@{t_read:6.2f} -> app write@{t_write:6.2f} mode={app_packet[0x15]:#04x}: identical={same}")

# per-key: config read, then 09 bc + 09 c0, then a custom-mode config write
block_writes = [(t, d) for t, kind, rid, d in pkts if kind == "SET" and rid == 6]
for t_read, reply in config_reads:
    later = [d for t, d in block_writes if t > t_read]
    if not later or later[0][1:3] != b"\x09\xbc":
        continue
    app_bc, app_c0 = later[0], later[1]
    app_cfg = next(d for d in later if d[1] == 0x03)
    kb = Replay(reply)
    kb.probe()
    kb.set_perkey(app_bc[8:] + app_c0[8:])
    ours = [d for d in kb.sent if d[0] == 6]
    same = ours == [app_bc, app_c0, app_cfg]
    all_ok &= same
    print(f"read@{t_read:6.2f} -> app per-key bc/c0 + custom-mode config: identical={same}")

# palette: config read, then 08 b8, then the config written back unchanged (the app did this at 33.4 s,
# with a keymap read/write in between that set-palette doesn't send)
for t_read, reply in config_reads:
    later = [d for t, d in block_writes if t > t_read]
    if not later or later[0][1:3] != b"\x08\xb8":
        continue
    app_b8 = later[0]
    app_cfg = next(d for d in later if d[1] == 0x03)
    if app_cfg[2:len(reply)] != reply[2:]:
        continue  # the app also changed the config here
    kb = Replay(reply)
    kb.probe()
    kb.set_palette(app_b8[8:])
    ours = [d for d in kb.sent if d[0] == 6]
    same = ours == [app_b8, app_cfg]
    all_ok &= same
    print(f"read@{t_read:6.2f} -> app palette 08 b8 + unchanged config: identical={same}")

# stream: an all-zero frame must match the app's report 8 packets
app_frames = {d for _, kind, rid, d in pkts if kind == "SET" and rid == 8}
if app_frames:
    kb = Replay(None)
    kb.send_frame(bytes(k636.STREAM_DATA))
    same = {kb.sent[-1]} == app_frames
    all_ok &= same
    print(f"all-zero stream frame vs the app's {len(app_frames)} distinct report 8 packet(s): identical={same}")

print("ALL IDENTICAL" if all_ok else "MISMATCH")
sys.exit(0 if all_ok else 1)
