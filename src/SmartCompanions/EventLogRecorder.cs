using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZedZone.SmartCompanions
{
    /// <summary>
    /// Opt-in runtime event & state-transition recorder for player bug reports.
    /// Zero per-frame overhead when disabled; bounded file size with automatic rotation;
    /// privacy-safe (never writes OS paths, usernames, Steam IDs, or character names).
    /// </summary>
    public static class EventLogRecorder
    {
        public const string LogFileName = "SmartCompanions_EventLog.log";
        public const string PrevLogFileName = "SmartCompanions_EventLog.prev.log";
        public const long MaxLogFileBytes = 4L * 1024L * 1024L; // 4 MiB
        private const int RingBufferCapacity = 128;
        private const float FlushIntervalSec = 1.5f;
        private const float ObserverIntervalSec = 1.0f;
        private const float PulseIntervalSec = 30.0f;
        private const float StallWindowSec = 4.0f;
        private const float StallMinMoveDist = 0.35f;
        private const float StallCooldownSec = 6.0f;

        private static readonly object Sync = new object();
        private static readonly List<string> PendingLines = new List<string>(64);
        private static readonly string[] RingBuffer = new string[RingBufferCapacity];
        private static int _ringHead;
        private static int _ringCount;

        private static readonly Dictionary<string, float> DedupeLastAt =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> NpcAliasByGuid =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, NpcWatchState> WatchByGuid =
            new Dictionary<string, NpcWatchState>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> SeenThisTick =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> RemovedGuidsScratch = new List<string>(16);

        private static int _nextNpcOrdinal = 1;
        private static bool _sessionHeaderWritten;
        private static float _lastFlushAt = -999f;
        private static float _lastObserveAt = -999f;
        private static float _lastPulseAt = -999f;
        private static string _lastConsecutiveKey = "";
        private static float _lastConsecutiveAt = -999f;
        private static int _suppressedConsecutiveCount;

        // World-level delta tracking
        private static bool _worldInit;
        private static bool _lastWorldReady;
        private static bool _lastSimActive;
        private static string _lastSceneName = "";
        private static bool _lastPlayerDriving;
        private static int _lastSquadCount = -1;
        private static bool _lastRaining;
        private static bool _lastBloodRaining;

        public static long SessionEventCount { get; private set; }
        public static long CachedFileSizeBytes { get; private set; }
        public static int BugMarkCount { get; private set; }

        public static bool IsEnabled => CompanionConfigStore.EnableEventLogging;

        public static string LogFilePath => Path.Combine(Paths.ConfigPath, LogFileName);
        public static string PrevLogFilePath => Path.Combine(Paths.ConfigPath, PrevLogFileName);
        public static string DisplayRelativePath => "BepInEx/config/" + LogFileName;

        private sealed class NpcWatchState
        {
            public string Alias;
            public string Signature;
            public Vector2 LastMovedPos;
            public float LastMovedAt;
            public float LastStallLogAt;
            public sbyte LastZ;
            public bool LastDead;
            public bool LastInVehicle;
            public int LastHpBucket;
        }

        public static void SetEnabled(bool enabled, string source = "ui")
        {
            bool prev = CompanionConfigStore.EnableEventLogging;
            CompanionConfigStore.EnableEventLogging = enabled;

            if (enabled)
            {
                if (!prev || !_sessionHeaderWritten)
                {
                    BeginSession(source);
                }
                CompanionConfigStore.Save();
                Flush(force: true);
            }
            else
            {
                if (prev)
                {
                    WriteRawLine(FormatLine("REC", $"Event logging DISABLED (source={source}, sessionEvents={SessionEventCount})."));
                    Flush(force: true);
                }
                _sessionHeaderWritten = false;
                ResetObserverState();
                CompanionConfigStore.Save();
            }
        }

        public static void OnConfigLoaded()
        {
            RefreshCachedFileSize();
            if (IsEnabled && !_sessionHeaderWritten)
            {
                BeginSession("config_autoload");
            }
        }

        private static void BeginSession(string source)
        {
            _sessionHeaderWritten = true;
            ResetObserverState();

            string header = BuildSessionHeader(source);
            lock (Sync)
            {
                PendingLines.Add(header);
                // Replay recent in-memory ring-buffer context so toggling ON right after a glitch still preserves lead-up.
                if (_ringCount > 0)
                {
                    PendingLines.Add("--- Recent pre-enable context (in-memory ring buffer) ---");
                    int start = (_ringHead - _ringCount + RingBufferCapacity) % RingBufferCapacity;
                    for (int i = 0; i < _ringCount; i++)
                    {
                        string entry = RingBuffer[(start + i) % RingBufferCapacity];
                        if (!string.IsNullOrEmpty(entry))
                            PendingLines.Add("  [PRE] " + entry);
                    }
                    PendingLines.Add("--- Live event stream ---");
                }
            }
            SessionEventCount++;
            WriteWorldAndSquadSnapshot("SESSION_START");
        }

        private static void ResetObserverState()
        {
            WatchByGuid.Clear();
            _worldInit = false;
            _lastObserveAt = -999f;
            _lastPulseAt = -999f;
        }

        public static string GetNpcAlias(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return "NPC#??";
            lock (Sync)
            {
                if (NpcAliasByGuid.TryGetValue(guid, out string existing))
                    return existing;
                uint h = 2166136261u;
                for (int i = 0; i < guid.Length; i++)
                {
                    h ^= guid[i];
                    h *= 16777619u;
                }
                string alias = $"NPC#{_nextNpcOrdinal:00}({(h & 0xFFFFu):x4})";
                _nextNpcOrdinal++;
                NpcAliasByGuid[guid] = alias;
                return alias;
            }
        }

        /// <summary>
        /// Logs a structured event. Safe to call from any subsystem:
        /// always records high-value events into the tiny in-memory ring buffer;
        /// writes to disk buffer only when EnableEventLogging is ON.
        /// </summary>
        public static void Log(string category, string message, string dedupeKey = null, float minIntervalSec = 0f, bool immediateFlush = false)
        {
            try
            {
                if (string.IsNullOrEmpty(message)) return;
                string cat = string.IsNullOrEmpty(category) ? "EVT" : category;
                float now = SafeUnscaledTime();

                if (!string.IsNullOrEmpty(dedupeKey) && minIntervalSec > 0f)
                {
                    lock (Sync)
                    {
                        if (DedupeLastAt.TryGetValue(dedupeKey, out float prevAt) && now - prevAt < minIntervalSec)
                            return;
                        if (DedupeLastAt.Count > 512) DedupeLastAt.Clear();
                        DedupeLastAt[dedupeKey] = now;
                    }
                }

                string clean = SanitizeSingleLine(message);
                string line = FormatLine(cat, clean);

                lock (Sync)
                {
                    string consecutiveKey = cat + "|" + clean;
                    if (consecutiveKey == _lastConsecutiveKey && now - _lastConsecutiveAt < 2.0f)
                    {
                        _suppressedConsecutiveCount++;
                        return;
                    }

                    if (_suppressedConsecutiveCount > 0)
                    {
                        string supLine = FormatLine("DEDUPE", $"Previous line repeated {_suppressedConsecutiveCount} more time(s).");
                        PushRing(supLine);
                        if (IsEnabled)
                        {
                            if (!_sessionHeaderWritten) PendingLines.Add(BuildSessionHeader("auto"));
                            _sessionHeaderWritten = true;
                            PendingLines.Add(supLine);
                        }
                        _suppressedConsecutiveCount = 0;
                    }

                    _lastConsecutiveKey = consecutiveKey;
                    _lastConsecutiveAt = now;

                    PushRing(line);

                    if (IsEnabled)
                    {
                        if (!_sessionHeaderWritten)
                        {
                            _sessionHeaderWritten = true;
                            PendingLines.Add(BuildSessionHeader("auto"));
                        }
                        PendingLines.Add(line);
                        SessionEventCount++;
                    }
                }

                if (immediateFlush && IsEnabled)
                {
                    Flush(force: true);
                }
            }
            catch { }
        }

        /// <summary>
        /// Writes a prominent user bug marker + full live world & squad snapshot and flushes to disk immediately.
        /// Works even if continuous logging was OFF (dumps the in-memory ring buffer + snapshot).
        /// </summary>
        public static bool MarkBugNow(string note = "user_button")
        {
            try
            {
                BugMarkCount++;
                bool wasEnabled = IsEnabled;

                lock (Sync)
                {
                    if (!wasEnabled)
                    {
                        PendingLines.Add(BuildSessionHeader("manual_bug_mark"));
                        if (_ringCount > 0)
                        {
                            PendingLines.Add("--- Recent context before bug marker (in-memory ring buffer) ---");
                            int start = (_ringHead - _ringCount + RingBufferCapacity) % RingBufferCapacity;
                            for (int i = 0; i < _ringCount; i++)
                            {
                                string entry = RingBuffer[(start + i) % RingBufferCapacity];
                                if (!string.IsNullOrEmpty(entry))
                                    PendingLines.Add("  [CTX] " + entry);
                            }
                        }
                    }

                    PendingLines.Add($"==================== [BUG_MARK #{BugMarkCount}] ({note}) ====================");
                    PendingLines.Add(FormatLine("BUG_MARK", $"User triggered bug marker #{BugMarkCount} (continuousLogging={wasEnabled})."));
                    SessionEventCount++;
                }

                WriteWorldAndSquadSnapshot($"BUG_MARK #{BugMarkCount}", forceWriteToPending: true);
                return Flush(force: true);
            }
            catch
            {
                return false;
            }
        }

        public static bool ClearLogFile()
        {
            try
            {
                lock (Sync)
                {
                    PendingLines.Clear();
                    _suppressedConsecutiveCount = 0;
                    _lastConsecutiveKey = "";
                    SessionEventCount = 0;
                    BugMarkCount = 0;
                }

                if (File.Exists(LogFilePath)) File.Delete(LogFilePath);
                if (File.Exists(PrevLogFilePath)) File.Delete(PrevLogFilePath);
                CachedFileSizeBytes = 0;

                if (IsEnabled)
                {
                    _sessionHeaderWritten = false;
                    BeginSession("cleared");
                    Flush(force: true);
                }
                return true;
            }
            catch (Exception e)
            {
                SmartCompanionsPlugin.LogWarning("EventLogRecorder.ClearLogFile failed: " + e.Message);
                return false;
            }
        }

        public static bool OpenLogFolder()
        {
            try
            {
                Directory.CreateDirectory(Paths.ConfigPath);
                string target = File.Exists(LogFilePath) ? LogFilePath : Paths.ConfigPath;
                if (File.Exists(LogFilePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{target}\"",
                        UseShellExecute = true
                    });
                }
                else
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Paths.ConfigPath,
                        UseShellExecute = true
                    });
                }
                return true;
            }
            catch
            {
                try { GUIUtility.systemCopyBuffer = LogFilePath; } catch { }
                return false;
            }
        }

        /// <summary>
        /// Periodic observer called from SmartCompanionsController.Update().
        /// Does nothing when EnableEventLogging is OFF.
        /// </summary>
        public static void Tick()
        {
            if (!IsEnabled) return;
            try
            {
                float now = SafeUnscaledTime();
                if (now - _lastObserveAt >= ObserverIntervalSec)
                {
                    _lastObserveAt = now;
                    ObserveWorldAndCompanions(now);
                }

                if (now - _lastFlushAt >= FlushIntervalSec)
                {
                    Flush(force: false);
                }
            }
            catch { }
        }

        public static bool Flush(bool force)
        {
            List<string> batch = null;
            lock (Sync)
            {
                if (PendingLines.Count == 0)
                {
                    _lastFlushAt = SafeUnscaledTime();
                    return true;
                }
                if (!force && SafeUnscaledTime() - _lastFlushAt < FlushIntervalSec)
                    return true;

                batch = new List<string>(PendingLines);
                PendingLines.Clear();
                _lastFlushAt = SafeUnscaledTime();
            }

            try
            {
                Directory.CreateDirectory(Paths.ConfigPath);
                RotateIfOversized();

                var sb = new StringBuilder(batch.Count * 96);
                for (int i = 0; i < batch.Count; i++)
                    sb.AppendLine(batch[i]);

                File.AppendAllText(LogFilePath, sb.ToString(), new UTF8Encoding(false));
                RefreshCachedFileSize();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void RotateIfOversized()
        {
            try
            {
                if (!File.Exists(LogFilePath)) return;
                var info = new FileInfo(LogFilePath);
                if (info.Length < MaxLogFileBytes) return;

                if (File.Exists(PrevLogFilePath))
                    File.Delete(PrevLogFilePath);
                File.Move(LogFilePath, PrevLogFilePath);

                string rotNote = FormatLine("ROTATE", $"Log exceeded {MaxLogFileBytes / (1024 * 1024)} MiB; rotated to {PrevLogFileName}.") +
                                 Environment.NewLine + BuildSessionHeader("log_rotate") + Environment.NewLine;
                File.WriteAllText(LogFilePath, rotNote, new UTF8Encoding(false));
            }
            catch { }
        }

        private static void RefreshCachedFileSize()
        {
            try
            {
                CachedFileSizeBytes = File.Exists(LogFilePath) ? new FileInfo(LogFilePath).Length : 0L;
            }
            catch { CachedFileSizeBytes = 0L; }
        }

        private static void ObserveWorldAndCompanions(float now)
        {
            bool worldReady = SmartCompanionsRuntimeGate.IsGameplayWorldReady();
            bool simActive = SmartCompanionsRuntimeGate.IsGameplaySimulationActive();
            string sceneName = "";
            try { sceneName = SceneManager.GetActiveScene().name ?? ""; } catch { }

            var gc = GameController.instance;
            var player = Il2CppSafe.IsAliveUnity(gc) ? gc.playerCharacter : null;
            bool playerDriving = Il2CppSafe.IsAliveUnity(player) &&
                                 (player.isDriving || Il2CppSafe.IsAliveUnity(player.drivingVehicle));
            bool raining = false, bloodRain = false;
            try
            {
                if (Il2CppSafe.IsAliveUnity(gc) && Il2CppSafe.IsAlive(gc.gameData))
                {
                    raining = gc.gameData.isRaining;
                    bloodRain = gc.gameData.isBloodRaining;
                }
            }
            catch { }

            var teammates = worldReady ? SmartChatterSystem.GetActiveTeammateAgents() : null;
            int squadCount = teammates != null ? teammates.Count : 0;

            if (!_worldInit ||
                worldReady != _lastWorldReady ||
                simActive != _lastSimActive ||
                sceneName != _lastSceneName ||
                playerDriving != _lastPlayerDriving ||
                squadCount != _lastSquadCount ||
                raining != _lastRaining ||
                bloodRain != _lastBloodRaining)
            {
                _worldInit = true;
                _lastWorldReady = worldReady;
                _lastSimActive = simActive;
                _lastSceneName = sceneName;
                _lastPlayerDriving = playerDriving;
                _lastSquadCount = squadCount;
                _lastRaining = raining;
                _lastBloodRaining = bloodRain;

                Log("WORLD",
                    $"scene={sceneName} ready={(worldReady ? 1 : 0)} sim={(simActive ? 1 : 0)} " +
                    $"squad={squadCount} playerDriving={(playerDriving ? 1 : 0)} rain={(raining ? 1 : 0)} bloodRain={(bloodRain ? 1 : 0)} " +
                    $"timeScale={Time.timeScale.ToString("F2", CultureInfo.InvariantCulture)}");
            }

            if (!worldReady || teammates == null) return;

            Vector2 playerPos = Il2CppSafe.IsAliveUnity(player) ? (Vector2)player.transform.position : Vector2.zero;
            bool simRunning = simActive && Time.timeScale >= 0.5f;

            SeenThisTick.Clear();
            for (int i = 0; i < teammates.Count; i++)
            {
                var ag = teammates[i];
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData)) continue;
                string guid = ag.npcData.npcGUID;
                if (string.IsNullOrEmpty(guid)) continue;
                SeenThisTick.Add(guid);

                ObserveSingleCompanion(ag, guid, playerPos, now, simRunning);
            }

            // Detect companions leaving active teammate set
            RemovedGuidsScratch.Clear();
            foreach (var kv in WatchByGuid)
            {
                if (!SeenThisTick.Contains(kv.Key))
                    RemovedGuidsScratch.Add(kv.Key);
            }
            for (int i = 0; i < RemovedGuidsScratch.Count; i++)
            {
                string removedGuid = RemovedGuidsScratch[i];
                if (WatchByGuid.TryGetValue(removedGuid, out var oldW))
                {
                    CompanionConfigStore.CompanionStates.TryGetValue(removedGuid, out var st);
                    string roster = st != null ? st.RosterMode.ToString() : "?";
                    string dismissed = st != null ? (st.IsDismissedOrNotInTeam ? "1" : "0") : "?";
                    Log("SQUAD", $"{oldW.Alias} left active teammate list (roster={roster}, dismissed={dismissed}).");
                    WatchByGuid.Remove(removedGuid);
                }
            }

            if (now - _lastPulseAt >= PulseIntervalSec)
            {
                _lastPulseAt = now;
                WriteWorldAndSquadSnapshot("PULSE");
            }
        }

        private static void ObserveSingleCompanion(GOAPAgent ag, string guid, Vector2 playerPos, float now, bool simRunning)
        {
            var hc = ag.humanCharacter;
            if (!Il2CppSafe.IsAliveUnity(hc)) return;

            var st = CompanionConfigStore.GetOrCreateCompanion(guid);
            string alias = GetNpcAlias(guid);

            Vector2 pos = ag.transform.position;
            float distToPlayer = Vector2.Distance(pos, playerPos);
            sbyte z = ag.npcData.zLevel;
            bool dead = hc.deadFlag;
            bool inVeh = hc.isDriving || Il2CppSafe.IsAliveUnity(hc.drivingVehicle);

            float hp = 0f, maxHp = 100f;
            try
            {
                var status = Il2CppSafe.IsAlive(hc.characterData) ? hc.characterData.characterStatusData : null;
                if (Il2CppSafe.IsAlive(status))
                {
                    hp = status.hp;
                    maxHp = Mathf.Max(1f, status.maxhp);
                }
                else
                {
                    hp = ag.npcData.health;
                    maxHp = Mathf.Max(1f, ag.npcData.maxHealth);
                }
            }
            catch { }
            int hpPct = Mathf.Clamp(Mathf.RoundToInt((hp / maxHp) * 100f), 0, 100);
            int hpBucket = hpPct <= 25 ? hpPct / 5 * 5 : hpPct / 15 * 15;

            string goalName = ReadIl2CppShortTypeName(ag.currentGoal);
            string actionName = ReadCurrentActionName(ag);
            bool hasNav = ag.hasNavigationTarget;
            bool hasThreat = Il2CppSafe.IsAliveUnity(ag.mostThreateningCharacter) && !ag.mostThreateningCharacter.deadFlag;
            int weaponId = ReadEquippedWeaponId(hc);
            int carryCount = st.CarryBuffer != null ? st.CarryBuffer.Count : 0;
            bool busy = st.HasAnyPhysicalTask;

            string sig = $"{st.RosterMode}|{st.ActiveOrder}|{st.OrderBlockReason}|{st.LastBlockReason}|{st.WorkRole}|{st.Doctrine}|" +
                         $"g={goalName}|a={actionName}|nav={(hasNav ? 1 : 0)}|thr={(hasThreat ? 1 : 0)}|wpn={weaponId}|" +
                         $"busy={(busy ? 1 : 0)}|carry={carryCount}|veh={(inVeh ? 1 : 0)}|z={z}|dead={(dead ? 1 : 0)}|hpB={hpBucket}|" +
                         $"ovf={(st.IsTransientVehicleOverflow ? 1 : 0)}";

            if (!WatchByGuid.TryGetValue(guid, out var w))
            {
                w = new NpcWatchState
                {
                    Alias = alias,
                    Signature = sig,
                    LastMovedPos = pos,
                    LastMovedAt = now,
                    LastStallLogAt = -999f,
                    LastZ = z,
                    LastDead = dead,
                    LastInVehicle = inVeh,
                    LastHpBucket = hpBucket
                };
                WatchByGuid[guid] = w;
                Log("SQUAD",
                    $"{alias} joined watch: pos=({pos.x:F1},{pos.y:F1}) z={z} dist={distToPlayer:F1}m hp={hpPct}% " +
                    $"roster={st.RosterMode} order={st.ActiveOrder} role={st.WorkRole} doc={st.Doctrine} goal={goalName} act={actionName}");
                return;
            }

            if (sig != w.Signature)
            {
                w.Signature = sig;
                Log("NPC_STATE",
                    $"{alias} pos=({pos.x:F1},{pos.y:F1}) z={z} dist={distToPlayer:F1}m hp={hpPct}% " +
                    $"roster={st.RosterMode} order={st.ActiveOrder} ordBlk={st.OrderBlockReason} wrkBlk={st.LastBlockReason} " +
                    $"goal={goalName} act={actionName} nav={(hasNav ? 1 : 0)} thr={(hasThreat ? 1 : 0)} " +
                    $"wpn={weaponId} busy={(busy ? 1 : 0)} carry={carryCount} veh={(inVeh ? 1 : 0)} dead={(dead ? 1 : 0)}");
            }

            // Movement & stall detection
            if (Vector2.Distance(pos, w.LastMovedPos) >= StallMinMoveDist || z != w.LastZ || inVeh != w.LastInVehicle)
            {
                w.LastMovedPos = pos;
                w.LastMovedAt = now;
                w.LastZ = z;
                w.LastInVehicle = inVeh;
            }
            else if (simRunning && !dead && !inVeh && (hasNav || busy) &&
                     now - w.LastMovedAt >= StallWindowSec &&
                     now - w.LastStallLogAt >= StallCooldownSec)
            {
                w.LastStallLogAt = now;
                Vector2 navTarget = Vector2.zero;
                try { navTarget = ag.navigationTarget; } catch { }
                Log("STALL",
                    $"{alias} stalled for {(now - w.LastMovedAt):F1}s at ({pos.x:F1},{pos.y:F1}) z={z} " +
                    $"navTarget=({navTarget.x:F1},{navTarget.y:F1}) distPlayer={distToPlayer:F1}m " +
                    $"goal={goalName} act={actionName} order={st.ActiveOrder} wrkBlk={st.LastBlockReason} busy={(busy ? 1 : 0)} carry={carryCount}");
            }
        }

        private static void WriteWorldAndSquadSnapshot(string tag, bool forceWriteToPending = false)
        {
            try
            {
                var sb = new StringBuilder(512);
                bool worldReady = SmartCompanionsRuntimeGate.IsGameplayWorldReady();
                bool simActive = SmartCompanionsRuntimeGate.IsGameplaySimulationActive();
                string netMode = MultiplayerSyncSystem.IsMultiplayerHost ? "Host"
                    : MultiplayerSyncSystem.IsMultiplayerClient ? "Client" : "SP";

                sb.Append($"[{tag}] ready={(worldReady ? 1 : 0)} sim={(simActive ? 1 : 0)} net={netMode} ");
                sb.Append($"form={CompanionConfigStore.ActiveFormation} fdist={CompanionConfigStore.FollowDistance} ");
                sb.Append($"basePaused={(CompanionConfigStore.BaseWorkPaused ? 1 : 0)} prio={CompanionConfigStore.BasePriorityMode} ");
                sb.Append($"repair={CompanionConfigStore.AutoRepairMode} boxes={BaseSortingSystem.CachedContainers.Count}");

                var gc = GameController.instance;
                var player = Il2CppSafe.IsAliveUnity(gc) ? gc.playerCharacter : null;
                Vector2 pPos = Vector2.zero;
                if (Il2CppSafe.IsAliveUnity(player))
                {
                    pPos = player.transform.position;
                    sbyte pz = Il2CppSafe.IsAlive(player.characterData) ? player.characterData.zLevel : (sbyte)0;
                    bool pDrv = player.isDriving || Il2CppSafe.IsAliveUnity(player.drivingVehicle);
                    sb.Append($" | player=({pPos.x:F1},{pPos.y:F1}) z={pz} drv={(pDrv ? 1 : 0)}");
                }

                WriteLineInternal("SNAPSHOT", sb.ToString(), forceWriteToPending);

                if (!worldReady) return;
                var mates = SmartChatterSystem.GetActiveTeammateAgents();
                for (int i = 0; i < mates.Count && i < 15; i++)
                {
                    var ag = mates[i];
                    if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData)) continue;
                    var hc = ag.humanCharacter;
                    string guid = ag.npcData.npcGUID ?? "";
                    string alias = GetNpcAlias(guid);
                    var st = CompanionConfigStore.GetOrCreateCompanion(guid);
                    Vector2 pos = ag.transform.position;
                    float dist = Vector2.Distance(pos, pPos);
                    string goalName = ReadIl2CppShortTypeName(ag.currentGoal);
                    string actName = ReadCurrentActionName(ag);
                    bool inVeh = Il2CppSafe.IsAliveUnity(hc) && (hc.isDriving || Il2CppSafe.IsAliveUnity(hc.drivingVehicle));
                    int wpn = Il2CppSafe.IsAliveUnity(hc) ? ReadEquippedWeaponId(hc) : 0;

                    WriteLineInternal("SNAP_NPC",
                        $"{alias} pos=({pos.x:F1},{pos.y:F1}) z={ag.npcData.zLevel} dist={dist:F1}m " +
                        $"roster={st.RosterMode} ft={st.Fireteam} order={st.ActiveOrder} ordBlk={st.OrderBlockReason} wrkBlk={st.LastBlockReason} " +
                        $"role={st.WorkRole} doc={st.Doctrine} goal={goalName} act={actName} nav={(ag.hasNavigationTarget ? 1 : 0)} " +
                        $"wpn={wpn} veh={(inVeh ? 1 : 0)} busy={(st.HasAnyPhysicalTask ? 1 : 0)} carry={(st.CarryBuffer?.Count ?? 0)}",
                        forceWriteToPending);
                }
            }
            catch { }
        }

        private static void WriteLineInternal(string cat, string msg, bool forceWriteToPending)
        {
            string line = FormatLine(cat, SanitizeSingleLine(msg));
            lock (Sync)
            {
                PushRing(line);
                if (IsEnabled || forceWriteToPending)
                {
                    PendingLines.Add(line);
                    SessionEventCount++;
                }
            }
        }

        private static void WriteRawLine(string line)
        {
            lock (Sync)
            {
                PendingLines.Add(line);
            }
        }

        private static void PushRing(string line)
        {
            RingBuffer[_ringHead] = line;
            _ringHead = (_ringHead + 1) % RingBufferCapacity;
            if (_ringCount < RingBufferCapacity) _ringCount++;
        }

        private static string BuildSessionHeader(string source)
        {
            bool avo = CompanionConfigStore.IsAvoExpandedLoaded(out string avoSource);
            string netMode = MultiplayerSyncSystem.IsMultiplayerHost ? "Host"
                : MultiplayerSyncSystem.IsMultiplayerClient ? "Client" : "Singleplayer";
            var sb = new StringBuilder(512);
            sb.AppendLine("================================================================================");
            sb.AppendLine($"=== ZED ZONE: Smart Companions Event Log | v{SmartCompanionsPlugin.PluginVersion} | source={source} ===");
            sb.AppendLine($"StartedUtc: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z | Unity: {Application.unityVersion} | Screen: {Screen.width}x{Screen.height} | Scale: {SmartCompanionsUI.EffectiveUiScale:F2}");
            sb.AppendLine($"Lang: {CompanionConfigStore.LanguageMode}->{CompanionConfigStore.EffectiveLanguage} | Net: {netMode} | AVO: {avo}({avoSource}) | Patches: {PerformanceAndDiagnostics.LastPatchedMethodCount} (fail={SafeHarmonyPatcher.FailedPatchCount})");
            sb.AppendLine($"Settings: Combat={CompanionConfigStore.EnableSmartCombat} AntiFF={CompanionConfigStore.EnableAntiFriendlyFire} Kite={CompanionConfigStore.EnableKiting} Form={CompanionConfigStore.ActiveFormation} FDist={CompanionConfigStore.FollowDistance} Repair={CompanionConfigStore.AutoRepairMode} Harvest={CompanionConfigStore.AutoHarvestCrops} MaxTeam={CompanionConfigStore.MaxTeamSize}");
            sb.AppendLine("PrivacyNotice: No OS paths, usernames, Steam IDs, or player/companion names are written. NPCs use anonymized session aliases NPC#NN(xxxx).");
            sb.Append("================================================================================");
            return sb.ToString();
        }

        private static string FormatLine(string category, string message)
        {
            string utc = DateTime.UtcNow.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
            string tStr = SafeUnscaledTime().ToString("F1", CultureInfo.InvariantCulture);
            string igStr = "-";
            try
            {
                var gc = GameController.instance;
                if (Il2CppSafe.IsAliveUnity(gc) && Il2CppSafe.IsAlive(gc.gameData))
                    igStr = gc.gameData.inGameTime.ToString("F2", CultureInfo.InvariantCulture);
            }
            catch { }
            return $"[{utc} | t={tStr}s | ig={igStr}] [{category}] {message}";
        }

        private static string SanitizeSingleLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace('\r', ' ').Replace('\n', ' ').Trim();
        }

        private static float SafeUnscaledTime()
        {
            try { return Time.unscaledTime; }
            catch { return 0f; }
        }

        private static string ReadIl2CppShortTypeName(Il2CppSystem.Object obj)
        {
            if (!Il2CppSafe.IsAlive(obj)) return "none";
            try
            {
                string n = obj.GetIl2CppType()?.Name;
                if (string.IsNullOrEmpty(n)) return "obj";
                if (n.StartsWith("GOAPGoal_", StringComparison.Ordinal)) return n.Substring(9);
                if (n.StartsWith("GOAPAction_", StringComparison.Ordinal)) return n.Substring(11);
                return n;
            }
            catch { return "?"; }
        }

        private static string ReadCurrentActionName(GOAPAgent ag)
        {
            try
            {
                if (Il2CppSafe.IsAlive(ag.forcedAction))
                    return "!" + ReadIl2CppShortTypeName(ag.forcedAction);
                var actions = ag.currentActions;
                if (actions != null && actions.Count > 0 && Il2CppSafe.IsAlive(actions[0]))
                    return ReadIl2CppShortTypeName(actions[0]);
            }
            catch { }
            return "none";
        }

        private static int ReadEquippedWeaponId(HumanCharacterController hc)
        {
            try
            {
                if (Il2CppSafe.IsAliveUnity(hc.rangedWeapon) && Il2CppSafe.IsAlive(hc.rangedWeapon.weaponData))
                    return hc.rangedWeapon.weaponData.itemId;
                if (Il2CppSafe.IsAliveUnity(hc.meleeWeapon) && Il2CppSafe.IsAlive(hc.meleeWeapon.weaponData))
                    return hc.meleeWeapon.weaponData.itemId;
            }
            catch { }
            return 0;
        }

        public static string FormatFileSize(long bytes)
        {
            if (bytes < 1024L) return bytes + " B";
            if (bytes < 1024L * 1024L) return (bytes / 1024.0).ToString("F1", CultureInfo.InvariantCulture) + " KB";
            return (bytes / (1024.0 * 1024.0)).ToString("F2", CultureInfo.InvariantCulture) + " MB";
        }

        public static void SelfVerify()
        {
            string a1 = GetNpcAlias("selfverify-guid-alpha");
            string a2 = GetNpcAlias("selfverify-guid-alpha");
            if (string.IsNullOrEmpty(a1) || a1 != a2 || a1.Contains("selfverify-guid-alpha"))
                throw new Exception("EventLogRecorder.GetNpcAlias failed stability/anonymization check!");

            lock (Sync)
            {
                NpcAliasByGuid.Remove("selfverify-guid-alpha");
                if (_nextNpcOrdinal > 1) _nextNpcOrdinal--;
            }

            if (FormatFileSize(512) != "512 B" || !FormatFileSize(2048).Contains("KB"))
                throw new Exception("EventLogRecorder.FormatFileSize check failed!");
        }
    }
}
