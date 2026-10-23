using System.Collections.Generic;
using Evergreen.Systems;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>Shows which meter hit zero, the final readings and how long the city lasted.</summary>
    public class GameOverScreen : MonoBehaviour
    {
        [SerializeField] SimulationClock clock;
        [SerializeField] ResourceManager resources;
        [SerializeField] ResourceIcons icons;
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
        [SerializeField] int goalYears = 20;

        [Header("Bar scale (value that fills a bar)")]
        [SerializeField] float moneyFullAt = 10000f;
        [SerializeField] float ecoFullAt = 100f;
        [SerializeField] float populationFullAt = 3000f;

        void Awake()
        {
            if (clock == null) clock = FindFirstObjectByType<SimulationClock>();
            if (resources == null) resources = FindFirstObjectByType<ResourceManager>();
            // Keep this component on an always-active object; only `root` is toggled.
            if (root == null) root = gameObject;
            if (resources != null) resources.GameOver += Show;
            if (root != gameObject) root.SetActive(false);
        }

        void OnDestroy()
        {
            if (resources != null) resources.GameOver -= Show;
        }

        void Start()
        {
            if (playAgainButton != null) playAgainButton.onClick.AddListener(Restart);
            if (menuButton != null)
            {
                menuButton.gameObject.SetActive(!string.IsNullOrEmpty(mainMenuScene));
                menuButton.onClick.AddListener(() => SceneManager.LoadScene(mainMenuScene));
            }
        }

        public void Show(GameOverReason reason)
        {
            root.SetActive(true);
            root.transform.SetAsLastSibling();

            bool money = (reason & GameOverReason.Bankrupt) != 0;
            bool eco = (reason & GameOverReason.EcoCollapse) != 0;
            bool pop = (reason & GameOverReason.Depopulated) != 0;

            var names = new List<string>();
            if (money) names.Add("Money");
            if (eco) names.Add("Eco");
            if (pop) names.Add("Population");
            if (titleText != null) titleText.text = string.Join(" & ", names) + " hit zero!";

            int year = clock != null ? clock.CurrentYear : 0;
            string cause = eco ? "The environment collapsed" : money ? "The city went bankrupt" : "Everyone moved away";
            if (subtitleText != null) subtitleText.text = $"{cause} in Year {year}.";

            if (reasonIcon != null && icons != null)
                reasonIcon.sprite = eco ? icons.eco : money ? icons.money : icons.population;

            int survived = clock != null ? clock.YearsElapsed : 0;
            if (survivedText != null)
                survivedText.text = $"Survived <color=#9BE15D><size=24>{survived}</size></color> of {goalYears} years";

            if (resources != null)
            {
                if (moneyRow != null) moneyRow.Set($"${Mathf.FloorToInt(resources.Money):N0}", resources.Money / moneyFullAt, money);
                if (ecoRow != null) ecoRow.Set($"{Mathf.FloorToInt(resources.Eco):N0}", resources.Eco / ecoFullAt, eco);
                if (populationRow != null) populationRow.Set($"{Mathf.FloorToInt(resources.Population):N0}", resources.Population / populationFullAt, pop);
                if (happinessRow != null) happinessRow.Set($"{Mathf.RoundToInt(resources.Happiness)}%", resources.Happiness / 100f, false);
            }
        }

        static void Restart()
        {
            Scene scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            // Works in the editor even if the scene isn't in the build scene list.
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.buildIndex);
#endif
        }
    }
}
