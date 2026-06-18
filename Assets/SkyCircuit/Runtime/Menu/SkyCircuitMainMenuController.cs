using System.Collections;
using SkyCircuit.Networking;
using SkyCircuit.Profiles;
using SkyCircuit.Race;
using SkyCircuit.Tutorial;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SkyCircuit.Menu
{
    public sealed class SkyCircuitMainMenuController : MonoBehaviour
    {
        [SerializeField] private string combatSceneName = "CloudSeaRace";
        [SerializeField] private string trainingSceneName = "CloudSeaRace";
        [SerializeField] private LanNetworkBootstrap lanBootstrap;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Text statusText;
        [SerializeField] private float statusMessageSeconds = 2.4f;

        private Coroutine statusRoutine;
        private RaceTypeSelectionPanel raceTypeSelectionPanel;
        private LanRaceReadyCoordinator readyCoordinator;

        public void Configure(
            string combatScene,
            string trainingScene,
            LanNetworkBootstrap networkBootstrap,
            GameObject settingsRoot,
            Text statusLabel)
        {
            combatSceneName = combatScene;
            trainingSceneName = trainingScene;
            lanBootstrap = networkBootstrap;
            settingsPanel = settingsRoot;
            statusText = statusLabel;
        }

        private void Awake()
        {
            ResolveLanBootstrap();
            EnsureRaceSelectionSystems();

            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }

            if (statusText != null)
            {
                statusText.text = string.Empty;
            }
        }

        public void StartCombat()
        {
            if (!HasEstablishedLanConnection())
            {
                ShowStatus("\u672a\u5efa\u7acb\u8054\u673a\u8fde\u63a5\uff0c\u65e0\u6cd5\u5f00\u59cb\u4f5c\u6218");
                return;
            }

            EnsureRaceSelectionSystems();
            raceTypeSelectionPanel.ShowLan();
        }

        public void OpenTraining()
        {
            EnsureRaceSelectionSystems();
            raceTypeSelectionPanel.ShowTraining();
        }

        public void ToggleSettings()
        {
            OpenSettings();
        }

        public void OpenSettings()
        {
            if (settingsPanel == null)
            {
                ShowStatus("\u8054\u673a\u9762\u677f\u6682\u672a\u914d\u7f6e");
                return;
            }

            settingsPanel.SetActive(true);
        }

        public void OpenTutorial()
        {
            string tutorialSceneName = !string.IsNullOrWhiteSpace(combatSceneName)
                ? combatSceneName
                : trainingSceneName;

            if (string.IsNullOrWhiteSpace(tutorialSceneName) || !Application.CanStreamedLevelBeLoaded(tutorialSceneName))
            {
                ShowStatus("\u6559\u7a0b\u573a\u666f\u8fd8\u6ca1\u6709\u52a0\u5165\u6784\u5efa");
                return;
            }

            SkyCircuitTutorialBootstrap.RequestTutorial();
            RaceLaunchRequest.Request(RaceMode.Tutorial);
            ShutdownLanNetwork();

            SceneManager.LoadScene(tutorialSceneName);
        }

        public void BeginSelectedTraining(CompetitorArchetype playerArchetype)
        {
            RaceLaunchRequest.Request(RaceMode.AiTraining, playerArchetype, CompetitorArchetype.AllRounder);
            ShutdownLanNetwork();
            LoadScene(trainingSceneName, "\u8bad\u7ec3\u573a\u666f\u8fd8\u6ca1\u6709\u52a0\u5165\u6784\u5efa");
        }

        public void ShowMenuStatus(string message)
        {
            ShowStatus(message);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void CloseSettings()
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }
        }

        private void LoadScene(string sceneName, string missingMessage)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                ShowStatus(missingMessage);
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                ShowStatus(missingMessage);
                return;
            }

            SceneManager.LoadScene(sceneName);
        }

        private void HandleLanReadyCountdownCompleted()
        {
            if (lanBootstrap == null || !lanBootstrap.IsServer)
            {
                return;
            }

            RaceLaunchRequest.Request(
                RaceMode.LanMultiplayer,
                readyCoordinator != null ? readyCoordinator.LocalArchetype : CompetitorArchetype.AllRounder,
                CompetitorArchetype.AllRounder);
            TryLoadCombatAsNetworkScene();
        }

        private void ShutdownLanNetwork()
        {
            NetworkManager networkManager = lanBootstrap != null && lanBootstrap.NetworkManager != null
                ? lanBootstrap.NetworkManager
                : NetworkManager.Singleton;
            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
            }
        }

        private bool HasEstablishedLanConnection()
        {
            ResolveLanBootstrap();
            if (lanBootstrap == null || !lanBootstrap.IsListening)
            {
                return false;
            }

            if (lanBootstrap.IsHost)
            {
                return lanBootstrap.ConnectedClientCount > 1;
            }

            if (lanBootstrap.IsServer || lanBootstrap.IsClient)
            {
                return true;
            }

            return false;
        }

        private bool TryLoadCombatAsNetworkScene()
        {
            NetworkManager networkManager = lanBootstrap != null && lanBootstrap.NetworkManager != null
                ? lanBootstrap.NetworkManager
                : NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening || !networkManager.IsServer)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(combatSceneName) || !Application.CanStreamedLevelBeLoaded(combatSceneName))
            {
                ShowStatus("\u4f5c\u6218\u573a\u666f\u8fd8\u6ca1\u6709\u52a0\u5165\u6784\u5efa");
                return true;
            }

            if (!networkManager.NetworkConfig.EnableSceneManagement || networkManager.SceneManager == null)
            {
                return false;
            }

            SceneEventProgressStatus status = networkManager.SceneManager.LoadScene(combatSceneName, LoadSceneMode.Single);
            if (status == SceneEventProgressStatus.Started)
            {
                return true;
            }

            ShowStatus($"\u7f51\u7edc\u573a\u666f\u52a0\u8f7d\u5931\u8d25\uff1a{status}");
            return true;
        }

        private void ResolveLanBootstrap()
        {
            if (lanBootstrap == null)
            {
                lanBootstrap = FindAnyObjectByType<LanNetworkBootstrap>(FindObjectsInactive.Include);
            }
        }

        private void EnsureRaceSelectionSystems()
        {
            if (readyCoordinator == null)
            {
                readyCoordinator = GetComponent<LanRaceReadyCoordinator>();
                if (readyCoordinator == null)
                {
                    readyCoordinator = gameObject.AddComponent<LanRaceReadyCoordinator>();
                }

                readyCoordinator.Configure(lanBootstrap);
                readyCoordinator.CountdownCompleted -= HandleLanReadyCountdownCompleted;
                readyCoordinator.CountdownCompleted += HandleLanReadyCountdownCompleted;
            }

            if (raceTypeSelectionPanel == null)
            {
                raceTypeSelectionPanel = GetComponent<RaceTypeSelectionPanel>();
                if (raceTypeSelectionPanel == null)
                {
                    raceTypeSelectionPanel = gameObject.AddComponent<RaceTypeSelectionPanel>();
                }

                Canvas canvas = GetComponentInParent<Canvas>();
                raceTypeSelectionPanel.Configure(
                    this,
                    canvas != null ? canvas.GetComponent<RectTransform>() : null,
                    readyCoordinator);
            }
        }

        private void OnDestroy()
        {
            if (readyCoordinator != null)
            {
                readyCoordinator.CountdownCompleted -= HandleLanReadyCountdownCompleted;
            }
        }

        private void ShowStatus(string message)
        {
            if (statusText == null)
            {
                Debug.Log(message);
                return;
            }

            if (statusRoutine != null)
            {
                StopCoroutine(statusRoutine);
            }

            statusRoutine = StartCoroutine(ShowStatusRoutine(message));
        }

        private IEnumerator ShowStatusRoutine(string message)
        {
            statusText.text = message;
            yield return new WaitForSeconds(statusMessageSeconds);
            statusText.text = string.Empty;
            statusRoutine = null;
        }
    }
}
