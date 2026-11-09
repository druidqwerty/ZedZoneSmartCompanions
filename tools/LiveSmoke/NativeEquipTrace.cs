using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using HarmonyLib;
using UnityEngine;
namespace ZedZone.SmartCompanions.LiveSmoke
{
    [HarmonyPatch(typeof(HumanCharacterController), nameof(HumanCharacterController.EquipWeapon_WeaponSlot))]
    internal static class NativeEquipTrace
    {
        private static readonly float[] Next = new float[8];
        [DllImport("kernel32.dll", EntryPoint="RtlCaptureStackBackTrace")]
        private static extern ushort Capture(uint skip,uint count,[Out] IntPtr[] frames,IntPtr hash);
        private static void Prefix(HumanCharacterController __instance,int __0,ref string __state)
        {
            int key=Math.Max(0,Math.Min(7,__0));
            if(Time.unscaledTime<Next[key] || __instance?.characterData?.characterName!="orlov")return;
            Next[key]=Time.unscaledTime+1f;
            var eq=__instance.characterData.characterEquipmentData;
            var requested=eq.GetWeaponData(__0);
            System.IO.File.WriteAllText(System.IO.Path.Combine(LiveSmokePlugin.Lab,"slot-watch.json"),
                System.Text.Json.JsonSerializer.Serialize(new{pid=Process.GetCurrentProcess().Id,address=eq.Pointer.ToInt64()+0x80}));
            __state="requested="+__0+":"+requested?.itemId+" before="+eq.currentWeaponIndex+":"+eq.currentWeaponItemData?.itemId;
            try
            {
                long start=0,end=0;
                foreach(ProcessModule m in Process.GetCurrentProcess().Modules) if(m.ModuleName=="GameAssembly.dll") {start=m.BaseAddress.ToInt64();end=start+m.ModuleMemorySize;break;}
                var frames=new IntPtr[48];var count=Capture(0,48,frames,IntPtr.Zero);var trace=new StringBuilder();
                for(int i=0;i<count;i++) {long address=frames[i].ToInt64();if(address>=start&&address<end)trace.Append("0x").Append((address-start).ToString("X")).Append(' ');}
                __state+=" nativeCallers="+trace;
            }catch(Exception e){__state+=" trace="+e.Message;}
        }
        private static void Postfix(HumanCharacterController __instance,string __state)
        {
            if(__state==null)return;
            var eq=__instance.characterData.characterEquipmentData;var gun=__instance.rangedWeapon;
            LiveSmokePlugin.Logger.LogInfo("[NativeEquipTrace] "+__state+" after="+eq.currentWeaponIndex+":"+eq.currentWeaponItemData?.itemId+" gun="+gun?.weaponData?.itemId+" active="+(gun!=null&&gun.gameObject.activeInHierarchy));
        }
    }
}
