using System.Collections.Generic;
using Evergreen.Systems;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>
    /// Shown when ResourceManager raises GameOver. Says which meter hit zero,
    /// lists the final readings and how many years the city lasted.
    /// </summary>
    public class GameOverScreen : MonoBehaviour
    {
        [SerializeField] SimulationClock clock;
        [SerializeField] ResourceManager resources;
        [SerializeField] ResourceIcons icons;

        [Tooltip("The panel that gets shown. Keep this component on an always-active object outside it.")]
        [SerializeField] GameObject root;

        [Header("Content")]
        [SerializeField] Image reasonIcon;
        [SerializeField] Text titleText;
        [SerializeField] Text subtitleText;
        [SerializeField] Text survivedText;
        [SerializeField] GameOverMeterRow moneyRow;
        [SerializeField] GameOverMeterRow ecoRow;
        [SerializeField] GameOverMeterRow populationRow;
        [SerializeField] GameOverMeterRow happinessRow;

        [Header("Buttons")]
        [SerializeField] Button playAgainButton;
        [SerializeField] Button menuButton;
        [Tooltip("Scene loaded by the Menu button. Leave empty to hide the button.")]
        [SerializeField] string mainMenuScene = "";
        [Tooltip("Shown as 'Survived 7 of 20 years'.")]
        [SerializeField] int goalYears = 20;

        [Header("Bar scale (value that fills a bar)")]
        [SerializeField] float moneyFullAt = 10000f;
        [SerializeField] float ecoFullAt = 100f;
        [SerializeField] float populationFullAt = 3000f;

        // ---------- Unity lifecycle ----------

        void Awake()
        {
            if (clock == null) clock = FindFirstObjectByType<SimulationClock>();
            if (resources == null) resources = FindFirstObjectByType<ResourceManager>();
            if (root == null) root = gameObject;

            if (resources != null) resources.GameOver += Show;

            // Only hide the panel if it's a separate object; hiding ourselves would stop us hearing GameOver.
            if (root != gameObject) root.SetActive(false);
        }

        void OnDestroy()
        {
            if (resources != null) resources.GameOver -= Show;
        }

        void Start()
        {
            if (playAgainButton != null) playAgainButton.onClick.AddListener(RestartScene);
            if (menuButton != null)
            {
                bool hasMenuScene = !string.IsNullOrEmpty(mainMenuScene);
                menuButton.gameObject.SetActive(hasMenuScene);
                menuButton.onClick.AddListener(() => SceneManager.LoadScene(mainMenuScene));
            }
        }

        // ---------- Showing the screen ----------

        public void Show(GameOverReason reason)
        {
            root.SetActive(true);
            root.transform.SetAsLastSibling(); // Draw on top of the rest of the HUD.

            bool wentBankrupt = (reason & GameOverReason.Bankrupt) != 0;
            bool ecoCollapsed = (reason & GameOverReason.EcoCollapse) != 0;
            bool depopulated = (reason & GameOverReason.Depopulated) != 0;

            // Title, e.g. "Money & Eco hit zero!"
            var failedResourceNames = new List<string>();
            if (wentBankrupt) failedResourceNames.Add("Money");
            if (ecoCollapsed) failedResourceNames.Add("Eco");
            if (depopulated) failedResourceNames.Add("Population");
            if (titleText != null) titleText.text = string.Join(" & ", failedResourceNames) + " hit zero!";

            // If several failed at once, eco wins, then money, for the subtitle and icon.
            int finalYear = clock != null ? clock.CurrentYear : 0;
            string cause = ecoCollapsed ? "The environment collapsed"
                : wentBankrupt ? "The city went bankrupt"
                : "Everyone moved away";
            if (subtitleText != null) subtitleText.text = $"{cause} in Year {finalYear}.";

            if (reasonIcon != null && icons != null)
                reasonIcon.sprite = ecoCollapsed ? icons.eco : wentBankrupt ? icons.money : icons.population;

            int yearsSurvived = clock != null ? clock.YearsElapsed : 0;
            if (survivedText != null)
                survivedText.text = $"Survived <color=#9BE15D><size=24>{yearsSurvived}</size></color> of {goalYears} years";

            FillMeterRows(wentBankrupt, ecoCollapsed, depopulated);
        }

        void FillMeterRows(bool wentBankrupt, bool ecoCollapsed, bool depopulated)
        {
            if (resources == null) return;

            if (moneyRow != null)
                moneyRow.Set($"${Mathf.FloorToInt(resources.Money):N0}", resources.Money / moneyFullAt, wentBankrupt);
            if (ecoRow != null)
                ecoRow.Set($"{Mathf.FloorToInt(resources.Eco):N0}", resources.Eco / ecoFullAt, ecoCollapsed);
            if (populationRow != null)
                populationRow.Set($"{Mathf.FloorToInt(resources.Population):N0}", resources.Population / populationFullAt, depopulated);

            // Happiness can't cause a game over, so it's never marked failed.
            if (happinessRow != null)
                happinessRow.Set($"{Mathf.RoundToInt(resources.Happiness)}%", resources.Happiness / 100f, false);
        }

        // ---------- Buttons ----------

        static void RestartScene()
        {
            Scene currentScene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            // Works in the editor even if the scene isn't in the build scene list.
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                currentScene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(currentScene.buildIndex);
#endif
        }
    }
}
