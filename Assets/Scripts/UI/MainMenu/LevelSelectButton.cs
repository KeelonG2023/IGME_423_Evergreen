using System;
using UnityEngine;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>
    /// A level card that already exists in the scene. Assign its LevelData in the Inspector;
    /// MainMenuController only toggles lock/selection state at runtime.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class LevelSelectButton : MonoBehaviour
    {
        [SerializeField] LevelData level;

        [Header("Parts")]
        [SerializeField] Image background;
        [SerializeField] Text nameText;
        [SerializeField] Text difficultyText;
        [SerializeField] GameObject lockOverlay;
        [SerializeField] Outline selectionOutline;

        Button button;
        Action<LevelData> onSelected;

        public LevelData Level => level;

        public void Init(bool unlocked, Action<LevelData> onClick)
        {
            if (button == null) button = GetComponent<Button>();
            onSelected = onClick;

            if (nameText != null) nameText.text = level.displayName;
            if (difficultyText != null) difficultyText.text = level.difficulty.ToUpperInvariant();
            if (lockOverlay != null) lockOverlay.SetActive(!unlocked);

            if (background != null)
            {
                Color c = background.color;
                c.a = unlocked ? 1f : 0.55f;
                background.color = c;
            }

            button.interactable = unlocked;
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (selectionOutline != null) selectionOutline.enabled = selected;
        }

        void HandleClick() => onSelected?.Invoke(level);
    }
}
