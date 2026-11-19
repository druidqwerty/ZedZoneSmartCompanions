using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine.SceneManagement;

namespace ZedZone.SmartCompanions
{
    /// <summary>
    /// v3.1.1 Centralized runtime readiness &amp; transition gate.
    /// Every native/interop type access is isolated inside a [NoInlining] helper wrapped by try/catch
    /// so missing or changed symbols on older/newer interop builds never trigger RyuJIT TypeLoadException
    /// or MissingMethodException in callers.
    ///
    /// Native singleton safety invariants (verified in GameAssembly.dll):
    /// - NEVER access .instance on LoadingInfoPanel, GameInitPanel, or GenericConfirmPanel (their getters
    ///   instantiate UI prefabs onto MainCanvas when null). Read ONLY _instance backing properties.
    /// - NEVER call GameInitPanel.isIniting (throws NRE when openingPanel is null).
    /// </summary>
    public static class SmartCompanionsRuntimeGate
    {
        public static bool IsAnyLoadingScreenOrTransitionActive()
        {
            try
            {
                if (IsLoadingScreenOpaqueSafe() || IsStoryModeSwitchingAreaSafe())
                    return true;

                if (IsLoadingInfoPanelAliveSafe() || IsGameInitPanelActiveSafe())
                    return true;

                if (IsInGameControllerAliveSafe())
                {
                    if (IsInGameReturningToMainMenuSafe())
                        return true;

                    if (!IsMapControllerAliveSafe() ||
                        !IsMapDataInitializedSafe() ||
                        IsMapSwitchModeSafe() ||
                        IsMapChangingFloorSafe())
                    {
                        return true;
                    }

                    bool terrainDone = IsTerrainInitialStreamingDoneSafe(out bool streamingSymbolMissing);
                    bool streamingDone = terrainDone
                        || ((IsNetworkClientSafe() || streamingSymbolMissing)
                            && HasLoadedMapChunksSafe()
                            && !IsLoadingInfoPanelAliveSafe()
                            && !IsGameInitPanelActiveSafe());
                    if (!streamingDone)
                        return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsGameplayWorldReady()
        {
            try
            {
                if (!IsGameControllerInitFinishedSafe())
                    return false;
                if (!IsGameControllerAndGameDataReadySafe())
                    return false;
                if (!IsInGameControllerAliveSafe() || IsInGameReturningToMainMenuSafe())
                    return false;
                if (!IsActiveGameSceneSafe())
                    return false;
                if (!IsMapControllerAliveSafe())
                    return false;
                if (IsAnyLoadingScreenOrTransitionActive())
                    return false;
                if (!IsLocalPlayerAndCharacterDataAliveSafe())
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsGameplaySimulationActive()
        {
            try
            {
                if (!IsGameplayWorldReady())
                    return false;
                if (IsInGamePausingSafe())
                    return false;
                if (IsGameControllerSoftPausedSafe())
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsUiAllowedToRenderAndCapture()
        {
            try
            {
                if (!IsInGameControllerAliveSafe() || IsInGameReturningToMainMenuSafe())
                    return false;
                if (!IsActiveGameSceneSafe())
                    return false;
                return IsLocalPlayerAliveSafe() && !IsAnyLoadingScreenOrTransitionActive();
            }
            catch
            {
                return false;
            }
        }

        // --- Safe wrappers around [NoInlining] interop accessors ---

        private static bool IsLoadingScreenOpaqueSafe()
        {
            try
            {
                if (ReadLoadingScreenOpaqueFlag())
                    return true;
            }
            catch { }

            try
            {
                if (ReadLoadingScreenIsLoadingOpaque())
                    return true;
            }
            catch { }

            return false;
        }

        private static bool IsStoryModeSwitchingAreaSafe()
        {
            try { return ReadStoryModeIsSwitchingArea(); }
            catch { return false; }
        }

        private static bool IsLoadingInfoPanelAliveSafe()
        {
            try { return ReadLoadingInfoPanelAlive(); }
            catch { return false; }
        }

        private static bool IsGameInitPanelActiveSafe()
        {
            try { return ReadGameInitPanelActive(); }
            catch { return false; }
        }

        private static bool IsInGameControllerAliveSafe()
        {
            try { return ReadInGameControllerAlive(); }
            catch { return false; }
        }

        private static bool IsInGameReturningToMainMenuSafe()
        {
            try { return ReadInGameReturningToMainMenu(); }
            catch { return false; }
        }

        private static bool IsMapControllerAliveSafe()
        {
            try { return ReadMapControllerAlive(); }
            catch { return false; }
        }

        private static bool IsMapDataInitializedSafe()
        {
            try { return ReadMapDataInitialized(); }
            catch { return false; }
        }

        private static bool IsMapSwitchModeSafe()
        {
            try { return ReadMapSwitchMode(); }
            catch { return false; }
        }

        private static bool IsMapChangingFloorSafe()
        {
            try { return ReadMapChangingFloor(); }
            catch { return false; }
        }

        private static bool IsTerrainInitialStreamingDoneSafe(out bool symbolMissing)
        {
            try
            {
                symbolMissing = false;
                return ReadTerrainInitialStreamingDone();
            }
            catch (Exception ex)
            {
                symbolMissing = ex is MissingMethodException || ex is TypeLoadException || ex is MissingFieldException;
                return false;
            }
        }

        private static bool HasLoadedMapChunksSafe()
        {
            try { return ReadHasLoadedMapChunks(); }
            catch { return false; }
        }

        private static bool IsNetworkClientSafe()
        {
            try { return ReadIsNetworkClient(); }
            catch { return false; }
        }

        private static bool IsGameControllerInitFinishedSafe()
        {
            try { return ReadGameControllerInitFinishFlag(); }
            catch { return false; }
        }

        private static bool IsGameControllerAndGameDataReadySafe()
        {
            try { return ReadGameControllerAndGameDataReady(); }
            catch { return false; }
        }

        private static bool IsLocalPlayerAliveSafe()
        {
            try { return ReadLocalPlayerAlive(); }
            catch { return false; }
        }

        private static bool IsLocalPlayerAndCharacterDataAliveSafe()
        {
            try { return ReadLocalPlayerAndCharacterDataAlive(); }
            catch { return false; }
        }

        private static bool IsInGamePausingSafe()
        {
            try
            {
                return ReadInGamePausing();
            }
            catch (Exception ex)
            {
                if (ex is MissingMethodException || ex is TypeLoadException || ex is MissingFieldException)
                    return false;
                return true;
            }
        }

        private static bool IsActiveGameSceneSafe()
        {
            try
            {
                return ReadActiveGameSceneIsGaming();
            }
            catch
            {
                return true;
            }
        }

        private static bool IsGameControllerSoftPausedSafe()
        {
            try
            {
                return ReadGameControllerSoftPaused();
            }
            catch (Exception ex)
            {
                if (ex is MissingMethodException || ex is TypeLoadException || ex is MissingFieldException)
                    return false;
                return true;
            }
        }

        // --- Raw [NoInlining] interop accessors (isolated per native type/member) ---

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadLoadingScreenOpaqueFlag() => LoadingScreenState.opaqueFlag;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadLoadingScreenIsLoadingOpaque() => LoadingScreenState.IsLoadingOpaque;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadStoryModeIsSwitchingArea() => StoryModeManager.isSwitchingArea;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadLoadingInfoPanelAlive() => Il2CppSafe.IsAliveUnity(LoadingInfoPanel._instance);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadGameInitPanelActive()
        {
            var initPanel = GameInitPanel._instance;
            return Il2CppSafe.IsAliveUnity(initPanel)
                && Il2CppSafe.IsAliveUnity(initPanel.gameObject)
                && initPanel.gameObject.activeInHierarchy;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadInGameControllerAlive() => Il2CppSafe.IsAliveUnity(InGameController.instance);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadInGameReturningToMainMenu()
        {
            var igc = InGameController.instance;
            return Il2CppSafe.IsAliveUnity(igc) && igc.isReturningToMainMenu;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadMapControllerAlive() => Il2CppSafe.IsAliveUnity(MapController.instance);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadMapDataInitialized()
        {
            var map = MapController.instance;
            return Il2CppSafe.IsAliveUnity(map) && map.dataInitialized;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadMapSwitchMode()
        {
            var map = MapController.instance;
            return Il2CppSafe.IsAliveUnity(map) && map.isMapSwitchMode;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadMapChangingFloor()
        {
            var map = MapController.instance;
            return Il2CppSafe.IsAliveUnity(map) && map.IsChangingFloor;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadTerrainInitialStreamingDone()
        {
            var map = MapController.instance;
            return Il2CppSafe.IsAliveUnity(map) && map.terrainInitialStreamingDone;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadHasLoadedMapChunks()
        {
            var map = MapController.instance;
            return Il2CppSafe.IsAliveUnity(map)
                && map.currentChunkList != null
                && map.currentChunkList.Count > 0;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadIsNetworkClient() => ZedZone.Net.NetworkManager.isClient;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadGameControllerInitFinishFlag() => GameController.initFinishFlag;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadGameControllerAndGameDataReady()
        {
            var gc = GameController.instance;
            if (!Il2CppSafe.IsAliveUnity(gc)) return false;
            var gd = gc.gameData;
            return Il2CppSafe.IsAlive(gd) && gd.initFlag;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadLocalPlayerAlive()
        {
            var gc = GameController.instance;
            return Il2CppSafe.IsAliveUnity(gc) && Il2CppSafe.IsAliveUnity(gc.playerCharacter);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadLocalPlayerAndCharacterDataAlive()
        {
            var gc = GameController.instance;
            if (!Il2CppSafe.IsAliveUnity(gc)) return false;
            var player = gc.playerCharacter;
            return Il2CppSafe.IsAliveUnity(player) && Il2CppSafe.IsAlive(player.characterData);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadInGamePausing()
        {
            var igc = InGameController.instance;
            return !Il2CppSafe.IsAliveUnity(igc) || igc.isPausing;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadGameControllerSoftPaused()
        {
            var gc = GameController.instance;
            return !Il2CppSafe.IsAliveUnity(gc) || gc.isSoftPaused;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadActiveGameSceneIsGaming()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(sceneName))
                return true;
            return string.Equals(sceneName, "Gaming", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Fault-isolated Harmony patch runner that replaces monolithic Harmony.PatchAll(Assembly).
    /// - Class-targeted patch classes run individually via CreateClassProcessor(type).Patch() inside try/catch.
    /// - Container patch classes ([HarmonyPatch] with per-method target attributes, such as HarmonyPatches and
    ///   MultiplayerTeammateAidPatches) resolve and group methods per target MethodBase so Prefix+Postfix+Finalizer
    ///   pairs (including ref __state pairs) are applied together on one PatchProcessor while isolating any missing
    ///   or renamed game method from all sibling patches.
    /// </summary>
    public static class SafeHarmonyPatcher
    {
        public static int LastPatchedMethodCount { get; private set; }
        public static int FailedPatchCount { get; private set; }

        private sealed class TargetPatchGroup
        {
            public readonly MethodBase Target;
            public readonly List<HarmonyMethod> Prefixes = new List<HarmonyMethod>();
            public readonly List<HarmonyMethod> Postfixes = new List<HarmonyMethod>();
            public readonly List<HarmonyMethod> Transpilers = new List<HarmonyMethod>();
            public readonly List<HarmonyMethod> Finalizers = new List<HarmonyMethod>();
            public readonly List<HarmonyMethod> ILManipulators = new List<HarmonyMethod>();

            public TargetPatchGroup(MethodBase target)
            {
                Target = target;
            }
        }

        public static int PatchAssemblySafely(Harmony harmony, Assembly assembly)
        {
            LastPatchedMethodCount = 0;
            FailedPatchCount = 0;

            if (harmony == null || assembly == null)
                return 0;

            IEnumerable<Type> types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = (ex.Types ?? Array.Empty<Type>()).Where(t => t != null);
            }

            foreach (var type in types)
            {
                if (type == null || type.ContainsGenericParameters)
                    continue;
                if (!HasHarmonyPatchAttributeData(type))
                    continue;

                try
                {
                    var classAttrs = HarmonyMethodExtensions.GetFromType(type);
                    var classMerged = (classAttrs != null && classAttrs.Count > 0)
                        ? HarmonyMethod.Merge(classAttrs)
                        : new HarmonyMethod();

                    bool typeSpecifiesTarget =
                        classMerged.declaringType != null ||
                        !string.IsNullOrEmpty(classMerged.methodName) ||
                        classMerged.methodType.HasValue ||
                        HasTargetMethodHelper(type);

                    if (typeSpecifiesTarget)
                    {
                        harmony.CreateClassProcessor(type).Patch();
                    }
                    else
                    {
                        PatchContainerClassMethods(harmony, type, classAttrs);
                    }
                }
                catch (Exception ex)
                {
                    FailedPatchCount++;
                    SmartCompanionsPlugin.LogWarning($"[Harmony] Skipped patch class {type.Name}: {ex.Message}");
                }
            }

            try
            {
                LastPatchedMethodCount = harmony.GetPatchedMethods().Count();
            }
            catch
            {
                LastPatchedMethodCount = 0;
            }

            return LastPatchedMethodCount;
        }

        private static void PatchContainerClassMethods(Harmony harmony, Type containerType, List<HarmonyMethod> classAttrs)
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            MethodInfo[] methods;
            try
            {
                methods = containerType.GetMethods(flags);
            }
            catch (Exception ex)
            {
                FailedPatchCount++;
                SmartCompanionsPlugin.LogWarning($"[Harmony] Skipped container {containerType.Name}: {ex.Message}");
                return;
            }

            var groupsByTarget = new Dictionary<MethodBase, TargetPatchGroup>();
            var orderedGroups = new List<TargetPatchGroup>();

            foreach (var method in methods)
            {
                if (method == null || !HasHarmonyPatchAttributeData(method))
                    continue;

                try
                {
                    DeterminePatchKinds(
                        method,
                        out bool isPrefix,
                        out bool isPostfix,
                        out bool isTranspiler,
                        out bool isFinalizer,
                        out bool isILManipulator,
                        out bool isReversePatch);

                    if (!isPrefix && !isPostfix && !isTranspiler && !isFinalizer && !isILManipulator && !isReversePatch)
                    {
                        FailedPatchCount++;
                        SmartCompanionsPlugin.LogWarning($"[Harmony] Skipped {containerType.Name}.{method.Name}: no patch kind attribute.");
                        continue;
                    }

                    var methodAttrs = HarmonyMethodExtensions.GetFromMethod(method);
                    var targetSpecs = BuildTargetSpecs(classAttrs, methodAttrs);
                    if (targetSpecs.Count == 0)
                    {
                        FailedPatchCount++;
                        SmartCompanionsPlugin.LogWarning($"[Harmony] Skipped {containerType.Name}.{method.Name}: empty HarmonyPatch target.");
                        continue;
                    }

                    foreach (var spec in targetSpecs)
                    {
                        try
                        {
                            var target = ResolveTargetMethod(spec);
                            if (target == null)
                            {
                                FailedPatchCount++;
                                SmartCompanionsPlugin.LogWarning(
                                    $"[Harmony] Skipped patch method {containerType.Name}.{method.Name}: target {DescribeSpec(spec)} not found.");
                                continue;
                            }

                            var fix = CreateFixHarmonyMethod(method, spec);
                            if (isReversePatch)
                            {
                                harmony.CreateReversePatcher(target, fix).Patch();
                                continue;
                            }

                            if (!groupsByTarget.TryGetValue(target, out var group))
                            {
                                group = new TargetPatchGroup(target);
                                groupsByTarget[target] = group;
                                orderedGroups.Add(group);
                            }

                            if (isPrefix) group.Prefixes.Add(fix);
                            if (isPostfix) group.Postfixes.Add(fix);
                            if (isTranspiler) group.Transpilers.Add(fix);
                            if (isFinalizer) group.Finalizers.Add(fix);
                            if (isILManipulator) group.ILManipulators.Add(fix);
                        }
                        catch (Exception targetEx)
                        {
                            FailedPatchCount++;
                            SmartCompanionsPlugin.LogWarning(
                                $"[Harmony] Skipped patch method {containerType.Name}.{method.Name}: {targetEx.Message}");
                        }
                    }
                }
                catch (Exception methodEx)
                {
                    FailedPatchCount++;
                    SmartCompanionsPlugin.LogWarning(
                        $"[Harmony] Skipped patch method {containerType.Name}.{method.Name}: {methodEx.Message}");
                }
            }

            foreach (var group in orderedGroups)
            {
                try
                {
                    var processor = harmony.CreateProcessor(group.Target);
                    foreach (var pre in group.Prefixes) processor.AddPrefix(pre);
                    foreach (var post in group.Postfixes) processor.AddPostfix(post);
                    foreach (var trans in group.Transpilers) processor.AddTranspiler(trans);
                    foreach (var fin in group.Finalizers) processor.AddFinalizer(fin);
                    foreach (var ilm in group.ILManipulators) processor.AddILManipulator(ilm);
                    processor.Patch();
                }
                catch (Exception patchEx)
                {
                    FailedPatchCount++;
                    SmartCompanionsPlugin.LogWarning(
                        $"[Harmony] Skipped target {group.Target.DeclaringType?.Name}.{group.Target.Name} in {containerType.Name}: {patchEx.Message}");
                }
            }
        }

        private static bool HasHarmonyPatchAttributeData(MemberInfo member)
        {
            try
            {
                foreach (var cad in member.CustomAttributes)
                {
                    var attrType = cad.AttributeType;
                    if (attrType != null &&
                        (attrType == typeof(HarmonyPatch) ||
                         attrType == typeof(HarmonyPatchAll) ||
                         string.Equals(attrType.FullName, "HarmonyLib.HarmonyPatch", StringComparison.Ordinal)))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return true;
            }
        }

        private static bool HasTargetMethodHelper(Type type)
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var m in type.GetMethods(flags))
            {
                if (string.Equals(m.Name, "TargetMethod", StringComparison.Ordinal) ||
                    string.Equals(m.Name, "TargetMethods", StringComparison.Ordinal))
                {
                    return true;
                }

                foreach (var cad in m.CustomAttributes)
                {
                    var at = cad.AttributeType;
                    if (at == typeof(HarmonyTargetMethod) || at == typeof(HarmonyTargetMethods))
                        return true;
                }
            }
            return false;
        }

        private static void DeterminePatchKinds(
            MethodInfo method,
            out bool isPrefix,
            out bool isPostfix,
            out bool isTranspiler,
            out bool isFinalizer,
            out bool isILManipulator,
            out bool isReversePatch)
        {
            isPrefix = method.IsDefined(typeof(HarmonyPrefix), false);
            isPostfix = method.IsDefined(typeof(HarmonyPostfix), false);
            isTranspiler = method.IsDefined(typeof(HarmonyTranspiler), false);
            isFinalizer = method.IsDefined(typeof(HarmonyFinalizer), false);
            isILManipulator = method.IsDefined(typeof(HarmonyILManipulator), false);
            isReversePatch = method.IsDefined(typeof(HarmonyReversePatch), false);

            if (!isPrefix && !isPostfix && !isTranspiler && !isFinalizer && !isILManipulator && !isReversePatch)
            {
                switch (method.Name)
                {
                    case "Prefix": isPrefix = true; break;
                    case "Postfix": isPostfix = true; break;
                    case "Transpiler": isTranspiler = true; break;
                    case "Finalizer": isFinalizer = true; break;
                    case "ILManipulator": isILManipulator = true; break;
                    case "ReversePatch": isReversePatch = true; break;
                }
            }
        }

        private static List<HarmonyMethod> BuildTargetSpecs(List<HarmonyMethod> classAttrs, List<HarmonyMethod> methodAttrs)
        {
            var complete = new List<HarmonyMethod>();
            var modifiers = new List<HarmonyMethod>();
            if (classAttrs != null)
                modifiers.AddRange(classAttrs);

            if (methodAttrs != null)
            {
                foreach (var attr in methodAttrs)
                {
                    if (attr == null) continue;
                    bool hasTargetMember = !string.IsNullOrEmpty(attr.methodName) ||
                        attr.methodType == MethodType.Constructor ||
                        attr.methodType == MethodType.StaticConstructor;
                    if (attr.declaringType != null && hasTargetMember)
                        complete.Add(attr);
                    else
                        modifiers.Add(attr);
                }
            }

            var results = new List<HarmonyMethod>();
            if (complete.Count > 1)
            {
                foreach (var targetAttr in complete)
                {
                    var combined = new List<HarmonyMethod>(modifiers.Count + 1);
                    combined.AddRange(modifiers);
                    combined.Add(targetAttr);
                    results.Add(HarmonyMethod.Merge(combined));
                }
            }
            else
            {
                var all = new List<HarmonyMethod>((classAttrs?.Count ?? 0) + (methodAttrs?.Count ?? 0));
                if (classAttrs != null) all.AddRange(classAttrs);
                if (methodAttrs != null) all.AddRange(methodAttrs);
                if (all.Count > 0)
                    results.Add(HarmonyMethod.Merge(all));
            }

            return results;
        }

        private static MethodBase ResolveTargetMethod(HarmonyMethod info)
        {
            if (info == null || info.declaringType == null)
                return null;

            MethodType kind = info.methodType ?? MethodType.Normal;
            switch (kind)
            {
                case MethodType.Normal:
                    if (string.IsNullOrEmpty(info.methodName))
                        return null;
                    return AccessTools.DeclaredMethod(info.declaringType, info.methodName, info.argumentTypes)
                        ?? AccessTools.Method(info.declaringType, info.methodName, info.argumentTypes);

                case MethodType.Getter:
                    if (string.IsNullOrEmpty(info.methodName))
                    {
                        var idxGet = AccessTools.DeclaredProperty(info.declaringType, "Item")
                            ?? AccessTools.Property(info.declaringType, "Item");
                        return idxGet?.GetGetMethod(true);
                    }
                    return AccessTools.DeclaredPropertyGetter(info.declaringType, info.methodName)
                        ?? AccessTools.PropertyGetter(info.declaringType, info.methodName);

                case MethodType.Setter:
                    if (string.IsNullOrEmpty(info.methodName))
                    {
                        var idxSet = AccessTools.DeclaredProperty(info.declaringType, "Item")
                            ?? AccessTools.Property(info.declaringType, "Item");
                        return idxSet?.GetSetMethod(true);
                    }
                    return AccessTools.DeclaredPropertySetter(info.declaringType, info.methodName)
                        ?? AccessTools.PropertySetter(info.declaringType, info.methodName);

                case MethodType.Constructor:
                    return AccessTools.DeclaredConstructor(info.declaringType, info.argumentTypes, false)
                        ?? AccessTools.Constructor(info.declaringType, info.argumentTypes, false);

                case MethodType.StaticConstructor:
                    return AccessTools.DeclaredConstructor(info.declaringType, Type.EmptyTypes, true)
                        ?? AccessTools.Constructor(info.declaringType, Type.EmptyTypes, true);

                case MethodType.Enumerator:
                    if (string.IsNullOrEmpty(info.methodName))
                        return null;
                    var enumMethod = AccessTools.DeclaredMethod(info.declaringType, info.methodName, info.argumentTypes)
                        ?? AccessTools.Method(info.declaringType, info.methodName, info.argumentTypes);
                    return enumMethod != null ? AccessTools.EnumeratorMoveNext(enumMethod) : null;

                default:
                    return null;
            }
        }

        private static HarmonyMethod CreateFixHarmonyMethod(MethodInfo patchMethod, HarmonyMethod mergedSpec)
        {
            var hm = new HarmonyMethod(patchMethod);
            if (mergedSpec != null)
            {
                if (mergedSpec.priority != -1)
                    hm.priority = mergedSpec.priority;
                if (mergedSpec.before != null && mergedSpec.before.Length > 0)
                    hm.before = mergedSpec.before;
                if (mergedSpec.after != null && mergedSpec.after.Length > 0)
                    hm.after = mergedSpec.after;
                if (mergedSpec.debug.HasValue)
                    hm.debug = mergedSpec.debug;
            }
            return hm;
        }

        private static string DescribeSpec(HarmonyMethod spec)
        {
            if (spec == null) return "<null>";
            string typeName = spec.declaringType != null ? spec.declaringType.FullName : "<null-type>";
            string memberName = !string.IsNullOrEmpty(spec.methodName) ? spec.methodName : (spec.methodType?.ToString() ?? "<unnamed>");
            return $"{typeName}.{memberName}";
        }
    }
}
