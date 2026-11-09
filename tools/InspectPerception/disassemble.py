"""Read-only disassembly of the owner's installed GameAssembly; output stays local.

Example: python disassemble.py 0x123456 --size 2048 --out backups/perception.txt
Use live IL2CPP MethodInfo function-pointer RVAs; do not guess code addresses.
"""
from pathlib import Path
import argparse
import json
import struct
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86 import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

p = argparse.ArgumentParser()
p.add_argument("rva", type=lambda x: int(x, 0))
p.add_argument("--size", type=int, default=2048)
p.add_argument("--assembly", default=r"G:\Steam\steamapps\common\ZED ZONE\GameAssembly.dll")
p.add_argument("--symbols", help="Optional JSON object mapping RVA strings to method names")
p.add_argument("--out")
p.add_argument("--metadata", default=r"G:\Steam\steamapps\common\ZED ZONE\ZEDZONE_Data\il2cpp_data\Metadata\global-metadata.dat")
a = p.parse_args()
pe = pefile.PE(a.assembly, fast_load=True)
symbols = {}
if a.symbols:
    symbols = {int(k, 0): v for k, v in json.loads(Path(a.symbols).read_text()).items()}
md = Cs(CS_ARCH_X86, CS_MODE_64)
md.detail = True
metadata = Path(a.metadata).read_bytes() if Path(a.metadata).exists() else None
header = struct.unpack_from("<6I", metadata) if metadata else None
rows = []
for i in md.disasm(pe.get_data(a.rva, a.size), a.rva):
    annotations = []
    if i.mnemonic in ("call", "jmp") and i.operands and i.operands[0].type == X86_OP_IMM:
        target = i.operands[0].imm
        if target in symbols:
            annotations.append(symbols[target])
    for op in i.operands:
        if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
            address = i.address + i.size + op.mem.disp
            data = pe.get_data(address, 8)
            annotations.append(f"rip_data RVA=0x{address:x} bytes={data.hex()}")
            if len(data) == 8 and metadata:
                encoded = struct.unpack("<Q", data)[0]
                index = (encoded & 0x1FFFFFFF) >> 1
                if encoded >> 29 == 5 and index * 8 < header[3]:
                    length, offset = struct.unpack_from("<II", metadata, header[2] + index * 8)
                    literal = metadata[header[4] + offset:header[4] + offset + length].decode("utf-8", errors="replace")
                    annotations.append("literal=" + repr(literal))
            if len(data) >= 4 and ("ss" in i.mnemonic):
                annotations.append("f32=" + str(struct.unpack("<f", data[:4])[0]))
    rows.append(f"{i.address:08x}: {i.mnemonic:8} {i.op_str}" + (" ; " + " | ".join(annotations) if annotations else ""))
text = "\n".join(rows) + "\n"
if a.out:
    Path(a.out).parent.mkdir(parents=True, exist_ok=True)
    Path(a.out).write_text(text, encoding="utf-8")
else:
    print(text, end="")
