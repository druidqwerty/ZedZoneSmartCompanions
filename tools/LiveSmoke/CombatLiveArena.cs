using System.Collections.Generic;
using UnityEngine;
namespace ZedZone.SmartCompanions.LiveSmoke
{
    internal static partial class CombatLiveRegression
    {
        private static readonly List<GameObject> ParkedEnemies = new();
        private static void IsolateArena()
        {
            var snapshot = new List<BasicCharacterController>();
            foreach(var character in GameController.instance.characterList) snapshot.Add(character);
            foreach(var character in snapshot)
            {
                if(!Il2CppSafe.IsAliveUnity(character) || character.Pointer==_zombie.Pointer || character.deadFlag ||
                    !Il2CppSafe.IsAlive(character.characterData) || !character.gameObject.activeSelf ||
                    !BasicCharacterController.IsHostileCamp(character.characterData)) continue;
                ParkedEnemies.Add(character.gameObject);
                character.gameObject.SetActive(false);
            }
            LiveSmokePlugin.Logger.LogInfo("[CombatLive] Isolated one-hostile copied-world arena; temporarily parked "+ParkedEnemies.Count+" other enemies.");
        }
        private static void RestoreArena()
        {
            foreach(var obj in ParkedEnemies) if(obj!=null)obj.SetActive(true);
            ParkedEnemies.Clear();
        }
    }
}
