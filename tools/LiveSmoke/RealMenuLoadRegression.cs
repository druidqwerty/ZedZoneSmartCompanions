using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZedZone.SmartCompanions.LiveSmoke
{
    /// <summary>
    /// End-to-end real-player MainMenu -> SinglePlayer -> Sandbox Save -> Slot/CharacterSelect -> Gaming
    /// native load harness (--sc-real-menu-load). Never overrides Time.timeScale, isSoftPaused, or ResumeGame().
    /// </summary>
    public sealed class RealMenuLoadRunner : MonoBehaviour
    {
        public RealMenuLoadRunner(IntPtr ptr) : base(ptr) { }

        private enum Phase
        {
            WaitMainMenu = 0,
            SettleMainMenu = 1,
            OpenSandboxSaveList = 2,
            ClickWorldRow = 3,
            HandlePreGamingModals = 4,
            WaitGamingWorldReady = 5,
            PostLoadVerify = 6,
        }

        private Phase _phase = Phase.WaitMainMenu;
        private float _initFinishedAt = -1f;
        private float _mainMenuEnteredAt = -1f;
        private float _lastConfirmDismissedAt = -10f;
        private float _nextConfirmDismissAt = 0f;
        private float _stepStartedAt = 0f;
        private float _worldClickedAt = 0f;
        private float _loadStartedAt = 0f;
        private float _charOverlaySeenAt = -1f;
        private float _nextProgressLogAt = 0f;
        private float _stageStartedAt = 0f;
        private int _postLoadStage = 0;
        private string _screenshotPath;

        private bool _forcedMainMenuFallback;
        private bool _clickedSlotRow;
        private bool _pickedCharacter;
        private bool _sawGamingScene;
        private bool _sawZeroTimeScale;
        private bool _sawGateBlockedDuringLoad;

        private bool TryDismissConfirmPanel(string context)
        {
            var cp = GenericConfirmPanel._instance;
            if (!Il2CppSafe.IsAliveUnity(cp) || !cp.isVisible)
                return false;
            if (Time.unscaledTime < _nextConfirmDismissAt)
                return true;

            _nextConfirmDismissAt = Time.unscaledTime + 0.30f;
            _lastConfirmDismissedAt = Time.realtimeSinceStartup;
            string msg = "";
            try { msg = cp.fullText ?? ""; } catch { }
            string flat = msg.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (flat.Length > 200) flat = flat.Substring(0, 200) + "...";
            if (context == "gaming-load" && cp.onCancel != null)
            {
                LiveSmokePlugin.Logger.LogInfo($"[RealMenuLoad] Cancelling in-game GenericConfirmPanel ({context}): \"{flat}\"");
                cp.OnCancelClick();
            }
            else
            {
                LiveSmokePlugin.Logger.LogInfo($"[RealMenuLoad] Confirming GenericConfirmPanel ({context}): \"{flat}\"");
                cp.OnConfirmClick();
            }
            return true;
        }

        private void Update()
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;

            try
            {
                if (Time.realtimeSinceStartup > 145f)
                    throw new Exception($"Overall 145s safety timeout exceeded in phase={_phase} (scene={SceneManager.GetActiveScene().name}, timeScale={Time.timeScale:F3}).");

                switch (_phase)
                {
                    case Phase.WaitMainMenu:
                        TickWaitMainMenu();
                        break;
                    case Phase.SettleMainMenu:
                        TickSettleMainMenu();
                        break;
                    case Phase.OpenSandboxSaveList:
                        TickOpenSandboxSaveList();
                        break;
                    case Phase.ClickWorldRow:
                        TickClickWorldRow();
                        break;
                    case Phase.HandlePreGamingModals:
                        TickHandlePreGamingModals();
                        break;
                    case Phase.WaitGamingWorldReady:
                        TickWaitGamingWorldReady();
                        break;
                    case Phase.PostLoadVerify:
                        TickPostLoadVerify();
                        break;
                }
            }
            catch (Exception e)
            {
                LiveSmokePlugin.Logger.LogError("[RealMenuLoad] FAIL " + e);
                try
                {
                    if (!string.IsNullOrEmpty(LiveSmokePlugin.Lab))
                        ScreenCapture.CaptureScreenshot(Path.Combine(LiveSmokePlugin.Lab, "real-menu-load-fail.png"));
                }
                catch { }
                enabled = false;
                Application.Quit(1);
            }
        }

        private void TickWaitMainMenu()
        {
            if (GameController.initFinishFlag && _initFinishedAt < 0f)
                _initFinishedAt = Time.realtimeSinceStartup;

            if (GameController.initFinishFlag)
                TryDismissConfirmPanel("boot");

            string activeScene = SceneManager.GetActiveScene().name;
            if (GameController.initFinishFlag && activeScene == "MainMenu")
            {
                _mainMenuEnteredAt = Time.realtimeSinceStartup;
                if (!_forcedMainMenuFallback)
                    LiveSmokePlugin.Logger.LogInfo($"[RealMenuLoad] MainMenu loaded naturally at t={_mainMenuEnteredAt:F2}s.");
                else
                    LiveSmokePlugin.Logger.LogInfo($"[RealMenuLoad] MainMenu loaded after fallback at t={_mainMenuEnteredAt:F2}s.");
                _phase = Phase.SettleMainMenu;
                return;
            }

            if (!GameController.initFinishFlag || Time.realtimeSinceStartup < 6f)
                return;

            if (activeScene == "GameDataInit" && !_forcedMainMenuFallback)
            {
                float waitAnchor = Math.Max(6f, _initFinishedAt);
                if (Time.realtimeSinceStartup - waitAnchor >= 12f)
                {
                    _forcedMainMenuFallback = true;
                    LiveSmokePlugin.Logger.LogInfo("[RealMenuLoad] Still in GameDataInit after 12s natural wait; calling LoadScene(\"MainMenu\") fallback.");
                    GameController.instance.LoadScene("MainMenu");
                }
            }
        }

        private void TickSettleMainMenu()
        {
            if (TryDismissConfirmPanel("mainmenu-settle"))
                return;

            if (Time.realtimeSinceStartup - _mainMenuEnteredAt < 2.0f)
                return;
            if (Time.realtimeSinceStartup - _lastConfirmDismissedAt < 0.40f)
                return;

            foreach (var p in new[] { GameConfigs.gameDataSavingPath, GameConfigs.mapDataSavingPath, GameConfigs.gameSettingsDataSavingPath, ZedZone.Net.NetCharacterSave.saveDirectory })
            {
                if (!Path.GetFullPath(p).StartsWith(LiveSmokePlugin.Lab, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Unredirected save/settings/mpchar path: " + p);
            }

            if (SmartCompanionsRuntimeGate.IsGameplayWorldReady())
                throw new Exception("SmartCompanionsRuntimeGate.IsGameplayWorldReady() unexpectedly returned true in MainMenu!");

            SinglePlayerModePanel.OpenPanel();
            LiveSmokePlugin.Logger.LogInfo("[RealMenuLoad] Opened SinglePlayerModePanel.");
            _stepStartedAt = Time.realtimeSinceStartup;
            _phase = Phase.OpenSandboxSaveList;
        }

        private void TickOpenSandboxSaveList()
        {
            if (TryDismissConfirmPanel("singleplayer-mode"))
                return;

            var sp = SinglePlayerModePanel.instance;
            if (!Il2CppSafe.IsAliveUnity(sp))
            {
                if (Time.realtimeSinceStartup - _stepStartedAt > 10f)
                    throw new Exception("SinglePlayerModePanel.instance did not appear within 10s.");
                return;
            }

            sp.OpenSaveList(GameDataPanel.GameDataListMode.Sandbox);
            LiveSmokePlugin.Logger.LogInfo("[RealMenuLoad] Opened Sandbox save list.");
            _stepStartedAt = Time.realtimeSinceStartup;
            _phase = Phase.ClickWorldRow;
        }

        private void TickClickWorldRow()
        {
            if (TryDismissConfirmPanel("sandbox-save-list"))
                return;

            var panel = GameDataPanel._instance;
            if (!Il2CppSafe.IsAliveUnity(panel) || panel.loadCoroutine != null || !Il2CppSafe.IsAliveUnity(panel.uiParent))
            {
                if (Time.realtimeSinceStartup - _stepStartedAt > 15f)
                    throw new Exception("GameDataPanel._instance did not finish loading save list within 15s.");
                return;
            }

            GameDataUI targetCard = null;
            foreach (var card in panel.uiParent.GetComponentsInChildren<GameDataUI>(false))
            {
                if (!Il2CppSafe.IsAliveUnity(card) || !card.gameObject.activeInHierarchy) continue;
                if (card.TryCast<GameDataUI_IngameLoad>() != null) continue;
                if (card.gameData == null) continue;
                targetCard = card;
                break;
            }

            if (targetCard == null)
            {
                if (Time.realtimeSinceStartup - _stepStartedAt > 12f)
                    throw new Exception("No Sandbox GameDataUI world save card found under GameDataPanel._instance.uiParent.");
                return;
            }

            if (Time.unscaledTime < targetCard.initTimeFlag + 0.30f)
                return;

            LiveSmokePlugin.Logger.LogInfo($"[RealMenuLoad] Clicking Sandbox world row (initTimeFlag={targetCard.initTimeFlag:F2}).");
            targetCard.OnClick();
            _worldClickedAt = Time.realtimeSinceStartup;
            _loadStartedAt = Time.realtimeSinceStartup;
            _nextProgressLogAt = Time.realtimeSinceStartup + 2.5f;
            _phase = Phase.HandlePreGamingModals;
        }

        private void TickHandlePreGamingModals()
        {
            if (Time.timeScale <= 0.02f)
                _sawZeroTimeScale = true;

            if (SceneManager.GetActiveScene().name == "Gaming")
            {
                _sawGamingScene = true;
                LiveSmokePlugin.Logger.LogInfo($"[RealMenuLoad] Entered Gaming scene (timeScale={Time.timeScale:F3}).");
                _phase = Phase.WaitGamingWorldReady;
                TickWaitGamingWorldReady();
                return;
            }

            if (TryDismissConfirmPanel("pre-gaming"))
                return;

            if (Time.realtimeSinceStartup - _worldClickedAt < 0.35f)
                return;

            if (!_pickedCharacter && CharacterSelectOverlay.IsOpen && Il2CppSafe.IsAliveUnity(CharacterSelectOverlay.instance))
            {
                if (_charOverlaySeenAt < 0f)
                {
                    _charOverlaySeenAt = Time.realtimeSinceStartup;
                    return;
                }
                if (Time.realtimeSinceStartup - _charOverlaySeenAt < 0.20f)
                    return;

                _pickedCharacter = true;
                string defaultId = CharacterSelectOverlay.instance.m_defaultId;
                LiveSmokePlugin.Logger.LogInfo($"[RealMenuLoad] Selecting character via CharacterSelectOverlay.OnPick(defaultId={defaultId ?? "null"}).");
                CharacterSelectOverlay.instance.OnPick(defaultId, null);
                return;
            }

            if (!_clickedSlotRow)
            {
                var slotPanel = GameDataPanel_IngameLoad._instance;
                if (Il2CppSafe.IsAliveUnity(slotPanel) && slotPanel.gameObject.activeInHierarchy && slotPanel.loadCoroutine == null)
                {
                    var searchRoot = Il2CppSafe.IsAliveUnity(slotPanel.uiParent) ? slotPanel.uiParent.gameObject : slotPanel.gameObject;
                    GameDataUI_IngameLoad chosenSlot = null;
                    foreach (var slot in searchRoot.GetComponentsInChildren<GameDataUI_IngameLoad>(false))
                    {
                        if (!Il2CppSafe.IsAliveUnity(slot) || !slot.gameObject.activeInHierarchy || slot.gameData == null)
                            continue;
                        if (chosenSlot == null || (!chosenSlot.isLatestSave && slot.isLatestSave))
                            chosenSlot = slot;
                    }

                    if (chosenSlot != null && Time.unscaledTime >= chosenSlot.initTimeFlag + 0.30f)
                    {
                        _clickedSlotRow = true;
                        LiveSmokePlugin.Logger.LogInfo($"[RealMenuLoad] Clicking multi-slot save row slotIndex={chosenSlot.slotIndex}, isLatestSave={chosenSlot.isLatestSave}.");
                        chosenSlot.OnClick();
                        return;
                    }
                }
            }

            if (Time.realtimeSinceStartup - _loadStartedAt > 45f)
                throw new Exception("Timed out (45s) waiting to transition from MainMenu save flow into Gaming scene.");
        }

        private void TickWaitGamingWorldReady()
        {
            _sawGamingScene = true;
            if (Time.timeScale <= 0.02f)
                _sawZeroTimeScale = true;

            bool worldReady = SmartCompanionsRuntimeGate.IsGameplayWorldReady();
            bool simActive = SmartCompanionsRuntimeGate.IsGameplaySimulationActive();

            if (Il2CppSafe.IsAliveUnity(InGameController.instance) && !worldReady)
                _sawGateBlockedDuringLoad = true;

            if (!worldReady)
            {
                if (SmartCompanionsInput.BlockPlayerControls || SmartCompanionsInput.BlockPlayerMouse)
                {
                    throw new Exception(
                        $"Input capture leaked during world loading (!IsGameplayWorldReady): " +
                        $"BlockPlayerControls={SmartCompanionsInput.BlockPlayerControls}, BlockPlayerMouse={SmartCompanionsInput.BlockPlayerMouse}");
                }
            }

            if (TryDismissConfirmPanel("gaming-load"))
                return;

            if (Time.realtimeSinceStartup >= _nextProgressLogAt)
            {
                _nextProgressLogAt = Time.realtimeSinceStartup + 2.5f;
                LiveSmokePlugin.Logger.LogInfo(
                    $"[RealMenuLoad] Loading Gaming... elapsed={Time.realtimeSinceStartup - _loadStartedAt:F1}s " +
                    $"timeScale={Time.timeScale:F3} worldReady={worldReady} simActive={simActive} " +
                    $"loadingOrTrans={SmartCompanionsRuntimeGate.IsAnyLoadingScreenOrTransitionActive()} " +
                    $"uiAllowed={SmartCompanionsRuntimeGate.IsUiAllowedToRenderAndCapture()} " +
                    $"sawZero={_sawZeroTimeScale} sawGateHeld={_sawGateBlockedDuringLoad}");
            }

            if (worldReady && simActive && Time.frameCount > GenericConfirmPanel.s_lastHideFrame + 1)
            {
                if (!_sawGamingScene)
                    throw new Exception("Gaming scene was not observed.");
                if (!_sawZeroTimeScale)
                    throw new Exception("Time.timeScale never dropped to 0 during real save load (sawZeroTimeScale=false).");
                if (Time.timeScale < 0.99f)
                    throw new Exception($"Time.timeScale did not naturally recover to >= 0.99f (actual={Time.timeScale:F3}).");
                if (!_sawGateBlockedDuringLoad)
                    throw new Exception("SmartCompanionsRuntimeGate did not hold during InGameController loading (sawGateBlockedDuringLoad=false).");
                if (Il2CppSafe.IsAliveUnity(LootInventoryPanel._instance))
                    throw new Exception("LootInventoryPanel._instance was prematurely instantiated during real menu load!");
                if (!Il2CppSafe.IsAliveUnity(CharacterInput.instance))
                    throw new Exception("CharacterInput.instance is missing after world became ready!");
                if (CharacterInput.instance.stopAllInput)
                    throw new Exception("CharacterInput.instance.stopAllInput remained stuck true after real menu load!");
                if (!SmartCompanionsRuntimeGate.IsUiAllowedToRenderAndCapture())
                    throw new Exception("SmartCompanionsRuntimeGate.IsUiAllowedToRenderAndCapture() was false after world ready!");
                var pcd = GameController.instance?.playerCharacter?.characterData;
                if (!Il2CppSafe.IsAlive(pcd) || pcd.characterAttrId != 0)
                    throw new Exception($"Player CharacterData.characterAttrId was corrupted during MainMenu/CharacterSelect load (actual={pcd?.characterAttrId ?? -999}, expected=0)!");

                LiveSmokePlugin.Logger.LogInfo(
                    $"[RealMenuLoad] Native load complete in {Time.realtimeSinceStartup - _loadStartedAt:F1}s " +
                    $"(timeScale={Time.timeScale:F3}, sawZeroTimeScale=true, sawGateBlockedDuringLoad=true, LootInventoryPanel=null, stopAllInput=false).");

                _postLoadStage = 0;
                _stageStartedAt = Time.realtimeSinceStartup;
                _phase = Phase.PostLoadVerify;
                return;
            }

            if (Time.realtimeSinceStartup - _loadStartedAt > 90f)
            {
                throw new Exception(
                    $"90s timeout waiting for native Gaming world load: worldReady={worldReady}, simActive={simActive}, " +
                    $"timeScale={Time.timeScale:F3}, sawZero={_sawZeroTimeScale}, sawGateHeld={_sawGateBlockedDuringLoad}");
            }
        }

        private void TickPostLoadVerify()
        {
            if (_postLoadStage < 4)
            {
                if (Time.timeScale < 0.99f)
                    throw new Exception($"Time.timeScale unexpectedly dropped to {Time.timeScale:F3} during post-load live verification!");
                if (!SmartCompanionsRuntimeGate.IsGameplayWorldReady() || !SmartCompanionsRuntimeGate.IsGameplaySimulationActive())
                    throw new Exception("SmartCompanionsRuntimeGate unexpectedly reported not-ready during post-load live verification!");
                if (Il2CppSafe.IsAliveUnity(LootInventoryPanel._instance))
                    throw new Exception("LootInventoryPanel._instance unexpectedly spawned during post-load live verification!");
            }

            float stageElapsed = Time.realtimeSinceStartup - _stageStartedAt;

            switch (_postLoadStage)
            {
                case 0: // 0.4s initial unpaused world check
                    if (CharacterInput.instance.stopAllInput || SmartCompanionsInput.BlockPlayerControls)
                        throw new Exception("Player input unexpectedly blocked before opening F8!");
                    if (stageElapsed >= 0.40f)
                    {
                        SmartCompanionsUI.ShowMainWindow = true;
                        if (!SmartCompanionsInput.BlockPlayerControls)
                            throw new Exception("Setting SmartCompanionsUI.ShowMainWindow=true did not set BlockPlayerControls=true!");
                        LiveSmokePlugin.Logger.LogInfo("[RealMenuLoad] Opened F8 MainWindow (BlockPlayerControls=true verified).");
                        _postLoadStage = 1;
                        _stageStartedAt = Time.realtimeSinceStartup;
                    }
                    break;

                case 1: // 0.8s with F8 open
                    if (!SmartCompanionsInput.BlockPlayerControls)
                        throw new Exception("BlockPlayerControls dropped while SmartCompanionsUI.ShowMainWindow=true!");
                    if (stageElapsed >= 0.80f)
                    {
                        SmartCompanionsUI.ShowMainWindow = false;
                        _ = SmartCompanionsInput.BlockPlayerControls;
                        _ = SmartCompanionsInput.BlockPlayerMouse;
                        _postLoadStage = 2;
                        _stageStartedAt = Time.realtimeSinceStartup;
                    }
                    break;

                case 2: // 0.8s with F8 closed
                    if (stageElapsed >= 0.10f)
                    {
                        if (SmartCompanionsInput.BlockPlayerControls)
                            throw new Exception("BlockPlayerControls remained true after closing SmartCompanionsUI.ShowMainWindow!");
                        if (CharacterInput.instance.stopAllInput)
                            throw new Exception("CharacterInput.instance.stopAllInput remained true after closing SmartCompanionsUI.ShowMainWindow!");
                    }
                    if (stageElapsed >= 0.80f)
                    {
                        LiveSmokePlugin.Logger.LogInfo("[RealMenuLoad] Closed F8 MainWindow (!stopAllInput && !BlockPlayerControls verified); running 2.0s live world soak.");
                        _postLoadStage = 3;
                        _stageStartedAt = Time.realtimeSinceStartup;
                    }
                    break;

                case 3: // 2.0s live companions & world tick
                    if (SmartCompanionsInput.BlockPlayerControls || CharacterInput.instance.stopAllInput)
                        throw new Exception("Player input became blocked during 2.0s post-load world soak!");
                    if (stageElapsed >= 2.0f)
                    {
                        _screenshotPath = Path.Combine(LiveSmokePlugin.Lab, "real-menu-load.png");
                        if (File.Exists(_screenshotPath))
                            File.Delete(_screenshotPath);
                        ScreenCapture.CaptureScreenshot(_screenshotPath);
                        _postLoadStage = 4;
                        _stageStartedAt = Time.realtimeSinceStartup;
                    }
                    break;

                case 4: // Flush screenshot (> 10 KB) before quitting
                    if (File.Exists(_screenshotPath) && new FileInfo(_screenshotPath).Length > 10240)
                    {
                        LiveSmokePlugin.Logger.LogInfo(
                            $"[RealMenuLoad] PASS v{SmartCompanionsPlugin.PluginVersion}: MainMenu -> Sandbox Save -> CharacterSelect -> Native Load (timeScale 0->1, gate held -> ready) -> Live F8 toggle & gameplay verified.");
                        enabled = false;
                        Application.Quit(0);
                        return;
                    }
                    if (stageElapsed > 5.0f)
                        throw new Exception("Timed out waiting for real-menu-load.png (> 10 KB) to flush to disk.");
                    break;
            }
        }
    }
}
