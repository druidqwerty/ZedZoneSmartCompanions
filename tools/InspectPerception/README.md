# Read-only native inspection

These tools inspect the owner's locally installed game. Do not commit native code,
the game DLL, or generated disassembly. Keep such output in ignored `backups/`.

1. Extract original IL2CPP MethodDef tokens from interop initialization:

   `dotnet run --project tools/InspectPerception -- GOAPAction_EquipRangedWeapon`

2. Resolve one or more original tokens through the PE's Assembly-CSharp codegen module:

   `python tools/InspectPerception/resolve.py 0x06000CC9 0x06000CCA`

3. Disassemble a bounded native function range using the printed RVA:

   `python tools/InspectPerception/disassemble.py 0x5517E0 --size 2640 --out backups/equip-execute.txt`

Cross-check at least one known RVA against a live IL2CPP MethodInfo pointer before
using a new game build's results. Do not confuse regenerated wrapper method tokens
with the original tokens passed to `IL2CPP.GetIl2CppMethodByToken`.

Requires the already installed .NET 8 SDK, Mono.Cecil from BepInEx, and Python
packages `pefile` and `capstone`. No game launch, runtime mutation or native patching
is performed by these inspection tools.
