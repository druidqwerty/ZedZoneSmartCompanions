using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static class SmartChatterSystem
    {
        private static readonly System.Random Rng = new System.Random();
        private static readonly Dictionary<ChatterTopic, int> LastPickedIndex = new Dictionary<ChatterTopic, int>();
        public static readonly List<string> RecentSpeechLog = new List<string>();

        private static bool _steakSignatureSpokenThisSession = false;
        private static float _lastGlobalChatterTime = -999f;
        private static float _lastAmbientCheckTime = 0f;
        private static bool _wasRaining = false;
        private static bool _wasBloodRaining = false;
        private static int _lastAnnouncedTimeBand = -1; // 0=Night, 1=Morning, 2=Day, 3=Evening

        private static string _pendingBanterPartnerGuid = "";
        private static float _pendingBanterReplyAt = -1f;

        public static List<GOAPAgent> GetActiveTeammateAgents()
        {
            var result = new List<GOAPAgent>();
            try
            {
                var mgr = GOAPNPCManager.instance;
                if (mgr == null || mgr.activeNPCs == null) return result;

                var gc = GameController.instance;
                var playerData = gc != null && gc.gameData != null ? gc.gameData.playerData : null;

                foreach (var kv in mgr.activeNPCs)
                {
                    var agent = kv.Value;
                    if (agent == null || agent.npcData == null || agent.humanCharacter == null) continue;
                    if (agent.humanCharacter.deadFlag) continue;

                    bool isTeammate = false;
                    try
                    {
                        if (agent.IsPlayerTeammate()) isTeammate = true;
                        else if (playerData != null && GOAPNPCManager.IsPlayerTeammateData(agent.npcData, playerData)) isTeammate = true;
                        else if (mgr.teammateNPCs != null && mgr.teammateNPCs.Contains(agent.npcData)) isTeammate = true;
                    }
                    catch { }

                    if (isTeammate)
                        result.Add(agent);
                }
            }
            catch { }
            return result;
        }

        public static GOAPAgent PickNearestTeammate(Vector3 origin, float maxDist = 28f)
        {
            GOAPAgent best = null;
            float bestSqr = maxDist * maxDist;
            foreach (var ag in GetActiveTeammateAgents())
            {
                if (ag == null || ag.humanCharacter == null) continue;
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
            if (agent == null || agent.humanCharacter == null || agent.humanCharacter.deadFlag) return false;

            float now = Time.unscaledTime;
            float scaledCooldown = minCooldown * CompanionConfigStore.ChatterIntervalMultiplier;
            var npcGuid = agent.npcData != null ? agent.npcData.npcGUID : "npc";
            var npcName = agent.npcData?.characterData?.characterName ?? "NPC";
            var st = CompanionConfigStore.GetOrCreateCompanion(npcGuid, npcName);

            if (!force)
            {
                if (now - st.LastChatterTime < scaledCooldown) return false;
                if (now - _lastGlobalChatterTime < 1.8f) return false;
            }

            string line = PickLine(topic, agent);
            if (string.IsNullOrEmpty(line)) return false;

            return SpeakCustom(agent, line, bubbleDuration, force);
        }

        public static bool SpeakCustom(GOAPAgent agent, string line, float bubbleDuration = 5.5f, bool force = false)
        {
            if (agent == null || agent.humanCharacter == null || string.IsNullOrEmpty(line)) return false;
            try
            {
                float now = Time.unscaledTime;
                var npcGuid = agent.npcData != null ? agent.npcData.npcGUID : "npc";
                var npcName = agent.npcData?.characterData?.characterName ?? "NPC";
                var st = CompanionConfigStore.GetOrCreateCompanion(npcGuid, npcName);

                st.LastChatterTime = now;
                _lastGlobalChatterTime = now;

                agent.humanCharacter.ShowDialogueBubble(line, bubbleDuration);

                string stamp = DateTime.Now.ToString("HH:mm:ss");
                RecentSpeechLog.Insert(0, $"[{stamp}] {npcName}: {line}");
                if (RecentSpeechLog.Count > 30)
                    RecentSpeechLog.RemoveAt(RecentSpeechLog.Count - 1);

                return true;
            }
            catch (Exception e)
            {
                SmartCompanionsPlugin.LogWarning("SpeakCustom failed: " + e.Message);
                return false;
            }
        }

        private static string PickLine(ChatterTopic topic, GOAPAgent agent)
        {
            bool ru = CompanionConfigStore.UseRussian;
            var lines = ChatterDatabase.GetLines(topic, ru);
            if (lines == null || lines.Length == 0) return null;

            // Ensure the signature steak line is guaranteed first time player eats steak/meat!
            if (topic == ChatterTopic.PlayerEatSteakMeat && !_steakSignatureSpokenThisSession)
            {
                _steakSignatureSpokenThisSession = true;
                LastPickedIndex[topic] = 0;
                return lines[0];
            }

            int idx = Rng.Next(lines.Length);
            if (lines.Length > 1 && LastPickedIndex.TryGetValue(topic, out int prev) && idx == prev)
            {
                idx = (idx + 1 + Rng.Next(lines.Length - 1)) % lines.Length;
            }
            LastPickedIndex[topic] = idx;
            return lines[idx];
        }

        public static void OnPlayerEatOrUseFood(HumanCharacterController character, ItemData foodItemData)
        {
            try
            {
                if (character == null || foodItemData == null) return;
                var gc = GameController.instance;
                if (gc == null || gc.playerCharacter == null || character.Pointer != gc.playerCharacter.Pointer)
                    return;

                var companion = PickNearestTeammate(character.transform.position, 26f);
                if (companion == null) return;

                var kind = ItemCategorizer.ClassifyFoodKind(foodItemData);
                ChatterTopic topic = kind switch
                {
                    FoodKind.SteakAndRoastMeat => ChatterTopic.PlayerEatSteakMeat,
                    FoodKind.RawMeat           => ChatterTopic.PlayerEatRawOrBad,
                    FoodKind.RottenFood        => ChatterTopic.PlayerEatRawOrBad,
                    FoodKind.CannedFood        => ChatterTopic.PlayerEatCanned,
                    FoodKind.FishDish          => ChatterTopic.PlayerEatFish,
                    FoodKind.CookedMeal        => ChatterTopic.PlayerEatSteakMeat, // hot cooked meals also trigger meat/meal envy!
                    FoodKind.SweetAndSnack     => ChatterTopic.PlayerEatSnackOrFruit,
                    FoodKind.FruitAndVeg       => ChatterTopic.PlayerEatSnackOrFruit,
                    FoodKind.DrinkAndSoda      => ChatterTopic.PlayerDrinkWaterSoda,
                    FoodKind.Alcohol           => ChatterTopic.PlayerDrinkAlcohol,
                    FoodKind.MedicineBandage   => ChatterTopic.PlayerHealBandage,
                    FoodKind.MedicinePills     => ChatterTopic.PlayerHealBandage,
                    _                          => ChatterTopic.PlayerEatCookedMeal
                };

                // Eating steak/meat is high priority so the companion always comments!
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
                if (gc == null || gc.playerCharacter == null || character == null) return;
                if (character.Pointer != gc.playerCharacter.Pointer) return;

                var companion = PickNearestTeammate(character.transform.position, 24f);
                if (companion == null) return;

                Speak(companion, topic, minCooldown: 8.0f, bubbleDuration: 5.0f);
            }
            catch { }
        }

        public static void TickAmbientChatter()
        {
            if (!CompanionConfigStore.EnableChatter) return;
            float now = Time.unscaledTime;

            // Handle pending 2-companion banter reply
            if (_pendingBanterReplyAt > 0f && now >= _pendingBanterReplyAt)
            {
                _pendingBanterReplyAt = -1f;
                var partnerGuid = _pendingBanterPartnerGuid;
                _pendingBanterPartnerGuid = "";
                foreach (var ag in GetActiveTeammateAgents())
                {
                    if (ag.npcData != null && ag.npcData.npcGUID == partnerGuid)
                    {
                        Speak(ag, ChatterTopic.BanterReply, minCooldown: 1f, bubbleDuration: 5.5f, force: true);
                        break;
                    }
                }
            }

            if (now - _lastAmbientCheckTime < 12f) return;
            _lastAmbientCheckTime = now;

            var gc = GameController.instance;
            if (gc == null || gc.playerCharacter == null || gc.gameData == null) return;

            var teammates = GetActiveTeammateAgents();
            if (teammates.Count == 0) return;

            var primary = PickNearestTeammate(gc.playerCharacter.transform.position, 24f);
            if (primary == null) return;

            // 1. Weather changes (Rain / Blood Rain)
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

            // 2. Time of day transitions (Morning 06:00, Night 20:00)
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

            // 3. Only do idle/needs/banter when not in active combat
            if (gc.isPlayerInCombat || primary.mostThreateningCharacter != null) return;
            if (now - _lastGlobalChatterTime < 38f * CompanionConfigStore.ChatterIntervalMultiplier) return;

            // Check hunger or fatigue of companion
            if (primary.npcData != null)
            {
                if (primary.npcData.hunger < 28f && Rng.NextDouble() < 0.45)
                {
                    Speak(primary, ChatterTopic.AmbientHungry, minCooldown: 45f, bubbleDuration: 5.5f);
                    return;
                }
                if (primary.npcData.fatigue < 25f && Rng.NextDouble() < 0.35)
                {
                    Speak(primary, ChatterTopic.AmbientTired, minCooldown: 45f, bubbleDuration: 5.5f);
                    return;
                }
            }

            // Two-companion banter if player has 2+ companions nearby!
            if (teammates.Count >= 2 && Rng.NextDouble() < 0.35)
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
                if (speaker2 != null && speaker2.npcData != null)
                {
                    if (Speak(speaker1, ChatterTopic.BanterQuestion, minCooldown: 40f, bubbleDuration: 5.0f))
                    {
                        _pendingBanterPartnerGuid = speaker2.npcData.npcGUID;
                        _pendingBanterReplyAt = now + 4.2f;
                        return;
                    }
                }
            }

            // Otherwise occasional base/calm ambient line
            if (Rng.NextDouble() < 0.25)
            {
                Speak(primary, ChatterTopic.AmbientBaseIdle, minCooldown: 50f, bubbleDuration: 5.5f);
            }
        }
    }
}
