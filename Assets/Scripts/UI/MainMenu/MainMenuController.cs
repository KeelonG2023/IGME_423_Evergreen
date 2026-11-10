using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Evergreen.UI
{
    /// <summary>
    /// Drives the main menu: Title screen ("Click to Begin") -> Level Select -> load level scene.
    /// All UI (including the level buttons) is built in the scene; nothing is spawned at runtime.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        enum MenuState { Title, Transitioning, LevelSelect }

        [Header("Panels (each needs a CanvasGroup)")]
        [SerializeField] CanvasGroup titlePanel;
        [SerializeField] CanvasGroup levelSelectPanel;

        [Header("Title Screen")]
        [SerializeField] Text clickToBeginText;

        [Header("Level Select")]
        [Tooltip("The level buttons already placed in the scene, in order.")]
        [SerializeField] List<LevelSelectButton> levelButtons = new List<LevelSelectButton>();
        [SerializeField] Button backButton;
        [SerializeField] Button startButton;
        [SerializeField] Text selectedNameText;
        [SerializeField] Text selectedDescriptionText;

        [Header("Transitions")]
        [SerializeField] float fadeDuration = 0.35f;

        MenuState state = MenuState.Title;
        LevelData selected;

        void Start()
        {
            SetPanel(titlePanel, true);
            SetPanel(levelSelectPanel, false);

            if (backButton != null) backButton.onClick.AddListener(ReturnToTitle);
            if (startButton != null) startButton.onClick.AddListener(StartSelected);

            InitLevelButtons();
        }

        void Update()
        {
            if (state == MenuState.Title)
            {
                if (AnyInputPressed()) StartCoroutine(GoToLevelSelect());
            }
            else if (state == MenuState.LevelSelect && CancelPressed())
            {
                ReturnToTitle();
            }
        }

      


        IEnumerator GoToLevelSelect()
        {
            state = MenuState.Transitioning;
            yield return Fade(titlePanel, false);
            yield return Fade(levelSelectPanel, true);
            state = MenuState.LevelSelect;
        }

        public void ReturnToTitle()
        {
            if (state != MenuState.LevelSelect) return;
            StartCoroutine(BackToTitleRoutine());
        }

        IEnumerator BackToTitleRoutine()
        {
            state = MenuState.Transitioning;
            yield return Fade(levelSelectPanel, false);
            yield return Fade(titlePanel, true);
            yield return null; // Skip a frame so the Back click doesn't also count as "begin".
            state = MenuState.Title;
        }

        // ---------- Level Select ----------

        void InitLevelButtons()
        {
            for (int i = 0; i < levelButtons.Count; i++)
            {
                LevelSelectButton btn = levelButtons[i];
                if (btn == null || btn.Level == null) continue;

                LevelData data = btn.Level;
                bool unlocked = i == 0 || data.unlockedByDefault || LevelProgress.IsUnlocked(data.levelId);
                btn.Init(unlocked, Select);

                if (selected == null && unlocked) Select(data);
            }
        }

        void Select(LevelData data)
        {
            selected = data;
            foreach (var b in levelButtons)
                if (b != null) b.SetSelected(b.Level == data);

            if (selectedNameText != null) selectedNameText.text = data.displayName;
            if (selectedDescriptionText != null) selectedDescriptionText.text = data.description;
            if (startButton != null) startButton.interactable = !string.IsNullOrEmpty(data.sceneName);
        }

        void StartSelected()
        {
            if (state != MenuState.LevelSelect || selected == null) return;
            StartCoroutine(LoadLevelRoutine(selected));
        }

        IEnumerator LoadLevelRoutine(LevelData data)
        {
            state = MenuState.Transitioning;
            yield return Fade(levelSelectPanel, false);
            SceneManager.LoadScene(data.sceneName);
        }

        // ---------- Helpers ----------

        IEnumerator Fade(CanvasGroup group, bool show)
        {
            if (group == null) yield break;

            group.gameObject.SetActive(true);
            group.interactable = false;
            group.blocksRaycasts = false;

            float start = group.alpha;
            float end = show ? 1f : 0f;
            for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(start, end, t / fadeDuration);
                yield return null;
            }

            SetPanel(group, show);
        }

        static void SetPanel(CanvasGroup group, bool show)
        {
            if (group == null) return;
            group.alpha = show ? 1f : 0f;
            group.interactable = show;
            group.blocksRaycasts = show;
            group.gameObject.SetActive(show);
        }

        static bool AnyInputPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) return true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
            return false;
#else
            return Input.anyKeyDown;
#endif
        }

        static bool CancelPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
            if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) return true;
            return false;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
