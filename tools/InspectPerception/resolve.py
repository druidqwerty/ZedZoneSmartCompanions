"""Resolve IL2CPP native method tokens from the installed PE codegen module.

Native tokens come from the interop class's static constructor, not its regenerated
assembly's own metadata tokens. Program.cs extracts the correct originals.
"""
import argparse
import struct
import pefile

p = argparse.ArgumentParser()
p.add_argument("tokens", nargs="+", type=lambda x: int(x, 0))
p.add_argument("--assembly", default=r"G:\Steam\steamapps\common\ZED ZONE\GameAssembly.dll")
a = p.parse_args()
pe = pefile.PE(a.assembly, fast_load=True)
binary = bytes(pe.__data__)
base = pe.OPTIONAL_HEADER.ImageBase
name = binary.find(b"Assembly-CSharp.dll\0")
if name < 0:
    raise SystemExit("Assembly-CSharp codegen module name not found")
module_name_va = base + pe.get_rva_from_offset(name)
position = 0
modules = []
while True:
    position = binary.find(struct.pack("<Q", module_name_va), position)
    if position < 0:
        break
    _, count, pointers = struct.unpack_from("<QQQ", binary, position)
    if 1000 < count < 1000000 and base <= pointers < base + pe.OPTIONAL_HEADER.SizeOfImage:
        modules.append((pe.get_rva_from_offset(position), count, pointers - base))
    position += 8
if len(modules) != 1:
    raise SystemExit(f"Expected one validated codegen module, found {len(modules)}")
module, count, pointers = modules[0]
print(f"module=0x{module:X} methodCount={count} pointers=0x{pointers:X}")
for token in a.tokens:
    row = token & 0xFFFFFF
    if token >> 24 != 6 or not 1 <= row <= count:
        raise SystemExit(f"Invalid native MethodDef token: 0x{token:X}")
    pointer = struct.unpack("<Q", pe.get_data(pointers + 8 * (row - 1), 8))[0]
    print(f"0x{token:08X} -> " + (f"RVA 0x{pointer-base:X}" if pointer else "NULL/abstract"))
