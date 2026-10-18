using SandHouse.Combat;
using SandHouse.Player;
using SandHouse.Stats;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SandHouse.UI
{
    /// <summary>
    /// Bottom-left stat investment panel: weapon damage, magic damage and health levels plus the HP bar.
    /// Hold Stats (Tab) to upgrade: the cursor is freed and you click the stat you want (or press 1/2/3)
    /// while you keep moving. Let go of Tab to close.
    /// </summary>
    public class StatsPanel : MonoBehaviour
    {
        [SerializeField] PlayerStats stats;
        [SerializeField] Health health;
        [SerializeField] PlayerInputReader input;

        struct StatRowInfo
        {
            public StatType Type;
            public string Label;
            public Color Color;
            public Key Hotkey;
        }

        static readonly StatRowInfo[] RowInfos =
        {
            new StatRowInfo { Type = StatType.WeaponDamage, Label = "WEAPON DMG", Color = new Color(1f, 0.6f, 0.25f), Hotkey = Key.Digit1 },
            new StatRowInfo { Type = StatType.MagicDamage, Label = "MAGIC DMG", Color = new Color(0.6f, 0.5f, 1f), Hotkey = Key.Digit2 },
            new StatRowInfo { Type = StatType.Health, Label = "HEALTH", Color = new Color(0.95f, 0.3f, 0.3f), Hotkey = Key.Digit3 },
        };

        class StatRow
        {
            public Button Button;
            public Text Level, Value, Plus;
        }

        readonly StatRow[] rows = new StatRow[RowInfos.Length];
        Image panel, hpFill;
        Text pointsText, hpText, hintText;
        bool investing;

        void Awake() => BuildUI();

        void OnEnable()
        {
            if (stats != null) stats.Changed += Refresh;
        }

        void OnDisable()
        {
            if (stats != null) stats.Changed -= Refresh;
            SetInvesting(false);
        }

        void Start() => Refresh();

        void Update()
        {
            SetInvesting(input != null && input.StatsHeld && !UIState.IsPaused);

            if (investing && Keyboard.current != null)
            {
                foreach (var info in RowInfos)
                    if (Keyboard.current[info.Hotkey].wasPressedThisFrame)
                        Invest(info.Type);
            }

            if (health != null)
            {
                float fraction = health.Max > 0f ? health.Current / health.Max : 0f;
                hpFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
                hpText.text = $"{health.Current:0} / {health.Max:0}";
            }

            // Gently pulse the hint while there are points to spend.
            if (!investing && stats != null && stats.UnspentPoints > 0)
            {
                Color c = UIFactory.AccentColor;
                c.a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
                hintText.color = c;
            }
        }

        void SetInvesting(bool on)
        {
            if (investing == on) return;
            investing = on;
            UIState.SetOpen(this, on);
            Refresh();
        }

        void Invest(StatType type)
        {
            if (investing && stats != null) stats.Invest(type);
        }

        void Refresh()
        {
            if (stats == null || rows[0] == null) return;

            pointsText.text = stats.UnspentPoints == 1 ? "1 POINT" : $"{stats.UnspentPoints} POINTS";
            pointsText.color = stats.UnspentPoints > 0 ? UIFactory.AccentColor : UIFactory.MutedTextColor;

            for (int i = 0; i < RowInfos.Length; i++)
            {
                StatType type = RowInfos[i].Type;
                bool canUpgrade = investing && stats.CanInvest(type);
                rows[i].Button.interactable = canUpgrade;
                rows[i].Level.text = $"Lv {stats.GetLevel(type)}/{stats.MaxLevel}";
                rows[i].Value.text = stats.Describe(type);
                rows[i].Plus.text = canUpgrade ? $"+ [{i + 1}]" : "";
            }

            panel.color = investing ? new Color(0.12f, 0.09f, 0.05f, 0.93f) : UIFactory.PanelColor;
            if (investing)
                hintText.text = stats.UnspentPoints > 0 ? "Click a stat (or 1 / 2 / 3)   -   release TAB to close" : "No points - get kills to earn more";
            else
                hintText.text = stats.UnspentPoints > 0 ? "Hold TAB to upgrade" : "Hold TAB for stats";
            hintText.color = investing ? Color.white : UIFactory.MutedTextColor;
        }

        // ---------------------------------------------------------------- UI construction

        void BuildUI()
        {
            UIFactory.EnsureEventSystem();
            var canvas = UIFactory.CreateCanvas("Stats Canvas", transform, sortingOrder: 5, interactive: true);

            panel = UIFactory.CreatePanel(canvas.transform, "Stats Panel");
            panel.rectTransform.Place(new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(420f, 0f));
            UIFactory.VerticalLayout(panel.gameObject, spacing: 4f, padding: 14);
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var header = UIFactory.CreateRow(panel.transform, "Header", 30f);
            UIFactory.CreateText(header, "Title", "STATS", 24, TextAnchor.MiddleLeft, UIFactory.AccentColor).Size(flexibleWidth: 1f);
            pointsText = UIFactory.CreateText(header, "Points", "", 22, TextAnchor.MiddleRight).Size(width: 140f);

            for (int i = 0; i < RowInfos.Length; i++)
                rows[i] = BuildStatRow(panel.transform, RowInfos[i]);

            // HP bar.
            var bar = UIFactory.CreateImage(panel.transform, "HP Bar", UIFactory.TrackColor).Size(height: 24f);
            hpFill = UIFactory.CreateImage(bar.transform, "Fill", new Color(0.8f, 0.2f, 0.2f));
            hpFill.rectTransform.anchorMin = Vector2.zero;
            hpFill.rectTransform.anchorMax = Vector2.one;
            hpFill.rectTransform.offsetMin = hpFill.rectTransform.offsetMax = Vector2.zero;
            hpText = UIFactory.CreateText(bar.transform, "HP", "", 18, TextAnchor.MiddleCenter);
            hpText.rectTransform.Stretch();

            hintText = UIFactory.CreateText(panel.transform, "Hint", "", 17, TextAnchor.MiddleLeft, UIFactory.MutedTextColor).Size(height: 22f);
        }

        /// <summary>A whole stat row is one button: hover highlights it, click upgrades it.</summary>
        StatRow BuildStatRow(Transform parent, StatRowInfo info)
        {
            var row = UIFactory.CreateRow(parent, info.Label, 40f, spacing: 10f);
            row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 8, 0, 0);

            var background = row.gameObject.AddComponent<Image>();
            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = colors.selectedColor = new Color(1f, 1f, 1f, 0.06f);
            colors.highlightedColor = new Color(info.Color.r, info.Color.g, info.Color.b, 0.35f);
            colors.pressedColor = new Color(info.Color.r, info.Color.g, info.Color.b, 0.6f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.02f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            button.onClick.AddListener(() => Invest(info.Type));

            UIFactory.CreateImage(row, "Color", info.Color).Size(width: 6f);
            UIFactory.CreateText(row, "Label", info.Label, 21).Size(flexibleWidth: 1f);
            return new StatRow
            {
                Button = button,
                Level = UIFactory.CreateText(row, "Level", "", 19, TextAnchor.MiddleRight, UIFactory.MutedTextColor).Size(width: 72f),
                Value = UIFactory.CreateText(row, "Value", "", 21, TextAnchor.MiddleRight, info.Color).Size(width: 84f),
                Plus = UIFactory.CreateText(row, "Plus", "", 19, TextAnchor.MiddleRight, UIFactory.AccentColor).Size(width: 52f),
            };
        }
    }
}
