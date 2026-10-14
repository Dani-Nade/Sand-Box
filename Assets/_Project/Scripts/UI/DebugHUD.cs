using SandHouse.Player;
using SandHouse.Weapons;
using UnityEngine;
using UnityEngine.UI;

namespace SandHouse.UI
{
    /// <summary>
    /// Placeholder test HUD built in code: crosshair, hitmarker, ammo, dash and Dead Eye meters,
    /// movement-tech callouts and a movement readout for tuning. Health lives in the StatsPanel.
    /// Replace with a designed HUD later.
    /// </summary>
    public class DebugHUD : MonoBehaviour
    {
        [SerializeField] PlayerMotor motor;
        [SerializeField] WeaponInventory weapons;
        [SerializeField] bool showMovementStats = true;

        static readonly Color HitColor = Color.white;
        static readonly Color CritColor = new Color(1f, 0.85f, 0.2f);
        static readonly Color KillColor = new Color(1f, 0.3f, 0.25f);
        static readonly Color GoodColor = new Color(1f, 0.85f, 0.2f);
        static readonly Color BadColor = new Color(1f, 0.4f, 0.35f);
        static readonly Color DeadEyeTint = new Color(0.55f, 0.35f, 0.12f, 0.28f);
        const float HitmarkerDuration = 0.15f;
        const float CalloutDuration = 0.9f;

        Text ammoText, weaponNameText, weaponListText, statsText, calloutText, deadEyeText, dashText;
        Image deadEyeOverlay;
        Image[] hitmarker;
        float hitmarkerTimer, calloutTimer, fpsSmoothed;

        void Awake() => BuildUI();

        void OnEnable()
        {
            if (weapons != null) weapons.HitTarget += OnHit;
            if (motor == null) return;
            motor.MantleSlideAttempted += OnMantleSlideAttempted;
            motor.Rolled += OnRolled;
            motor.HardLanded += OnHardLanded;
            motor.Slammed += OnSlammed;
            motor.SlamJumped += OnSlamJumped;
            motor.RecoilHopped += OnRecoilHopped;
            motor.BunnyHopped += OnBunnyHopped;
        }

        void OnDisable()
        {
            if (weapons != null) weapons.HitTarget -= OnHit;
            if (motor == null) return;
            motor.MantleSlideAttempted -= OnMantleSlideAttempted;
            motor.Rolled -= OnRolled;
            motor.HardLanded -= OnHardLanded;
            motor.Slammed -= OnSlammed;
            motor.SlamJumped -= OnSlamJumped;
            motor.RecoilHopped -= OnRecoilHopped;
            motor.BunnyHopped -= OnBunnyHopped;
        }

        void OnMantleSlideAttempted(bool success) => Callout(success ? "MANTLE SLIDE" : "TOO EARLY", success ? GoodColor : BadColor);
        void OnRolled() => Callout("ROLL", GoodColor);
        void OnHardLanded(float impact) => Callout("HARD LANDING", BadColor);
        void OnSlammed(float height) => Callout($"SLAM  {height:0.0} m", GoodColor);
        void OnSlamJumped(float extra) => Callout($"SLAM JUMP  +{extra:0.0} m", GoodColor);
        void OnRecoilHopped() => Callout($"RECOIL HOP  ({motor.RecoilHopsLeft} left)", Color.white);

        void OnBunnyHopped(int chain)
        {
            if (chain >= 2) Callout($"BHOP x{chain}   {motor.HorizontalSpeed:0.0} m/s", chain >= 5 ? GoodColor : Color.white);
        }

        void Callout(string text, Color color)
        {
            calloutText.text = text;
            calloutText.color = color;
            calloutTimer = CalloutDuration;
        }

        void Update()
        {
            // Unscaled so the HUD behaves normally during Dead Eye slow motion.
            float dt = Time.unscaledDeltaTime;

            UpdateWeaponText();

            if (motor != null)
            {
                deadEyeOverlay.enabled = motor.IsDeadEye;
                float charge = motor.DeadEyeCharge01;
                deadEyeText.text = motor.IsDeadEye ? "DEAD EYE" : charge >= 1f ? "DEAD EYE READY  [F]" : $"DEAD EYE  {charge * 100f:0}%";
                deadEyeText.color = motor.IsDeadEye || charge >= 1f ? GoodColor : UIFactory.MutedTextColor;
                dashText.text = DashMeter();
            }

            if (showMovementStats && motor != null)
            {
                fpsSmoothed = Mathf.Lerp(fpsSmoothed, 1f / Mathf.Max(dt, 0.0001f), 0.05f);
                statsText.text =
                    $"Speed {motor.HorizontalSpeed:0.0} m/s\nVert {motor.Velocity.y:0.0} m/s\n{StateName()}\n" +
                    $"Wall bounces {motor.MaxWallBounces - motor.WallBouncesUsed}/{motor.MaxWallBounces}   Recoil hops {motor.RecoilHopsLeft}   Bhop x{motor.BunnyHopChain}\n" +
                    $"{fpsSmoothed:0} fps";
            }

            if (calloutTimer > 0f)
            {
                calloutTimer -= dt;
                Color c = calloutText.color;
                c.a = Mathf.Clamp01(calloutTimer / (CalloutDuration * 0.5f));
                calloutText.color = c;
            }

            if (hitmarkerTimer > 0f)
            {
                hitmarkerTimer -= dt;
                SetHitmarkerAlpha(Mathf.Clamp01(hitmarkerTimer / HitmarkerDuration));
            }
        }

        void UpdateWeaponText()
        {
            Weapon current = weapons != null ? weapons.Current : null;
            if (current == null) return;

            weaponNameText.text = current.DisplayName.ToUpperInvariant();
            ammoText.text = current.IsReloading ? $"RELOADING  {current.Ammo} / {current.MagazineSize}" : $"{current.Ammo} / {current.MagazineSize}";

            // "1 Revolver   2 Rifle ..." with the equipped weapon highlighted.
            var list = new System.Text.StringBuilder();
            for (int i = 0; i < weapons.Weapons.Count; i++)
            {
                string entry = $"{i + 1} {weapons.Weapons[i].DisplayName}";
                list.Append(i == weapons.CurrentIndex ? $"<color=#FFCC59>{entry}</color>" : entry);
                if (i < weapons.Weapons.Count - 1) list.Append("    ");
            }
            weaponListText.text = list.ToString();
        }

        /// <summary>"DASH ●●○" - filled dots are ready charges, the partly refilled one shows a percentage.</summary>
        string DashMeter()
        {
            int full = Mathf.FloorToInt(motor.DashCharge);
            var meter = new System.Text.StringBuilder("DASH  ");
            for (int i = 0; i < motor.MaxDashCharges; i++)
                meter.Append(i < full ? "● " : "○ ");
            if (full < motor.MaxDashCharges)
                meter.Append($" {(motor.DashCharge - full) * 100f:0}%");
            return meter.ToString();
        }

        string StateName()
        {
            if (motor.IsDashing) return "DASH";
            if (motor.IsSwinging) return "SWING";
            if (motor.IsMantling) return "MANTLE";
            if (motor.IsSlamming) return "SLAM";
            if (motor.IsDiving) return "DIVE";
            if (motor.IsRolling) return "ROLL";
            if (motor.IsMantleSliding) return "MANTLE SLIDE";
            if (motor.IsSliding) return "SLIDE";
            if (!motor.IsGrounded) return "AIR";
            if (motor.IsStumbling) return "STUMBLE";
            if (motor.IsCrouching) return "CROUCH";
            return motor.HorizontalSpeed > 0.5f ? "RUN" : "GROUND";
        }

        void OnHit(HitResult hit)
        {
            Color c = hit.Killed ? KillColor : hit.IsCritical ? CritColor : HitColor;
            foreach (var img in hitmarker) img.color = c;
            hitmarkerTimer = HitmarkerDuration;
        }

        void SetHitmarkerAlpha(float a)
        {
            foreach (var img in hitmarker)
            {
                Color c = img.color;
                c.a = a;
                img.color = c;
            }
        }

        // ---------------------------------------------------------------- UI construction

        void BuildUI()
        {
            var root = UIFactory.CreateCanvas("HUD Canvas", transform, sortingOrder: 0, interactive: false).transform;
            var center = new Vector2(0.5f, 0.5f);

            // Full-screen sepia tint for Dead Eye (built first so it sits behind everything else).
            deadEyeOverlay = UIFactory.CreateImage(root, "Dead Eye Tint", DeadEyeTint);
            deadEyeOverlay.rectTransform.Stretch();
            deadEyeOverlay.enabled = false;

            BuildCrosshair(root);
            hitmarker = BuildHitmarker(root);
            SetHitmarkerAlpha(0f);

            ammoText = UIFactory.CreateText(root, "Ammo", "", 44, TextAnchor.LowerRight);
            ammoText.rectTransform.Place(new Vector2(1f, 0f), new Vector2(-60f, 50f), new Vector2(600f, 60f));

            weaponNameText = UIFactory.CreateText(root, "Weapon Name", "", 24, TextAnchor.LowerRight, UIFactory.AccentColor);
            weaponNameText.rectTransform.Place(new Vector2(1f, 0f), new Vector2(-60f, 108f), new Vector2(600f, 32f));

            weaponListText = UIFactory.CreateText(root, "Weapon List", "", 18, TextAnchor.LowerRight, UIFactory.MutedTextColor);
            weaponListText.rectTransform.Place(new Vector2(1f, 0f), new Vector2(-60f, 18f), new Vector2(900f, 26f));

            deadEyeText = UIFactory.CreateText(root, "Dead Eye", "", 24, TextAnchor.LowerCenter);
            deadEyeText.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(600f, 32f));

            dashText = UIFactory.CreateText(root, "Dash", "", 28, TextAnchor.LowerCenter);
            dashText.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0f, 75f), new Vector2(600f, 36f));

            statsText = UIFactory.CreateText(root, "Stats", "", 22, TextAnchor.UpperLeft);
            statsText.rectTransform.Place(new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(800f, 200f));
            statsText.gameObject.SetActive(showMovementStats);

            calloutText = UIFactory.CreateText(root, "Callout", "", 34, TextAnchor.MiddleCenter, Color.clear);
            calloutText.rectTransform.Place(center, new Vector2(0f, -90f), new Vector2(800f, 50f));

            var help = UIFactory.CreateText(root, "Controls",
                "WASD move   Shift dash (3 charges, works in the air)   Space jump   Ctrl crouch\n" +
                "LMB shoot   R reload   1-4 / scroll = switch weapon   Esc pause   Hold Tab = upgrade stats\n" +
                "Dash + Space = dash jump      Shotgun at the ground in the air = shotgun jump\n" +
                "Run/dash + hold Ctrl = slide      Jump into ledge + W = mantle\n" +
                "Ctrl at the END of a mantle = mantle slide\n" +
                "Space near wall in air = wall bounce      A/D + turn in air = air strafe\n" +
                "Hold Q = lasso (Space = lasso jump)      F = Dead Eye dive\n" +
                "Ctrl high in the air = slam (Space on landing = slam jump)\n" +
                "Ctrl just before a hard landing = roll      Shoot in the air = recoil hop\n" +
                "Jump again right as you land = bunny hop (keeps speed, chains add speed)",
                19, TextAnchor.UpperRight, UIFactory.MutedTextColor);
            help.rectTransform.Place(new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(1000f, 260f));
        }

        static void BuildCrosshair(Transform parent)
        {
            const float gap = 6f, length = 9f, thickness = 2f;
            CrosshairBar(parent, "Dot", Vector2.zero, new Vector2(thickness, thickness), 0f);
            CrosshairBar(parent, "Up", new Vector2(0, gap + length / 2), new Vector2(thickness, length), 0f);
            CrosshairBar(parent, "Down", new Vector2(0, -gap - length / 2), new Vector2(thickness, length), 0f);
            CrosshairBar(parent, "Left", new Vector2(-gap - length / 2, 0), new Vector2(length, thickness), 0f);
            CrosshairBar(parent, "Right", new Vector2(gap + length / 2, 0), new Vector2(length, thickness), 0f);
        }

        static Image[] BuildHitmarker(Transform parent)
        {
            const float offset = 14f, length = 10f, thickness = 2.5f;
            var images = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                float angle = 45f + 90f * i;
                Vector2 dir = Quaternion.Euler(0, 0, angle) * Vector2.right;
                images[i] = CrosshairBar(parent, "Hitmarker", dir * offset, new Vector2(length, thickness), angle);
            }
            return images;
        }

        static Image CrosshairBar(Transform parent, string name, Vector2 position, Vector2 size, float rotation)
        {
            var image = UIFactory.CreateImage(parent, name, HitColor);
            image.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, size);
            image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.localRotation = Quaternion.Euler(0, 0, rotation);
            return image;
        }
    }
}
