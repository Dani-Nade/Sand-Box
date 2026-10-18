using SandHouse.Run;
using SandHouse.Stats;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SandHouse.UI
{
    /// <summary>Modal choose-one-of-three reward / results overlay, plus the current room objective.</summary>
    public class RunRewardPanel : MonoBehaviour
    {
        RoguelikeRun run;
        GameObject overlay, cards;
        Text objective, title, subtitle;
        Button continueButton, restartButton;
        readonly Button[] buttons = new Button[3];
        readonly Text[] names = new Text[3], descriptions = new Text[3], levels = new Text[3];

        void Awake() => BuildUI();
        public void Bind(RoguelikeRun owner)
        {
            if (run != null) run.Changed -= Refresh;
            run = owner;
            run.Changed += Refresh;
            Refresh();
        }
        void OnDestroy() { if (run != null) run.Changed -= Refresh; }

        void Update()
        {
            if (run == null || run.Phase != RunPhase.Reward || Keyboard.current == null) return;
            if (Keyboard.current.digit1Key.wasPressedThisFrame) run.TrySelectReward(0);
            else if (Keyboard.current.digit2Key.wasPressedThisFrame) run.TrySelectReward(1);
            else if (Keyboard.current.digit3Key.wasPressedThisFrame) run.TrySelectReward(2);
        }

        void Refresh()
        {
            if (run == null) return;
            objective.text = $"LEVEL {run.LevelIndex + 1}/{RoguelikeRun.LevelCount} - {run.LevelName}\nEnemies remaining: {run.EnemiesRemaining}";
            bool reward = run.Phase == RunPhase.Reward;
            bool ended = run.Phase == RunPhase.Complete || run.Phase == RunPhase.Defeated;
            overlay.SetActive(reward || ended);
            cards.SetActive(reward && run.Choices.Count > 0);
            continueButton.gameObject.SetActive(reward && run.Choices.Count == 0);
            restartButton.gameObject.SetActive(ended);
            if (reward)
            {
                title.text = $"LEVEL {run.LevelIndex + 1} CLEARED";
                subtitle.text = run.Choices.Count == 0 ? "All gun upgrades are maxed. Recover 20% health and continue."
                    : $"CHOOSE ONE GUN UPGRADE - FREE\n+1 base-stat point earned. { (run.LevelIndex + 1 < RoguelikeRun.LevelCount ? "Your choice starts the next level." : "Claim your final reward to finish the run.")}";
                for (int i = 0; i < buttons.Length; i++)
                {
                    bool available = i < run.Choices.Count;
                    buttons[i].gameObject.SetActive(available);
                    if (!available) continue;
                    var type = run.Choices[i];
                    names[i].text = $"[{i + 1}]  {GunUpgrades.DisplayName(type)}";
                    descriptions[i].text = GunUpgrades.Description(type);
                    levels[i].text = $"STACK {run.Player.GunUpgrades.GetStacks(type)} -> {run.Player.GunUpgrades.GetStacks(type) + 1} / {GunUpgrades.MaxStacks(type)}\nApplies to every gun this run";
                }
            }
            else if (ended)
            {
                title.text = run.Phase == RunPhase.Complete ? "RUN COMPLETE" : "RUN ENDED";
                subtitle.text = run.Phase == RunPhase.Complete ? "All three levels cleared. Start a fresh run with a new build."
                    : $"Fell in {run.LevelName}. Try again with a fresh build.";
            }
        }

        void BuildUI()
        {
            UIFactory.EnsureEventSystem();
            var canvas = UIFactory.CreateCanvas("Run Rewards Canvas", transform, 40, true);
            objective = UIFactory.CreateText(canvas.transform, "Objective", "", 24, TextAnchor.UpperCenter, UIFactory.AccentColor);
            objective.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(650f, 90f));
            var dim = UIFactory.CreateImage(canvas.transform, "Run overlay", new Color(0.025f, 0.025f, 0.04f, 0.96f));
            dim.raycastTarget = true; dim.rectTransform.Stretch(); overlay = dim.gameObject;
            var panel = UIFactory.CreateRect(dim.transform, "Reward layout");
            panel.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 620f));
            UIFactory.VerticalLayout(panel.gameObject, 22f, 28);
            title = UIFactory.CreateText(panel, "Title", "", 48, TextAnchor.MiddleCenter, UIFactory.AccentColor).Size(height: 65f);
            subtitle = UIFactory.CreateText(panel, "Subtitle", "", 24, TextAnchor.MiddleCenter).Size(height: 78f);
            var row = UIFactory.CreateRow(panel, "Three choices", 290f, 22f); cards = row.gameObject;
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var button = UIFactory.CreateButton(row, "", () => run?.TrySelectReward(index));
                button.Size(flexibleWidth: 1f); buttons[i] = button;
                var oldLabel = button.GetComponentInChildren<Text>(); Destroy(oldLabel.gameObject);
                var contents = UIFactory.CreateRect(button.transform, "Card content").Stretch(20f);
                UIFactory.VerticalLayout(contents.gameObject, 18f, 0);
                names[i] = UIFactory.CreateText(contents, "Name", "", 28, TextAnchor.MiddleCenter, UIFactory.AccentColor).Size(height: 58f);
                names[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                descriptions[i] = UIFactory.CreateText(contents, "Effect", "", 25, TextAnchor.MiddleCenter).Size(height: 105f);
                descriptions[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                levels[i] = UIFactory.CreateText(contents, "Stacks", "", 21, TextAnchor.MiddleCenter, UIFactory.MutedTextColor).Size(height: 52f);
            }
            continueButton = UIFactory.CreateButton(panel, "RECOVER & CONTINUE", () => run?.ContinueWithoutReward()).Size(height: 62f);
            restartButton = UIFactory.CreateButton(panel, "START A NEW RUN", () => run?.RestartRun()).Size(height: 62f);
            overlay.SetActive(false);
        }
    }
}
