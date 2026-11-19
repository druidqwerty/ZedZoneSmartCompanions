using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
namespace ZedZone.SmartCompanions.LiveSmoke
{
    [BepInPlugin("zedzone.smartcompanions.livesmoke", "Smart Companions Live Lab", "1.0.0")]
    [BepInDependency("zedzone.smartcompanions")]
    public sealed class LiveSmokePlugin : BasePlugin
    {
        internal static string Lab;
        internal static string Original;
        internal static string OriginalBackslash;
        internal static string OriginalForward;
        internal static bool IsRealMenuLoadMode;
        internal static BepInEx.Logging.ManualLogSource Logger;
        internal static void Trace(string message)
        {
            try
            {
                if (string.IsNullOrEmpty(Lab)) return;
                string tracePath = Path.Combine(Lab, "live-trace.log");
                string line = $"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}{Environment.NewLine}";
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(line);
                using var fs = new FileStream(tracePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(flushToDisk: true);
            }
            catch { }
        }
        internal static void FlushLogs()
        {
            try
            {
                foreach (var listener in BepInEx.Logging.Logger.Listeners)
                {
                    if (listener is BepInEx.Logging.DiskLogListener disk && disk.LogWriter != null)
                        disk.LogWriter.Flush();
                }
            }
            catch (Exception ex)
            {
                Trace("FlushLogs exception: " + ex);
            }
        }
        public override void Load()
        {
            var args = Environment.GetCommandLineArgs();
            var arg = args.FirstOrDefault(a => a.StartsWith("--sc-live-profile="));
            if (arg == null) return;
            IsRealMenuLoadMode = args.Contains("--sc-real-menu-load");
            Lab = Path.GetFullPath(arg.Substring("--sc-live-profile=".Length)).TrimEnd('\\', '/');
            if (!Directory.Exists(Path.Combine(Lab,"GameData","GameData"))) throw new Exception("Missing copied lab profile");
            Original = Application.persistentDataPath;
            OriginalBackslash = Path.GetFullPath(Original).TrimEnd('\\', '/');
            OriginalForward = OriginalBackslash.Replace('\\', '/');
            Logger = Log;
            Application.runInBackground = true;
            new Harmony("zedzone.smartcompanions.livesmoke").PatchAll(typeof(LiveSmokePlugin).Assembly);
            if (IsRealMenuLoadMode)
            {
                AddComponent<RealMenuLoadRunner>();
                Logger.LogInfo("[RealMenuLoad] Save/config paths redirected to disposable copy; cloud saving disabled.");
            }
            else
            {
                AddComponent<LiveRunner>();
                Logger.LogInfo("[LiveLab] Save/config paths redirected to disposable copy; cloud saving disabled.");
            }
        }
    }
    [HarmonyPatch]
    internal static class RedirectPaths
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var m in typeof(GameConfigs).GetProperties(BindingFlags.Static | BindingFlags.Public)
                .Where(p => p.PropertyType == typeof(string) && p.Name.EndsWith("Path") && p.GetMethod != null && p.SetMethod == null)
                .Select(p => p.GetMethod))
            {
                yield return m;
            }

            const BindingFlags StaticAny = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var mpSaveDir = typeof(ZedZone.Net.NetCharacterSave).GetProperty("saveDirectory", StaticAny)?.GetMethod;
            if (mpSaveDir != null) yield return mpSaveDir;

            var mpProfilePath = typeof(ZedZone.Net.NetCharacterSave).GetMethod("ProfilePath", StaticAny);
            if (mpProfilePath != null) yield return mpProfilePath;

            var noticeCache = typeof(ZedZone.Net.NetServiceNotice).GetProperty("CachePath", StaticAny)?.GetMethod;
            if (noticeCache != null) yield return noticeCache;
        }
        static void Postfix(ref string __result)
        {
            if (string.IsNullOrEmpty(__result)) return;
            if (__result.StartsWith(LiveSmokePlugin.OriginalBackslash, StringComparison.OrdinalIgnoreCase))
                __result = LiveSmokePlugin.Lab + __result.Substring(LiveSmokePlugin.OriginalBackslash.Length);
            else if (__result.StartsWith(LiveSmokePlugin.OriginalForward, StringComparison.OrdinalIgnoreCase))
                __result = LiveSmokePlugin.Lab + __result.Substring(LiveSmokePlugin.OriginalForward.Length);
            else if (__result.StartsWith(LiveSmokePlugin.Original, StringComparison.OrdinalIgnoreCase))
                __result = LiveSmokePlugin.Lab + __result.Substring(LiveSmokePlugin.Original.Length);
        }
    }
    [HarmonyPatch(typeof(SteamCloudManager), nameof(SteamCloudManager.SteamCloudSave))]
    internal static class NoCloud { static bool Prefix() => false; }
    [HarmonyPatch(typeof(CompanionConfigStore), nameof(CompanionConfigStore.Save))]
    internal static class NoModConfigWrite { static bool Prefix() => false; }
    [HarmonyPatch(typeof(GameController), nameof(GameController.UpdateFocusAutoPause))]
    internal static class KeepLabRunning { static void Prefix(ref bool hasFocus) { hasFocus = true; } }
    [HarmonyPatch(typeof(GameController), "get_isSoftPaused")]
    internal static class UnpauseIsolatedLab
    {
        static bool Prefix(ref bool __result)
        {
            if (LiveSmokePlugin.IsRealMenuLoadMode) return true;
            __result = false;
            return false;
        }
    }
    [HarmonyPatch(typeof(Time), "set_timeScale")]
    internal static class PinLabTimeScale
    {
        static void Prefix(ref float value)
        {
            if (LiveSmokePlugin.IsRealMenuLoadMode) return;
            if (LiveRunner.WorldLoaded && value < 1f) value = 1f;
        }
    }
    public sealed class LiveRunner : MonoBehaviour
    {
        public LiveRunner(IntPtr ptr) : base(ptr) { }
        internal static bool WorldLoaded;
        private bool _loading, _loaded, _quitPending;
        private float _started, _nextReport;
        private int _quitDelayFrames, _quitCode;
        private void RequestQuit(int exitCode, string reason, bool captureScreenshot = true)
        {
            if (_quitPending) return;
            _quitPending = true;
            _quitCode = exitCode;
            _quitDelayFrames = 2;
            LiveSmokePlugin.Trace($"RequestQuit(code={exitCode}, screenshot={captureScreenshot}): {reason}");
            if (captureScreenshot)
                ScreenCapture.CaptureScreenshot(Path.Combine(LiveSmokePlugin.Lab, "live-world.png"));
            LiveSmokePlugin.FlushLogs();
        }
        private void LateUpdate() { if(_loaded && !_quitPending) Time.timeScale=1f; }
        private void Update()
        {
            if (_quitPending)
            {
                LiveSmokePlugin.FlushLogs();
                if (--_quitDelayFrames <= 0)
                {
                    LiveSmokePlugin.Trace($"Calling Application.Quit({_quitCode})");
                    enabled = false;
                    Application.Quit(_quitCode);
                }
                return;
            }
            QualitySettings.vSyncCount=0;
            Application.targetFrameRate=60;
            try
            {
                if(Environment.GetCommandLineArgs().Contains("--sc-dump-native"))
                {
                    if(Time.realtimeSinceStartup<5f)return;
                    NativeAddressDump.Write();RequestQuit(0,"--sc-dump-native complete",captureScreenshot:false);return;
                }
                if (!_loading)
                {
                    if (Time.realtimeSinceStartup < 9f || !GameController.initFinishFlag) return;
                    foreach (var p in new[]{GameConfigs.gameDataSavingPath,GameConfigs.mapDataSavingPath,GameConfigs.gameSettingsDataSavingPath})
                        if (!Path.GetFullPath(p).StartsWith(LiveSmokePlugin.Lab,StringComparison.OrdinalIgnoreCase)) throw new Exception("Unredirected save path: " + p);
                    var path = Directory.GetFiles(Path.Combine(LiveSmokePlugin.Lab,"GameData","GameData"),"*.game").OrderByDescending(File.GetLastWriteTimeUtc).First();
                    NativeAddressDump.Write();
                    var data = GameDataManager.instance.LoadGameDataFromPath(path);
                    if (data == null) throw new Exception("Unable to load copied world");
                    _loading = true; _started = Time.realtimeSinceStartup;
                    LiveSmokePlugin.Logger.LogInfo("[LiveLab] Loading copied world.");
                    GameController.instance.gameData = data;
                    GameController.instance.LoadScene("Gaming");
                    return;
                }
                var gc = GameController.instance;
                if (Il2CppSafe.IsAliveUnity(gc?.playerCharacter) && Il2CppSafe.IsAliveUnity(InGameController.instance))
                {
                    Time.timeScale=1f;
                    QualitySettings.vSyncCount=0;
                    Application.targetFrameRate=60;
                    if (!SmartCompanionsRuntimeGate.IsGameplayWorldReady())
                        return;
                    var cp = GenericConfirmPanel._instance;
                    if (Il2CppSafe.IsAliveUnity(cp) && (cp.isVisible || Time.frameCount <= GenericConfirmPanel.s_lastHideFrame + 1))
                    {
                        if (cp.isVisible)
                        {
                            LiveSmokePlugin.Logger.LogInfo("[LiveLab] Dismissing in-game GenericConfirmPanel via OnCancelClick/Hide.");
                            if (cp.onCancel != null) cp.OnCancelClick();
                            else if (cp.onRequiredDismiss != null) cp.OnConfirmClick();
                            else cp.Hide();
                        }
                        return;
                    }
                    if (!_loaded)
                    {
                        _loaded=true;
                        WorldLoaded=true;
                        Time.timeScale=1f;
                        Application.runInBackground=true;
                        GameController.UpdateFocusAutoPause(true);
                        InGameController.instance.ResumeGame();
                        if (Il2CppSafe.IsAliveUnity(LootInventoryPanel._instance))
                            throw new Exception("v3.0.2 regression: LootInventoryPanel._instance was prematurely instantiated on world boot!");
                        if (Il2CppSafe.IsAliveUnity(CharacterInput.instance) && CharacterInput.instance.stopAllInput)
                            throw new Exception("v3.0.2 regression: CharacterInput.instance.stopAllInput remained stuck true on world boot!");
                        LiveSmokePlugin.Logger.LogInfo("[LiveLab] WORLD READY player="+gc.playerCharacter.transform.position+" (LootInventoryPanel._instance=null, stopAllInput=false verified)");
                    }
                    if (Time.realtimeSinceStartup >= _nextReport)
                    {
                        _nextReport=Time.realtimeSinceStartup+5f;
                        var squad=SmartChatterSystem.GetActiveTeammateAgents();
                        var mgr=GOAPNPCManager.instance;
                        LiveSmokePlugin.Logger.LogInfo("[LiveLab] Squad="+squad.Count+" characters="+gc.characterList.Count+" timeScale="+Time.timeScale+" savedNPCs="+gc.gameData.npcList.Count+" cached="+mgr.allNPCDataCache.Count+" active="+mgr.activeNPCs.Count+" team="+mgr.teammateNPCs.Count);
                        if(squad.Count==0)
                        {
                            foreach(var entry in mgr.allNPCDataCache)
                            {
                                var npc=entry.Value;
                                var name=npc.characterData?.characterName??"";
                                if(name!="Time" && name!="orlov" && name!="Jason") continue;
                                npc.leaderCharacterGUID=gc.playerCharacter.characterData.characterId;
                                gc.playerCharacter.characterData.teamNPCGuids.Add(npc.npcGUID);
                                mgr.teammateNPCs.Add(npc);
                                npc.npcPosition=(Vector2)gc.playerCharacter.transform.position+Vector2.right*3f;
                                LiveSmokePlugin.Logger.LogInfo("[LiveLab] Bind "+name+" HP="+npc.health+" offline="+npc.offlineNPCState+" prefab="+npc.characterData.characterAttrId);
                                GOAPNPCManager.RestoreTeammateFullHealth(npc,"isolated combat lab");
                                mgr.InitNPC(npc);
                                mgr.DrainPendingInit();
                            }
                        }
                        foreach(var ag in squad) LiveSmokePlugin.Logger.LogInfo("[LiveLab] NPC="+ag.npcData.characterData.characterName+" pos="+ag.humanCharacter.transform.position+" combat="+ag.npcData.combatCommand+" weaponPref="+ag.npcData.weaponPreference+" ranged="+(ag.humanCharacter.rangedWeapon!=null)+" equipment="+ag.humanCharacter.characterData.characterEquipmentData.Pointer);
                    }
                    if(Environment.GetCommandLineArgs().Contains("--sc-equip-trace") && Time.realtimeSinceStartup-_started>17f)
                    {
                        LiveSmokePlugin.Logger.LogInfo("[NativeEquipTrace] Diagnostic-only run complete; not an acceptance test.");
                        RequestQuit(0,"--sc-equip-trace complete",captureScreenshot:false);return;
                    }
                    if(CombatLiveRegression.Tick())
                    {
                        if(!CombatLiveRegression.Succeeded)
                        {
                            RequestQuit(1,"CombatLiveRegression failed");return;
                        }
                        if(PhysicalCarryLiveRegression.Tick())
                        {
                            if(!PhysicalCarryLiveRegression.Succeeded)
                            {
                                RequestQuit(1,"PhysicalCarryLiveRegression failed");return;
                            }
                            if(TacticalLiveRegression.Tick())
                            {
                                if(!TacticalLiveRegression.Succeeded)
                                {
                                    RequestQuit(1,"TacticalLiveRegression failed");return;
                                }
                                if(SoakAndScaleLiveRegression.Tick())
                                {
                                    if(!SoakAndScaleLiveRegression.Succeeded)
                                    {
                                        RequestQuit(1,"SoakAndScaleLiveRegression failed");return;
                                    }
                                    if(GarrisonLiveRegression.Tick())
                                    {
                                        if(!GarrisonLiveRegression.Succeeded)
                                        {
                                            RequestQuit(1,"GarrisonLiveRegression failed");return;
                                        }
                                        if(V312LiveRegression.Tick())
                                        {
                                            RequestQuit(V312LiveRegression.Succeeded ? 0 : 1, $"V312LiveRegression Finished (Succeeded={V312LiveRegression.Succeeded})");return;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if(Time.realtimeSinceStartup-_started>105f)
                {
                    if(!_loaded) throw new Exception("World did not load within 105 seconds");
                    const string timeoutMsg = "[LiveLab] FAIL Overall 105s timeout reached before acceptance completed.";
                    LiveSmokePlugin.Logger.LogError(timeoutMsg);
                    LiveSmokePlugin.Trace(timeoutMsg);
                    RequestQuit(1, timeoutMsg);
                }
            }
            catch(Exception e)
            {
                string failMsg = "[LiveLab] FAIL " + e;
                LiveSmokePlugin.Logger.LogError(failMsg);
                LiveSmokePlugin.Trace(failMsg);
                LiveSmokePlugin.FlushLogs();
                RequestQuit(1, "LiveRunner catch: " + e.GetType().Name + ": " + e.Message, captureScreenshot:false);
            }
        }
    }
}
