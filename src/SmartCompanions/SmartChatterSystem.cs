using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static class SmartChatterSystem
    {
        private static readonly System.Random Rng = new System.Random();
        private static readonly Dictionary<ChatterTopic, Queue<int>> RecentIndicesByTopic = new Dictionary<ChatterTopic, Queue<int>>();
        private static readonly Dictionary<string, float> WorldNpcLastSpeakTime = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        public static readonly List<string> RecentSpeechLog = new List<string>();

        private static bool _steakSignatureSpokenThisSession = false;
        private static float _lastGlobalChatterTime = -999f;
        private static float _lastAmbientCheckTime = 0f;
        private static float _lastWorldNpcCheckTime = 0f;
        private static bool _wasRaining = false;
        private static bool _wasBloodRaining = false;
        private static int _lastAnnouncedTimeBand = -1;

        private static string _pendingBanterPartnerGuid = "";
        private static float _pendingBanterReplyAt = -1f;
        private static int _cachedTeammatesFrame = -1;
        private static int _cachedLocalOwnerFrame = -1;
        private static readonly List<GOAPAgent> CachedAllTeammates = new List<GOAPAgent>(16);
        private static readonly List<GOAPAgent> CachedLocalOwnerTeammates = new List<GOAPAgent>(16);

        public static void InvalidateSquadCache()
        {
            _cachedTeammatesFrame = -1;
            _cachedLocalOwnerFrame = -1;
        }

        public static List<GOAPAgent> GetActiveTeammateAgents(bool onlyLocalOwner = false)
        {
            int frame = Time.frameCount;
            if (!onlyLocalOwner && frame == _cachedTeammatesFrame && frame > 0)
            {
                PerformanceAndDiagnostics.NoteTeammateCache(hit: true);
                return CachedAllTeammates;
            }
            if (onlyLocalOwner && frame == _cachedLocalOwnerFrame && frame > 0)
            {
                PerformanceAndDiagnostics.NoteTeammateCache(hit: true);
                return CachedLocalOwnerTeammates;
            }
            PerformanceAndDiagnostics.NoteTeammateCache(hit: false);

            var result = onlyLocalOwner ? CachedLocalOwnerTeammates : CachedAllTeammates;
            result.Clear();
            var seen = new HashSet<IntPtr>();
            try
            {
                var mgr = GOAPNPCManager.instance;
                var gc = GameController.instance;
                var playerData = (Il2CppSafe.IsAliveUnity(gc) && Il2CppSafe.IsAlive(gc.gameData)) ? gc.gameData.playerData : null;

                if (Il2CppSafe.IsAlive(mgr) && mgr.activeNPCs != null)
                {
                    foreach (var kv in mgr.activeNPCs)
                    {
                        var agent = kv.Value;
                        if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData) || !Il2CppSafe.IsAliveUnity(agent.humanCharacter))
                            continue;
                        if (agent.humanCharacter.deadFlag) continue;

                        bool isTeammate = false;
                        try
                        {
                            if (CompanionConfigStore.CompanionStates.TryGetValue(agent.npcData.npcGUID ?? "", out var stDismiss) &&
                                stDismiss != null && stDismiss.IsDismissedOrNotInTeam &&
                                string.IsNullOrEmpty(agent.npcData.leaderCharacterGUID))
                            {
                                continue;
                            }
                            if (agent.IsPlayerTeammate()) isTeammate = true;
                            else if (Il2CppSafe.IsAlive(playerData) && GOAPNPCManager.IsPlayerTeammateData(agent.npcData, playerData)) isTeammate = true;
                            else if (mgr.teammateNPCs != null && mgr.teammateNPCs.Contains(agent.npcData)) isTeammate = true;
                            else if (!string.IsNullOrEmpty(agent.npcData.leaderCharacterGUID) && !agent.npcData.isBandit) isTeammate = true;
                        }
                        catch { }

                        if (isTeammate)
                        {
                            if (onlyLocalOwner && !MultiplayerSyncSystem.IsCompanionOwnedByLocalPlayer(agent.npcData))
                                continue;
                            if (seen.Add(agent.Pointer))
                                result.Add(agent);
                        }
                    }
                }

                // On a Multiplayer Client, companions are replicated via NetMonsterSync.m_puppets
                if (MultiplayerSyncSystem.IsMultiplayerClient && Il2CppSafe.IsAliveUnity(ZedZone.Net.NetMonsterSync.instance) &&
                    ZedZone.Net.NetMonsterSync.instance.m_puppets != null)
                {
                    sbyte localZ = Il2CppSafe.IsAliveUnity(MapController.instance) ? MapController.instance.currentZLevel : (sbyte)0;
                    foreach (var kv in ZedZone.Net.NetMonsterSync.instance.m_puppets)
                    {
                        var puppet = kv.Value;
                        if (!Il2CppSafe.IsAlive(puppet) || !Il2CppSafe.IsAliveUnity(puppet.controller) || puppet.controller.deadFlag)
                            continue;
                        var human = puppet.controller.TryCast<HumanCharacterController>();
                        if (!Il2CppSafe.IsAliveUnity(human) || !Il2CppSafe.IsAlive(human.characterData)) continue;
                        if (human.characterData.camp != 1) continue; // Camp 1 = player/teammate camp

                        MultiplayerSyncSystem.EnsureClientPuppetVisibleAndBound(human, localZ);
                        var agent = human.GetCachedGOAPAgent();
                        if (Il2CppSafe.IsAliveUnity(agent) && Il2CppSafe.IsAlive(agent.npcData) && seen.Add(agent.Pointer))
                        {
                            if (onlyLocalOwner && !MultiplayerSyncSystem.IsCompanionOwnedByLocalPlayer(agent.npcData))
                                continue;
                            result.Add(agent);
                        }
                    }
                }
            }
            catch { }
            if (!onlyLocalOwner) _cachedTeammatesFrame = frame;
            else _cachedLocalOwnerFrame = frame;
            return result;
        }

        public static GOAPAgent PickNearestTeammate(Vector3 origin, float maxDist = 28f)
        {
            GOAPAgent best = null;
            float bestSqr = maxDist * maxDist;
            foreach (var ag in GetActiveTeammateAgents())
            {
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAliveUnity(ag.humanCharacter)) continue;
                float sqr = (ag.humanCharacter.transform.position - origin).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = ag;
                }
            }
            return best;
        }

        public static bool Speak(GOAPAgent agent, ChatterTopic topic, float minCooldown = 4.5f, float bubbleDuration = 5.5f, bool force = false)
        {
            if (!CompanionConfigStore.EnableChatter && !force) return false;
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAliveUnity(agent.humanCharacter) || agent.humanCharacter.deadFlag)
                return false;

            float now = Time.unscaledTime;
            float scaledCooldown = minCooldown * CompanionConfigStore.ChatterIntervalMultiplier;
            var npcGuid = Il2CppSafe.IsAlive(agent.npcData) ? agent.npcData.npcGUID : "npc";
            var npcName = agent.npcData?.characterData?.characterName ?? "NPC";
            var st = CompanionConfigStore.GetOrCreateCompanion(npcGuid, npcName);

            if (!force)
            {
                if (now - st.LastChatterTime < scaledCooldown) return false;
                if (now - _lastGlobalChatterTime < 1.6f) return false;
            }

            string line = null;
            if ((topic == ChatterTopic.AmbientBaseIdle || topic == ChatterTopic.CampfireStory) && Rng.NextDouble() < 0.35)
            {
                line = ChatterDatabase.TryGetArchetypeBonusLine(st.EffectiveArchetype, topic, CompanionConfigStore.EffectiveLanguage, CompanionConfigStore.Profanity);
            }
            if (string.IsNullOrEmpty(line))
                line = PickLine(topic);
            if (string.IsNullOrEmpty(line)) return false;

            return SpeakCustom(agent, line, bubbleDuration, force);
        }

        public static bool SpeakCustom(GOAPAgent agent, string line, float bubbleDuration = 5.5f, bool force = false)
        {
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAliveUnity(agent.humanCharacter) || string.IsNullOrEmpty(line))
                return false;
            try
            {
                float now = Time.unscaledTime;
                var npcGuid = Il2CppSafe.IsAlive(agent.npcData) ? agent.npcData.npcGUID : "npc";
                var npcName = agent.npcData?.characterData?.characterName ?? "NPC";
                var st = CompanionConfigStore.GetOrCreateCompanion(npcGuid, npcName);

                st.LastChatterTime = now;
                _lastGlobalChatterTime = now;

                agent.humanCharacter.ShowDialogueBubble(line, bubbleDuration);

                string stamp = DateTime.Now.ToString("HH:mm:ss");
                RecentSpeechLog.Insert(0, $"[{stamp}] {npcName}: {line}");
                if (RecentSpeechLog.Count > 40)
                    RecentSpeechLog.RemoveAt(RecentSpeechLog.Count - 1);

                return true;
            }
            catch (Exception e)
            {
                SmartCompanionsPlugin.LogWarning("SpeakCustom failed: " + e.Message);
                return false;
            }
        }

        private static string PickLine(ChatterTopic topic)
        {
            var lines = ChatterDatabase.GetLines(topic, CompanionConfigStore.EffectiveLanguage, CompanionConfigStore.Profanity);
            if (lines == null || lines.Length == 0) return null;

            if (topic == ChatterTopic.PlayerEatSteakMeat && !_steakSignatureSpokenThisSession)
            {
                _steakSignatureSpokenThisSession = true;
                RecordRecentIndex(topic, 0, lines.Length);
                return lines[0];
            }

            if (!RecentIndicesByTopic.TryGetValue(topic, out var recentQueue))
            {
                recentQueue = new Queue<int>();
                RecentIndicesByTopic[topic] = recentQueue;
            }

            int idx = Rng.Next(lines.Length);
            if (lines.Length > 1)
            {
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    if (!recentQueue.Contains(idx)) break;
                    idx = (idx + 1 + Rng.Next(lines.Length - 1)) % lines.Length;
                }
            }

            RecordRecentIndex(topic, idx, lines.Length);
            return lines[idx];
        }

        private static void RecordRecentIndex(ChatterTopic topic, int idx, int poolSize)
        {
            if (!RecentIndicesByTopic.TryGetValue(topic, out var q))
            {
                q = new Queue<int>();
                RecentIndicesByTopic[topic] = q;
            }
            q.Enqueue(idx);
            int maxHistory = Mathf.Clamp(poolSize / 2, 1, 4);
            while (q.Count > maxHistory)
                q.Dequeue();
        }

        private static bool ApplySingleCompanionRation(GOAPAgent ag, CompanionRuntimeState st, bool fromAutoShare, float portion = 1f)
        {
            if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) || st == null) return false;
            float now = Time.unscaledTime;
            if (now - st.LastRationTime < 30f) return false;
            if (!fromAutoShare && !RationSharing.TryConsumePlayerPortion()) return false;

            st.LastRationTime = now;
            st.FedAndFierceUntil = now + 180f;
            ag.npcData.hunger = Mathf.Clamp(ag.npcData.hunger + 35f * portion, 0f, 100f);
            ag.npcData.thirst = Mathf.Clamp(ag.npcData.thirst + 35f * portion, 0f, 100f);
            ag.npcData.fatigue = Mathf.Clamp(ag.npcData.fatigue + 25f * portion, 0f, 100f);
            ag.npcData.hireAffinity = Mathf.Clamp(ag.npcData.hireAffinity + 0.15f, 0f, 100f);

            var status = ag.npcData.characterData?.characterStatusData;
            if (Il2CppSafe.IsAlive(status))
            {
                status.hunger = Mathf.Min(status.maxHunger > 0 ? status.maxHunger : 100f, status.hunger + 35f * portion);
                status.thirst = Mathf.Min(status.maxThirst > 0 ? status.maxThirst : 100f, status.thirst + 35f * portion);
                status.fatigue = Mathf.Min(status.maxFatigue > 0 ? status.maxFatigue : 100f, status.fatigue + 25f * portion);
                status.energy = Mathf.Min(status.maxEnergy > 0 ? status.maxEnergy : 100f, status.energy + 40f * portion);
            }
            return true;
        }

        /// <summary>
        /// Shares a ration with one or all active companions: nearby companions (&lt;= 3.2m) take it from hand immediately,
        /// while distant companions physically jog up to the player first to take their ration!
        /// </summary>
        public static int CommandShareRationWithSquad(GOAPAgent specificAgent = null, bool fromAutoShare = false)
        {
            var targets = new List<GOAPAgent>();
            if (Il2CppSafe.IsAliveUnity(specificAgent))
                targets.Add(specificAgent);
            else
                targets.AddRange(GetActiveTeammateAgents());

            if (targets.Count == 0) return 0;

            var player = GameController.instance?.playerCharacter;
            float now = Time.unscaledTime;
            int buffedCount = 0;
            int walkingCount = 0;
            GOAPAgent speaker = null;
            float portion = fromAutoShare ? 1f / targets.Count : 1f;

            foreach (var ag in targets)
            {
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) || !Il2CppSafe.IsAliveUnity(ag.humanCharacter)) continue;
                var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, ag.npcData.characterData?.characterName);
                if (now - st.LastRationTime < 30f) continue;

                float distToPlayer = Il2CppSafe.IsAliveUnity(player)
                    ? Vector2.Distance(ag.humanCharacter.transform.position, player.transform.position)
                    : 0f;

                if (!fromAutoShare && Il2CppSafe.IsAliveUnity(player) && distToPlayer > 3.2f && !st.IsSortingActive && !st.IsLootingActive)
                {
                    var capturedAg = ag;
                    var capturedSt = st;
                    BaseChoreSystem.StartPhysicalChoreWalk(
                        capturedAg,
                        capturedSt,
                        player.transform.position,
                        player.transform,
                        CompanionConfigStore.L("Идёт за пайком к лидеру...", "Jogging to leader for ration...", "正前往队长处领取口粮..."),
                        0,
                        () =>
                        {
                            if (ApplySingleCompanionRation(capturedAg, capturedSt, fromAutoShare: false, 1f))
                            {
                                Speak(capturedAg, ChatterTopic.PlayerShareFoodThanks, minCooldown: 0.5f, bubbleDuration: 6.0f, force: true);
                            }
                        });
                    walkingCount++;
                    continue;
                }

                try
                {
                    if (!ApplySingleCompanionRation(ag, st, fromAutoShare, portion)) continue;
                    if (Il2CppSafe.IsAliveUnity(player))
                        PhysicalMovementHelper.TriggerReachInteraction(ag.humanCharacter, player.transform.position, 0);
                }
                catch { continue; }

                if (speaker == null) speaker = ag;
                buffedCount++;
            }

            if (speaker != null)
            {
                Speak(speaker, ChatterTopic.PlayerShareFoodThanks, minCooldown: 0.5f, bubbleDuration: 6.0f, force: true);
            }

            if (!fromAutoShare && (buffedCount > 0 || walkingCount > 0))
            {
                BaseSortingSystem.SetBanner(walkingCount > 0
                    ? CompanionConfigStore.L(
                        $"🍖 Пайки розданы рядом ({buffedCount}), ещё {walkingCount} чел. подходят за своей долей!",
                        $"🍖 Shared rations nearby ({buffedCount}); {walkingCount} companion(s) walking over to take theirs!",
                        $"🍖 已向身边队友分发口粮（{buffedCount} 人），另有 {walkingCount} 人正走来领取！")
                    : CompanionConfigStore.L(
                        $"🍖 Вы поделились пайком с отрядом ({buffedCount} чел.)! Напарники получили бафф «Сытый и злой» на 3 мин!",
                        $"🍖 Shared rations with squad ({buffedCount})! Companions gained 'Fed & Fierce' combat buff for 3 min!",
                        $"🍖 已向小队（{buffedCount} 人）分享口粮！队友获得 3 分钟「饱腹狂暴」战斗增益！"));
            }

            if (!fromAutoShare && buffedCount == 0 && walkingCount == 0)
                BaseSortingSystem.SetBanner(CompanionConfigStore.L(
                    "Нужен съедобный паёк на каждого напарника. Между пайками — 30 секунд.",
                    "One edible ration per companion is required. Wait 30 seconds between rations.",
                    "每名队友需要一份可食用口粮。两次分享间隔为30秒。"));
            return buffedCount + walkingCount;
        }

        public static void OnPlayerEatOrUseFood(HumanCharacterController character, ItemData foodItemData)
        {
            try
            {
                if (!Il2CppSafe.IsAliveUnity(character) || !Il2CppSafe.IsAlive(foodItemData)) return;
                var gc = GameController.instance;
                if (!Il2CppSafe.IsAliveUnity(gc) || !Il2CppSafe.IsAliveUnity(gc.playerCharacter)) return;
                if (character.Pointer != gc.playerCharacter.Pointer) return;

                var companion = PickNearestTeammate(character.transform.position, 26f);
                if (!Il2CppSafe.IsAliveUnity(companion)) return;

                var kind = ItemCategorizer.ClassifyFoodKind(foodItemData);

                // If AutoShareFoodOnEat is enabled and player eats good food/drink, share with squad!
                if (CompanionConfigStore.AutoShareFoodOnEat &&
                    kind != FoodKind.RawMeat && kind != FoodKind.RottenFood &&
                    kind != FoodKind.MedicineBandage && kind != FoodKind.MedicinePills)
                {
                    CommandShareRationWithSquad(null, fromAutoShare: true);
                    return;
                }

                ChatterTopic topic = kind switch
                {
                    FoodKind.SteakAndRoastMeat => ChatterTopic.PlayerEatSteakMeat,
                    FoodKind.RawMeat           => ChatterTopic.PlayerEatRawOrBad,
                    FoodKind.RottenFood        => ChatterTopic.PlayerEatRawOrBad,
                    FoodKind.CannedFood        => ChatterTopic.PlayerEatCanned,
                    FoodKind.FishDish          => ChatterTopic.PlayerEatFish,
                    FoodKind.CookedMeal        => ChatterTopic.PlayerEatSteakMeat,
                    FoodKind.SweetAndSnack     => ChatterTopic.PlayerEatSnackOrFruit,
                    FoodKind.FruitAndVeg       => ChatterTopic.PlayerEatSnackOrFruit,
                    FoodKind.DrinkAndSoda      => ChatterTopic.PlayerDrinkWaterSoda,
                    FoodKind.Alcohol           => ChatterTopic.PlayerDrinkAlcohol,
                    FoodKind.MedicineBandage   => ChatterTopic.PlayerHealBandage,
                    FoodKind.MedicinePills     => ChatterTopic.PlayerHealBandage,
                    _                          => ChatterTopic.PlayerEatCookedMeal
                };

                bool forceFirstSteak = (topic == ChatterTopic.PlayerEatSteakMeat && !_steakSignatureSpokenThisSession);
                Speak(companion, topic, minCooldown: 2.0f, bubbleDuration: 6.5f, force: forceFirstSteak);
            }
            catch (Exception e)
            {
                SmartCompanionsPlugin.LogWarning("OnPlayerEatOrUseFood error: " + e.Message);
            }
        }

        public static void OnPlayerSpecialAction(HumanCharacterController character, ChatterTopic topic)
        {
            try
            {
                var gc = GameController.instance;
                if (!Il2CppSafe.IsAliveUnity(gc) || !Il2CppSafe.IsAliveUnity(gc.playerCharacter) || !Il2CppSafe.IsAliveUnity(character))
                    return;
                if (character.Pointer != gc.playerCharacter.Pointer) return;

                var companion = PickNearestTeammate(character.transform.position, 24f);
                if (!Il2CppSafe.IsAliveUnity(companion)) return;

                Speak(companion, topic, minCooldown: 7.5f, bubbleDuration: 5.5f);
            }
            catch { }
        }

        public static void TickAmbientChatter()
        {
            if (!CompanionConfigStore.EnableChatter) return;
            float now = Time.unscaledTime;

            // 1. Handle pending 2-companion banter reply
            if (_pendingBanterReplyAt > 0f && now >= _pendingBanterReplyAt)
            {
                _pendingBanterReplyAt = -1f;
                var partnerGuid = _pendingBanterPartnerGuid;
                _pendingBanterPartnerGuid = "";
                foreach (var ag in GetActiveTeammateAgents())
                {
                    if (Il2CppSafe.IsAlive(ag.npcData) && ag.npcData.npcGUID == partnerGuid)
                    {
                        Speak(ag, ChatterTopic.BanterReply, minCooldown: 1f, bubbleDuration: 5.5f, force: true);
                        break;
                    }
                }
            }

            // 2. Ambient World NPC chatter (non-teammate survivors/drifters around the player)
            if (CompanionConfigStore.EnableWorldNpcChatter && now - _lastWorldNpcCheckTime >= 16f)
            {
                _lastWorldNpcCheckTime = now;
                TickWorldNpcEncounterChatter(now);
            }

            if (now - _lastAmbientCheckTime < 11f) return;
            _lastAmbientCheckTime = now;

            var gc = GameController.instance;
            if (!Il2CppSafe.IsAliveUnity(gc) || !Il2CppSafe.IsAliveUnity(gc.playerCharacter) || !Il2CppSafe.IsAlive(gc.gameData))
                return;

            var teammates = GetActiveTeammateAgents();
            if (teammates.Count == 0) return;

            var primary = PickNearestTeammate(gc.playerCharacter.transform.position, 24f);
            if (!Il2CppSafe.IsAliveUnity(primary)) return;

            // Weather transitions
            bool isBloodRain = gc.gameData.isBloodRaining;
            bool isRain = gc.gameData.isRaining;
            if (isBloodRain && !_wasBloodRaining)
            {
                _wasBloodRaining = true;
                Speak(primary, ChatterTopic.AmbientBloodRain, minCooldown: 5f, bubbleDuration: 6f);
                return;
            }
            _wasBloodRaining = isBloodRain;

            if (isRain && !_wasRaining && !isBloodRain)
            {
                _wasRaining = true;
                Speak(primary, ChatterTopic.AmbientRain, minCooldown: 10f, bubbleDuration: 5.5f);
                return;
            }
            _wasRaining = isRain;

            // Time of day transitions
            float hours = (gc.gameData.inGameTime / 60f) % 24f;
            int timeBand = (hours >= 6f && hours < 10f) ? 1 : (hours >= 10f && hours < 20f) ? 2 : 0;
            if (_lastAnnouncedTimeBand != -1 && timeBand != _lastAnnouncedTimeBand)
            {
                _lastAnnouncedTimeBand = timeBand;
                if (timeBand == 1)
                {
                    Speak(primary, ChatterTopic.AmbientMorning, minCooldown: 15f, bubbleDuration: 5.5f);
                    return;
                }
                if (timeBand == 0)
                {
                    Speak(primary, ChatterTopic.AmbientNight, minCooldown: 15f, bubbleDuration: 5.5f);
                    return;
                }
            }
            _lastAnnouncedTimeBand = timeBand;

            if (gc.isPlayerInCombat || Il2CppSafe.IsAliveUnity(primary.mostThreateningCharacter)) return;
            if (now - _lastGlobalChatterTime < 32f * CompanionConfigStore.ChatterIntervalMultiplier) return;

            if (Il2CppSafe.IsAlive(primary.npcData))
            {
                if (primary.npcData.hunger < 30f && Rng.NextDouble() < 0.45)
                {
                    Speak(primary, ChatterTopic.AmbientHungry, minCooldown: 40f, bubbleDuration: 5.5f);
                    return;
                }
                if (primary.npcData.fatigue < 25f && Rng.NextDouble() < 0.35)
                {
                    Speak(primary, ChatterTopic.AmbientTired, minCooldown: 40f, bubbleDuration: 5.5f);
                    return;
                }
            }

            if (teammates.Count >= 2 && Rng.NextDouble() < 0.40)
            {
                var speaker1 = teammates[Rng.Next(teammates.Count)];
                GOAPAgent speaker2 = null;
                foreach (var t in teammates)
                {
                    if (t != speaker1 && Vector2.Distance(t.transform.position, speaker1.transform.position) < 16f)
                    {
                        speaker2 = t;
                        break;
                    }
                }
                if (Il2CppSafe.IsAliveUnity(speaker2) && Il2CppSafe.IsAlive(speaker2.npcData))
                {
                    if (Speak(speaker1, ChatterTopic.BanterQuestion, minCooldown: 35f, bubbleDuration: 5.0f))
                    {
                        _pendingBanterPartnerGuid = speaker2.npcData.npcGUID;
                        _pendingBanterReplyAt = now + 4.0f;
                        return;
                    }
                }
            }

            if (Rng.NextDouble() < 0.30)
            {
                Speak(primary, ChatterTopic.AmbientBaseIdle, minCooldown: 45f, bubbleDuration: 5.5f);
            }
        }

        private static void TickWorldNpcEncounterChatter(float now)
        {
            try
            {
                if (now - _lastGlobalChatterTime < 14f) return;
                var gc = GameController.instance;
                var mgr = GOAPNPCManager.instance;
                if (!Il2CppSafe.IsAliveUnity(gc) || !Il2CppSafe.IsAliveUnity(gc.playerCharacter) || !Il2CppSafe.IsAlive(mgr) || mgr.activeNPCs == null)
                    return;

                Vector2 playerPos = gc.playerCharacter.transform.position;
                foreach (var kv in mgr.activeNPCs)
                {
                    var ag = kv.Value;
                    if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) || !Il2CppSafe.IsAliveUnity(ag.humanCharacter))
                        continue;
                    if (ag.humanCharacter.deadFlag || ag.IsPlayerTeammate()) continue;

                    float dist = Vector2.Distance(ag.humanCharacter.transform.position, playerPos);
                    if (dist < 3.0f || dist > 13.5f) continue;

                    string guid = ag.npcData.npcGUID ?? "world_npc";
                    if (WorldNpcLastSpeakTime.TryGetValue(guid, out float lastT) && now - lastT < 75f)
                        continue;

                    if (Rng.NextDouble() < 0.35)
                    {
                        WorldNpcLastSpeakTime[guid] = now;
                        string line = PickLine(ChatterTopic.WorldNpcEncounter);
                        if (!string.IsNullOrEmpty(line))
                        {
                            ag.humanCharacter.ShowDialogueBubble(line, 5.0f);
                            _lastGlobalChatterTime = now;
                        }
                        break;
                    }
                }

                if (WorldNpcLastSpeakTime.Count > 120)
                    WorldNpcLastSpeakTime.Clear();
            }
            catch { }
        }
    }
}
