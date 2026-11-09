using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using Il2CppInterop.Runtime;
namespace ZedZone.SmartCompanions.LiveSmoke
{
    internal static class NativeAddressDump
    {
        internal static void Write()
        {
            long baseAddress=0;
            foreach(ProcessModule m in Process.GetCurrentProcess().Modules)
                if(m.ModuleName.Equals("GameAssembly.dll",StringComparison.OrdinalIgnoreCase)) baseAddress=m.BaseAddress.ToInt64();
            if(baseAddress==0) throw new Exception("GameAssembly not loaded");
            var result=new Dictionary<string,object>();
            foreach(var type in new[]{typeof(GOAPAgent),typeof(BasicCharacterController),typeof(HumanCharacterController),typeof(GOAPAction_RangeAttack),typeof(GOAPAction_EquipRangedWeapon),typeof(GOAPGoal_EliminateThreat),typeof(GOAPAction),typeof(CharacterEquipmentData),typeof(ItemAttr_RangedWeapon)})
            {
                var methods=new Dictionary<string,string>();var fields=new Dictionary<string,uint>();
                foreach(var f in type.GetFields(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic))
                {
                    if(f.FieldType!=typeof(IntPtr)) continue;
                    var ptr=(IntPtr)f.GetValue(null);
                    if(ptr==IntPtr.Zero)continue;
                    if(f.Name.StartsWith("NativeMethodInfoPtr_"))
                        methods[f.Name]="0x"+(Marshal.ReadIntPtr(ptr).ToInt64()-baseAddress).ToString("X");
                    if(f.Name.StartsWith("NativeFieldInfoPtr_"))fields[f.Name]=IL2CPP.il2cpp_field_get_offset(ptr);
                }
                result[type.Name]=new{methods,fields};
            }
            File.WriteAllText(Path.Combine(LiveSmokePlugin.Lab,"native-addresses.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
            LiveSmokePlugin.Logger.LogInfo("[NativeDump] Wrote runtime method RVAs/field offsets for offline analysis.");
        }
    }
}
