using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SkyCircuit.Menu
{
    public sealed class SkyCircuitMainMenuController : MonoBehaviour
    {
        [SerializeField] private string combatSceneName = "V0_9_CloudSeaRacePrototype";
        [SerializeField] private string trainingSceneName = "V0_1_FlightPrototype";
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Text statusText;
        [SerializeField] private float statusMessageSeconds = 2.4f;

        private Coroutine statusRoutine;

        public void Configure(
            string combatScene,
            string trainingScene,
            GameObject settingsRoot,
            Text statusLabel)
        {
            combatSceneName = combatScene;
            trainingSceneName = trainingScene;
            settingsPanel = settingsRoot;
            statusText = statusLabel;
        }

        private void Awake()
        {
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
            LoadScene(combatSceneName, "\u4f5c\u6218\u573a\u666f\u8fd8\u6ca1\u6709\u52a0\u5165\u6784\u5efa");
        }

        public void OpenTraining()
        {
            LoadScene(trainingSceneName, "\u8bad\u7ec3\u573a\u666f\u8fd8\u6ca1\u6709\u52a0\u5165\u6784\u5efa");
        }

        public void ToggleSettings()
        {
            if (settingsPanel == null)
            {
                ShowStatus("\u8bbe\u7f6e\u9762\u677f\u6682\u672a\u914d\u7f6e");
                return;
            }

            settingsPanel.SetActive(!settingsPanel.activeSelf);
            if (settingsPanel.activeSelf)
            {
                ShowStatus("\u8bbe\u7f6e\u5148\u4fdd\u7559\u8f7b\u91cf\u5165\u53e3");
            }
        }

        public void OpenTutorial()
        {
            ShowStatus("\u6559\u7a0b\u5185\u5bb9\u7a0d\u540e\u5f00\u653e");
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
