using System;
using System.Collections.Generic;
using Evergreen.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>
    /// The research screen (layout "A" from the skill tree mockups).
    /// The screen itself is built ahead of time in the SkillTree prefab with
    /// Tools > Evergreen > Build Skill Tree, from the Nodes list below. Everything it makes
    /// (header, node cards, lines, detail card) is a normal object you can move and restyle in the
    /// prefab; this script only fills in text and colors at runtime and handles research.
    ///   - A node is either a building (added to the build menu when researched) or an upgrade
    ///     (made available in the upgrade menu when researched, see ResearchUnlocks).
    ///   - A node with no parent can be researched from the start; the rest open once their parent is researched.
    ///   - Research costs happiness points, like the old tree.
    /// Rerun the menu item after changing the Nodes list; it replaces the "Research UI" child.
    /// </summary>
    public class SkillTreeView : MonoBehaviour
    {
        [Serializable]
        public class Node
        {
            [Tooltip("Short unique name other nodes use as their parent id.")]
            public string id;
            [Tooltip("Fill in a building OR an upgrade.")]
            public BuildingDefinition building;
            public BuildingUpgrade upgrade;
            [Min(0f)] public float researchCost = 50f;
            [Tooltip("Id of the node that must be researched first. Leave empty to make it available from the start.")]
            public string parentId;
            [Tooltip("Card centre, in px from the tree area's top-left corner (y goes down). Used when the tree is built.")]
            public Vector2 position;

            public string DisplayName => building != null ? building.displayName : (upgrade != null ? upgrade.displayName : id);
        }

        /// <summary>The prefab objects for one node, filled in by the build tool.</summary>
        [Serializable]
        public class NodeUI
        {
            public string id;
            public Button button;
            public Image ring;
            public Image icon;
            public Image lockBadge;
            public Text nameText;
            public Image pill;
            public Text pillText;
            public CanvasGroup body;
            [Tooltip("Line pieces from this node's parent to this node.")]
            public List<Image> lines = new List<Image>();
        }

        /// <summary>How often (seconds) points, affordability and button states are re-checked.</summary>
        const float RefreshInterval = 0.25f;

        internal static readonly Color TreeBackground = ArcadePalette.Hex(0x1A201E);
        internal static readonly Color BadgeColor = ArcadePalette.Hex(0x37403D);
        static readonly Color BadgeImageColor = new Color(0f, 0f, 0f, 0f); // icon badge behind node icons: transparent
        static readonly Color LockedPill = ArcadePalette.Hex(0x4A2A28);
        static readonly Color LockedPillText = ArcadePalette.Hex(0xFFB8AE);
        internal static readonly Color AmberPillText = ArcadePalette.Hex(0x3A2600);
        static readonly Color LimePillText = ArcadePalette.Hex(0x1C3A08);
        static readonly Color LockedName = ArcadePalette.Hex(0x8E9893);

        [SerializeField] List<Node> nodes = new List<Node>();
        [Tooltip("Resource icons for the detail card. Falls back to the build menu's.")]
        [SerializeField] ResourceIcons icons;

        [Header("Layout used by the build tool (px on a 1920x1080 screen)")]
        [SerializeField] Vector2 treeAreaPosition = new Vector2(40f, 136f);
        [SerializeField] Vector2 treeAreaSize = new Vector2(1260f, 904f);
        [SerializeField] Vector2 detailAreaPosition = new Vector2(1340f, 136f);
        [SerializeField] Vector2 detailAreaSize = new Vector2(540f, 904f);
        [SerializeField] float cardWidth = 330f;
        [SerializeField] float cardHeight = 108f;
        [SerializeField] float lineThickness = 6f;
        [SerializeField] string title = "Research";
        [SerializeField] string subtitle = "Spend happiness points to add green buildings and upgrades to the build menu";

        [Header("Built by Tools > Evergreen > Build Skill Tree")]
        [SerializeField] List<NodeUI> nodeUIs = new List<NodeUI>();
        [SerializeField] RectTransform detailArea;
        [SerializeField] BuildCard detailCard;
        [SerializeField] Text opensText;
        [SerializeField] Text pointsText;

        class NodeState
        {
            public Node data;
            public NodeUI ui;
            public NodeState parent;
            public readonly List<NodeState> children = new List<NodeState>();
            public bool researched;
            public bool Unlocked => parent == null || parent.researched;
        }

        readonly List<NodeState> states = new List<NodeState>();
        BuildMenu menu;
        ResourceManager resources;
        NodeState selected;
        bool ready;
        float secondsUntilRefresh;

        // ---------- Unity lifecycle ----------

        void Start()
        {
            menu = FindFirstObjectByType<BuildMenu>(FindObjectsInactive.Include);
            resources = ResourceManager.Instance;
            if (icons == null && menu != null) icons = menu.Icons;

            if (nodeUIs.Count == 0)
            {
                Debug.LogWarning("SkillTreeView: the tree hasn't been built yet. Run Tools > Evergreen > Build Skill Tree.", this);
                return;
            }

            var byId = new Dictionary<string, NodeState>();
            foreach (Node node in nodes)
            {
                if (node == null || (node.building == null && node.upgrade == null)) continue;
                var state = new NodeState { data = node, ui = nodeUIs.Find(u => u.id == node.id) };
                if (state.ui == null)
                {
                    Debug.LogWarning($"SkillTreeView: node '{node.id}' has no card. Rebuild the tree.", this);
                    continue;
                }
                states.Add(state);
                byId[node.id] = state;
            }
            foreach (NodeState state in states)
            {
                if (!string.IsNullOrEmpty(state.data.parentId) && byId.TryGetValue(state.data.parentId, out NodeState parent) && parent != state)
                {
                    state.parent = parent;
                    parent.children.Add(state);
                }
                NodeState captured = state;
                if (state.ui.button != null) state.ui.button.onClick.AddListener(() => Select(captured));
            }

            if (detailCard != null && detailCard.PlaceButton != null) detailCard.PlaceButton.onClick.AddListener(ResearchSelected);

            ready = true;
            Select(FirstChoice());
        }

        void OnEnable()
        {
            if (ready) Refresh();
        }

        void Update()
        {
            if (!ready) return;
            secondsUntilRefresh -= Time.unscaledDeltaTime;
            if (secondsUntilRefresh > 0f) return;
            secondsUntilRefresh = RefreshInterval;
            Refresh();
        }

        // ---------- Selecting and researching ----------

        NodeState FirstChoice()
        {
            foreach (NodeState state in states) if (state.Unlocked && !state.researched) return state;
            return states.Count > 0 ? states[0] : null;
        }

        void Select(NodeState state)
        {
            if (state == null) return;
            selected = state;
            ShowDetail(state);
            Refresh();
        }

        /// <summary>Fills the detail card with the selected node and opens its details.</summary>
        void ShowDetail(NodeState state)
        {
            if (detailCard == null) return;

            if (state.data.building != null) detailCard.SetupTreeNode(state.data.building, icons);
            else detailCard.SetupTreeNode(state.data.upgrade, icons);
            detailCard.SetAffordable(true, 0f); // clears the template's locked look; the button is relabelled in Refresh
            detailCard.SetOpen(true);
            if (detailCard.HeaderButton != null) detailCard.HeaderButton.enabled = false;

            // Shrink the card if it's taller than the panel.
            var rect = (RectTransform)detailCard.transform;
            rect.localScale = Vector3.one;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            float room = (detailArea != null ? detailArea.rect.height : detailAreaSize.y) - 48f;
            if (rect.rect.height > room) rect.localScale = Vector3.one * (room / rect.rect.height);

            if (opensText != null)
            {
                var names = new List<string>();
                foreach (NodeState child in state.children) names.Add(child.data.DisplayName);
                float used = 24f + rect.rect.height * rect.localScale.y + 24f;
                bool fits = (detailArea != null ? detailArea.rect.height : detailAreaSize.y) - used >= 70f;
                opensText.gameObject.SetActive(names.Count > 0 && fits);
                opensText.text = "OPENS NEXT\n" + string.Join("  ·  ", names);
                opensText.rectTransform.anchoredPosition = new Vector2(opensText.rectTransform.anchoredPosition.x, -used);
            }
        }

        void ResearchSelected()
        {
            NodeState state = selected;
            if (state == null || state.researched || !state.Unlocked) return;
            if (resources != null)
            {
                if (resources.Happiness < state.data.researchCost) return;
                resources.SpendHappiness(state.data.researchCost);
            }

            state.researched = true;
            if (state.data.building != null && menu != null) menu.AddCard(state.data.building);
            if (state.data.upgrade != null) ResearchUnlocks.Unlock(state.data.upgrade);
            Refresh();
        }

        /// <summary>Points readout, each card's state, line colors and the Research button.</summary>
        void Refresh()
        {
            float points = resources != null ? resources.Happiness : float.PositiveInfinity;
            if (pointsText != null) pointsText.text = $"{Mathf.FloorToInt(points)} pts";

            foreach (NodeState state in states)
            {
                ApplyNodeState(state, points);
                foreach (Image segment in state.ui.lines)
                    if (segment != null) segment.color = state.Unlocked ? ArcadePalette.Lime : ArcadePalette.Track;
            }

            if (detailCard != null && selected != null)
            {
                bool canAfford = points >= selected.data.researchCost;
                string label;
                if (selected.researched) label = selected.data.building != null ? "In build menu" : "Upgrade unlocked";
                else if (!selected.Unlocked) label = "Needs " + selected.parent.data.DisplayName;
                else if (!canAfford) label = $"{Mathf.CeilToInt(selected.data.researchCost - points)} more pts";
                else label = $"Research for {selected.data.researchCost:0} pts";
                detailCard.SetActionButton(label, !selected.researched && selected.Unlocked && canAfford);
            }
        }

        /// <summary>Locked / ready / too expensive / researched / selected look for one node.</summary>
        void ApplyNodeState(NodeState state, float points)
        {
            NodeUI ui = state.ui;
            bool locked = !state.Unlocked && !state.researched;
            bool canAfford = points >= state.data.researchCost;

            if (ui.ring != null) ui.ring.enabled = state == selected;
            if (ui.lockBadge != null) ui.lockBadge.enabled = locked && ui.lockBadge.sprite != null;
            if (ui.icon != null) ui.icon.color = locked ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
            if (ui.nameText != null) ui.nameText.color = locked ? LockedName : Color.white;
            if (ui.body != null) ui.body.alpha = locked ? 0.8f : 1f;
            if (ui.pill == null || ui.pillText == null) return;

            if (state.researched)
            {
                ui.pill.color = ArcadePalette.Lime;
                ui.pillText.text = "Done";
                ui.pillText.color = LimePillText;
            }
            else if (locked)
            {
                ui.pill.color = LockedPill;
                ui.pillText.text = "Locked";
                ui.pillText.color = LockedPillText;
            }
            else
            {
                ui.pill.color = canAfford ? ArcadePalette.Amber : LockedPill;
                ui.pillText.text = $"{state.data.researchCost:0} pts";
                ui.pillText.color = canAfford ? AmberPillText : LockedPillText;
            }
        }

#if UNITY_EDITOR
        // ---------- Build tool (edit mode only) ----------

        /// <summary>
        /// Replaces the "Research UI" child with a freshly built screen. Called by
        /// Tools > Evergreen > Build Skill Tree with the build menu's card template (from the HUD prefab).
        /// </summary>
        public void EditorBuild(BuildCard cardTemplate, ResourceIcons resourceIcons)
        {
            if (resourceIcons != null) icons = resourceIcons;

            Transform old = transform.Find("Research UI");
            if (old != null) DestroyImmediate(old.gameObject);
            nodeUIs.Clear();

            FixCanvas();
            HideOldTree();

            Image templateImage = cardTemplate.GetComponent<Image>();
            Sprite rounded = templateImage != null ? templateImage.sprite : null;
            Font bold = FontOf(cardTemplate.transform, "Header/Names/Name");
            Font regular = FontOf(cardTemplate.transform, "Header/Names/Lifespan") ?? bold;
            var kit = new Kit { rounded = rounded, bold = bold, regular = regular };

            RectTransform root = MakeLayer("Research UI", transform);

            // Header.
            Text titleText = MakeText(kit, "Title", root, title, true, 44, Color.white, TextAnchor.MiddleLeft);
            Place(titleText.rectTransform, new Vector2(40f, 32f), new Vector2(260f, 72f));
            Text subtitleText = MakeText(kit, "Subtitle", root, subtitle, false, 20, ArcadePalette.TextMuted, TextAnchor.MiddleLeft);
            Place(subtitleText.rectTransform, new Vector2(270f, 40f), new Vector2(900f, 60f));

            RectTransform pill = MakePanel(kit, "Points", root, Vector2.zero, new Vector2(200f, 60f), ArcadePalette.Card);
            pill.anchorMin = pill.anchorMax = pill.pivot = new Vector2(1f, 1f);
            pill.anchoredPosition = new Vector2(-260f, -38f);
            if (icons != null && icons.happiness != null)
            {
                Image heart = MakeIcon("Icon", pill, icons.happiness);
                Place(heart.rectTransform, new Vector2(16f, 12f), new Vector2(36f, 36f));
            }
            pointsText = MakeText(kit, "Value", pill, "60 pts", true, 28, Color.white, TextAnchor.MiddleLeft);
            Place(pointsText.rectTransform, new Vector2(62f, 0f), new Vector2(130f, 60f));

            Transform exit = transform.Find("Exit");
            if (exit is RectTransform exitRect)
            {
                exitRect.anchorMin = exitRect.anchorMax = exitRect.pivot = new Vector2(1f, 1f);
                exitRect.anchoredPosition = new Vector2(-40f, -36f);
                exitRect.sizeDelta = new Vector2(200f, 64f);
                exitRect.SetAsLastSibling();
            }

            // Tree.
            RectTransform tree = MakePanel(kit, "Tree Area", root, treeAreaPosition, treeAreaSize, TreeBackground);
            RectTransform lineLayer = MakeLayer("Lines", tree);
            RectTransform cardLayer = MakeLayer("Cards", tree);

            var byId = new Dictionary<string, Node>();
            foreach (Node node in nodes)
                if (node != null && !string.IsNullOrEmpty(node.id) && (node.building != null || node.upgrade != null)) byId[node.id] = node;

            foreach (Node node in byId.Values) nodeUIs.Add(BuildNodeCard(kit, node, cardLayer));
            foreach (Node node in byId.Values)
            {
                if (string.IsNullOrEmpty(node.parentId) || !byId.TryGetValue(node.parentId, out Node parent)) continue;
                NodeUI ui = nodeUIs.Find(u => u.id == node.id);
                BuildLine(kit, parent, node, lineLayer, ui.lines);
            }

            // Detail panel: a copy of the build menu's card, filled in at runtime.
            detailArea = MakePanel(kit, "Detail Area", root, detailAreaPosition, detailAreaSize, ArcadePalette.Panel);
            detailCard = Instantiate(cardTemplate, detailArea);
            detailCard.name = "Selected Card";
            detailCard.gameObject.SetActive(true);
            var cardRect = (RectTransform)detailCard.transform;
            cardRect.anchorMin = cardRect.anchorMax = cardRect.pivot = new Vector2(0f, 1f);
            cardRect.anchoredPosition = new Vector2(24f, -24f);
            cardRect.sizeDelta = new Vector2(detailAreaSize.x - 48f, cardRect.sizeDelta.y);
            ContentSizeFitter fitter = detailCard.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = detailCard.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            opensText = MakeText(kit, "Opens Next", detailArea, "OPENS NEXT", false, 20, ArcadePalette.TextMuted, TextAnchor.UpperLeft);
            Place(opensText.rectTransform, new Vector2(28f, 760f), new Vector2(detailAreaSize.x - 56f, 90f));
            opensText.gameObject.SetActive(false);
        }

        class Kit
        {
            public Sprite rounded;
            public Font bold;
            public Font regular;
        }

        NodeUI BuildNodeCard(Kit kit, Node node, RectTransform parent)
        {
            const float pad = 14f, badge = 80f, pillW = 104f, pillH = 34f;
            var ui = new NodeUI { id = node.id };

            RectTransform rect = MakeLayer("Node - " + node.DisplayName, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(node.position.x, -node.position.y);
            rect.sizeDelta = new Vector2(cardWidth, cardHeight);

            ui.ring = MakeImage(kit, "Selected Ring", rect, ArcadePalette.Lime);
            Stretch(ui.ring.rectTransform, -5f);
            ui.ring.raycastTarget = false;
            ui.ring.enabled = false;

            Image bg = MakeImage(kit, "Card", rect, ArcadePalette.Card);
            Stretch(bg.rectTransform, 0f);
            ui.button = bg.gameObject.AddComponent<Button>();
            ui.button.targetGraphic = bg;
            ui.body = bg.gameObject.AddComponent<CanvasGroup>();

            Image badgeBg = MakeImage(kit, "Badge", bg.transform, BadgeImageColor);
            badgeBg.raycastTarget = false;
            Place(badgeBg.rectTransform, new Vector2(pad, (cardHeight - badge) / 2f), new Vector2(badge, badge));

            ui.icon = MakeIcon("Icon", badgeBg.transform, IconFor(node));
            Place(ui.icon.rectTransform, new Vector2(12f, 12f), new Vector2(badge - 24f, badge - 24f));
            ui.lockBadge = MakeIcon("Lock", badgeBg.transform, icons != null ? icons.locked : null);
            Place(ui.lockBadge.rectTransform, new Vector2(badge - 30f, badge - 32f), new Vector2(34f, 34f));

            float textX = pad + badge + 14f;
            ui.nameText = MakeText(kit, "Name", bg.transform, node.DisplayName, true, 22, Color.white, TextAnchor.MiddleLeft);
            ui.nameText.resizeTextForBestFit = true;
            ui.nameText.resizeTextMinSize = 14;
            ui.nameText.resizeTextMaxSize = 22;
            ui.nameText.verticalOverflow = VerticalWrapMode.Truncate;
            Place(ui.nameText.rectTransform, new Vector2(textX, pad - 2f), new Vector2(cardWidth - textX - pillW - pad - 8f, 46f));

            ui.pill = MakeImage(kit, "Cost Pill", bg.transform, ArcadePalette.Amber);
            ui.pill.raycastTarget = false;
            Place(ui.pill.rectTransform, new Vector2(cardWidth - pad - pillW, pad), new Vector2(pillW, pillH));
            ui.pillText = MakeText(kit, "Cost", ui.pill.transform, $"{node.researchCost:0} pts", true, 17, AmberPillText, TextAnchor.MiddleCenter);
            Stretch(ui.pillText.rectTransform, 0f);

            ResourceYield yearly = node.building != null ? node.building.yearlyYield : node.upgrade.yearlyChange;
            float chipX = textX;
            float chipY = cardHeight - pad - 32f;
            if (icons != null)
            {
                AddChip(kit, bg.transform, icons.money, yearly.money, "", ref chipX, chipY);
                AddChip(kit, bg.transform, icons.eco, yearly.eco, "", ref chipX, chipY);
                AddChip(kit, bg.transform, icons.population, yearly.population, "", ref chipX, chipY);
                AddChip(kit, bg.transform, icons.happiness, yearly.happiness, "%", ref chipX, chipY);
            }
            return ui;
        }

        /// <summary>One small "icon +value" chip; skipped for zero values or when the row is full.</summary>
        void AddChip(Kit kit, Transform parent, Sprite resourceIcon, float value, string suffix, ref float x, float y)
        {
            if (ArcadePalette.IsNearlyZero(value)) return;
            const float width = 74f, height = 32f;
            if (x + width > cardWidth - 10f) return;

            Image chip = MakeImage(kit, "Chip", parent, BadgeColor);
            chip.raycastTarget = false;
            Place(chip.rectTransform, new Vector2(x, y), new Vector2(width, height));
            Image chipIcon = MakeIcon("Icon", chip.transform, resourceIcon);
            Place(chipIcon.rectTransform, new Vector2(6f, 5f), new Vector2(22f, 22f));
            Text chipText = MakeText(kit, "Value", chip.transform, ArcadePalette.Signed(value, "", suffix), true, 15,
                ArcadePalette.DeltaColor(value), TextAnchor.MiddleLeft);
            chipText.horizontalOverflow = HorizontalWrapMode.Overflow;
            Place(chipText.rectTransform, new Vector2(30f, 0f), new Vector2(width - 32f, height));
            x += width + 6f;
        }

        /// <summary>An elbow line from the right edge of the parent card to the left edge of the child.</summary>
        void BuildLine(Kit kit, Node parent, Node child, RectTransform layer, List<Image> into)
        {
            Vector2 from = new Vector2(parent.position.x + cardWidth / 2f, -parent.position.y);
            Vector2 to = new Vector2(child.position.x - cardWidth / 2f, -child.position.y);
            float midX = (from.x + to.x) / 2f;
            into.Add(Segment("Line to " + child.DisplayName, layer, from, new Vector2(midX, from.y)));
            into.Add(Segment("Line to " + child.DisplayName, layer, new Vector2(midX, from.y), new Vector2(midX, to.y)));
            into.Add(Segment("Line to " + child.DisplayName, layer, new Vector2(midX, to.y), to));
        }

        Image Segment(string name, RectTransform layer, Vector2 a, Vector2 b)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(layer, false);
            Image image = go.GetComponent<Image>();
            image.color = ArcadePalette.Track;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = (a + b) / 2f;
            // Padded by the thickness so the corners join up.
            rect.sizeDelta = new Vector2(Mathf.Abs(b.x - a.x) + lineThickness, Mathf.Abs(b.y - a.y) + lineThickness);
            return image;
        }

        Sprite IconFor(Node node)
        {
            if (node.building != null) return node.building.icon;
            if (node.upgrade.icon != null) return node.upgrade.icon;
            if (icons == null) return null;
            ResourceYield c = node.upgrade.yearlyChange;
            // Same rule as BuildCard: the resource the upgrade moves most (money runs ~10x bigger).
            Sprite best = icons.money;
            float size = Mathf.Abs(c.money) / 10f;
            if (Mathf.Abs(c.eco) > size) { best = icons.eco; size = Mathf.Abs(c.eco); }
            if (Mathf.Abs(c.population) / 5f > size) { best = icons.population; size = Mathf.Abs(c.population) / 5f; }
            if (Mathf.Abs(c.happiness) > size) best = icons.happiness;
            return best;
        }

        /// <summary>Scale With Screen Size at 1920x1080, drawn above the HUD.</summary>
        void FixCanvas()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null && canvas.sortingOrder < 20) canvas.sortingOrder = 20;
            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null) return;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        /// <summary>Turns off the old hand-placed tree and its info panel (kept in the prefab, not deleted).</summary>
        void HideOldTree()
        {
            foreach (NodeBehavior oldNode in GetComponentsInChildren<NodeBehavior>(true)) oldNode.gameObject.SetActive(false);
            string[] oldParts = { "Node Info Display", "Building Title", "Building Description", "Cost Label",
                "Happiness Icon", "Purchase", "Tree Line", "Tree Line (1)" };
            foreach (string part in oldParts)
            {
                Transform t = transform.Find(part);
                if (t != null) t.gameObject.SetActive(false);
            }
        }

        static void Place(RectTransform rect, Vector2 topLeft, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
            rect.sizeDelta = size;
        }

        static void Stretch(RectTransform rect, float outset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(-outset, -outset);
            rect.offsetMax = new Vector2(outset, outset);
        }

        static RectTransform MakeLayer(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        static RectTransform MakePanel(Kit kit, string name, Transform parent, Vector2 topLeft, Vector2 size, Color color)
        {
            Image image = MakeImage(kit, name, parent, color);
            Place(image.rectTransform, topLeft, size);
            return image.rectTransform;
        }

        static Image MakeImage(Kit kit, string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            if (kit.rounded != null)
            {
                image.sprite = kit.rounded;
                image.type = Image.Type.Sliced;
            }
            return image;
        }

        static Image MakeIcon(string name, Transform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        static Text MakeText(Kit kit, string name, Transform parent, string value, bool bold, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.text = value;
            Font font = bold ? kit.bold : kit.regular;
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static Font FontOf(Transform root, string path)
        {
            Transform t = root.Find(path);
            Text text = t != null ? t.GetComponent<Text>() : null;
            return text != null ? text.font : null;
        }
#endif
    }
}
