using Evergreen.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>
    /// Compact two-line summary of the selected building, shown at the top of the upgrade list.
    ///   Line 1: name (left) · "40 yrs" (right)
    ///   Line 2: inline yields, icon + signed number, no chip backgrounds ("coin +300   recycle −20   happy-emoji")
    /// Built entirely in code by UpgradeMenu so it needs no prefab. Icons come from ResourceIcons (Art/UI).
    /// </summary>
    public class BuildingSummaryHeader : MonoBehaviour
    {
        // Sizes match the build card (name 33, lifespan 23) so the panel reads as one family.
        const int NameSize = 33;
        const int MetaSize = 23;
        const int YieldSize = 27;
        const float IconSize = 30f;
        const float YieldGap = 26f;

        Text nameText;
        Text metaText;
        readonly Image[] yieldIcons = new Image[4];
        readonly Text[] yieldTexts = new Text[4];

        /// <summary>Creates the header under <paramref name="parent"/>, styled from the build card template.</summary>
        public static BuildingSummaryHeader Create(Transform parent, Font boldFont, Font regularFont, Sprite background)
        {
            var root = new GameObject("Building Summary", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            root.transform.SetParent(parent, false);

            Image bg = root.GetComponent<Image>();
            bg.sprite = background;
            bg.type = background != null ? Image.Type.Sliced : Image.Type.Simple;
            bg.color = ArcadePalette.Card;
            bg.raycastTarget = false;

            var stack = root.GetComponent<VerticalLayoutGroup>();
            stack.padding = new RectOffset(27, 27, 22, 22);
            stack.spacing = 10f;
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;

            var header = root.AddComponent<BuildingSummaryHeader>();

            // Line 1: name ........ years left
            Transform line1 = NewRow("Name Row", root.transform, 12f, TextAnchor.LowerLeft);
            header.nameText = NewText("Name", line1, boldFont, NameSize, Color.white, TextAnchor.LowerLeft);
            header.nameText.GetComponent<LayoutElement>().flexibleWidth = 1f;
            header.metaText = NewText("Meta", line1, regularFont, MetaSize, ArcadePalette.TextMuted, TextAnchor.LowerRight);

            // Line 2: inline yields
            Transform line2 = NewRow("Yields Row", root.transform, 0f, TextAnchor.MiddleLeft);
            for (int i = 0; i < 4; i++)
            {
                Transform group = NewRow("Yield " + i, line2, 6f, TextAnchor.MiddleLeft);
                var spacer = group.gameObject.AddComponent<LayoutElement>();
                spacer.minWidth = 0f;

                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                iconGo.transform.SetParent(group, false);
                var iconLayout = iconGo.GetComponent<LayoutElement>();
                iconLayout.preferredWidth = iconLayout.minWidth = IconSize;
                iconLayout.preferredHeight = iconLayout.minHeight = IconSize;
                header.yieldIcons[i] = iconGo.GetComponent<Image>();
                header.yieldIcons[i].preserveAspect = true;
                header.yieldIcons[i].raycastTarget = false;

                header.yieldTexts[i] = NewText("Value", group, boldFont, YieldSize, Color.white, TextAnchor.MiddleLeft);

                // Gap after each group (cheaper than nesting another layout).
                var gap = new GameObject("Gap", typeof(RectTransform), typeof(LayoutElement));
                gap.transform.SetParent(group, false);
                gap.GetComponent<LayoutElement>().preferredWidth = YieldGap - 6f;
            }

            return header;
        }

        /// <summary>Fills or refreshes every field from the building. Safe to call every refresh tick.</summary>
        public void Refresh(BuildingYield building, ResourceIcons icons)
        {
            if (building == null) return;

            nameText.text = building.DisplayName;

            string age;
            if (building.IsExpired) age = "Expired";
            else if (float.IsInfinity(building.YearsRemaining)) age = "Forever";
            else age = $"{Mathf.CeilToInt(building.YearsRemaining)} yrs";

            metaText.text = age;
            metaText.color = building.IsExpired ? ArcadePalette.Coral : ArcadePalette.TextMuted;

            ResourceYield yearly = building.YearlyYield;
            int slot = 0;
            if (icons != null)
            {
                FillRating(ref slot, icons, building.HappinessRating);
                Fill(ref slot, icons.money, yearly.money, "");
                Fill(ref slot, icons.eco, yearly.eco, "");
                Fill(ref slot, icons.population, yearly.population, "");
            }
            for (int i = slot; i < 4; i++) yieldIcons[i].transform.parent.gameObject.SetActive(false);
        }

        /// <summary>Happiness rating emoji with no number.</summary>
        void FillRating(ref int slot, ResourceIcons icons, HappinessRating rating)
        {
            if (slot >= 4) return;
            Sprite emoji = icons.happinessRatings != null ? icons.happinessRatings.EmojiFor(rating) : null;
            yieldIcons[slot].transform.parent.gameObject.SetActive(true);
            yieldIcons[slot].sprite = emoji != null ? emoji : icons.happiness;
            yieldTexts[slot].text = "";
            slot++;
        }

        void Fill(ref int slot, Sprite sprite, float perYear, string suffix)
        {
            if (ArcadePalette.IsNearlyZero(perYear) || slot >= 4) return;
            yieldIcons[slot].transform.parent.gameObject.SetActive(true);
            yieldIcons[slot].sprite = sprite;
            yieldTexts[slot].text = ArcadePalette.Signed(perYear, "", suffix);
            yieldTexts[slot].color = ArcadePalette.DeltaColor(perYear);
            slot++;
        }

        // ---------- Builders ----------

        static Transform NewRow(string name, Transform parent, float spacing, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            var row = go.GetComponent<HorizontalLayoutGroup>();
            row.spacing = spacing;
            row.childAlignment = align;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            return go.transform;
        }

        static Text NewText(string name, Transform parent, Font font, int size, Color color, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
