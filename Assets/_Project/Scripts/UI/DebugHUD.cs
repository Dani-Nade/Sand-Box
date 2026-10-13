using SandHouse.Combat;
using SandHouse.Player;
using SandHouse.Weapons;
using UnityEngine;
using UnityEngine.UI;

namespace SandHouse.UI
{
    /// <summary>
    /// Placeholder test HUD built entirely in code: crosshair, hitmarker, ammo, health, Dead Eye meter,
    /// movement-tech callouts and a movement readout for tuning. Replace with a designed HUD later.
    /// </summary>
    public class DebugHUD : MonoBehaviour
    {
        [SerializeField] PlayerMotor motor;
        [SerializeField] Revolver revolver;
        [SerializeField] Health playerHealth;
        [SerializeField] bool showMovementStats = true;

        static readonly Color HitColor = Color.white;
        static readonly Color CritColor = new Color(1f, 0.85f, 0.2f);
        static readonly Color KillColor = new Color(1f, 0.3f, 0.25f);
        static readonly Color GoodColor = new Color(1f, 0.85f, 0.2f);
        static readonly Color BadColor = new Color(1f, 0.4f, 0.35f);
        static readonly Color DeadEyeTint = new Color(0.55f, 0.35f, 0.12f, 0.28f);
        const float HitmarkerDuration = 0.15f;
        const float CalloutDuration = 0.9f;

        Font font;
        Text ammoText, healthText, statsText, calloutText, deadEyeText, dashText;
        Image deadEyeOverlay;
        Image[] hitmarker;
        float hitmarkerTimer, calloutTimer, fpsSmoothed;

        void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildUI();
        }

        void OnEnable()
        {
            if (revolver != null) revolver.HitTarget += OnHit;
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
            if (revolver != null) revolver.HitTarget -= OnHit;
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

            if (revolver != null)
                ammoText.text = revolver.IsReloading ? "RELOADING" : $"{revolver.Ammo} / {revolver.MagazineSize}";

            if (playerHealth != null)
                healthText.text = $"HP {playerHealth.Current:0}";

            if (motor != null)
            {
                deadEyeOverlay.enabled = motor.IsDeadEye;
                float charge = motor.DeadEyeCharge01;
                deadEyeText.text = motor.IsDeadEye ? "DEAD EYE" : charge >= 1f ? "DEAD EYE READY  [F]" : $"DEAD EYE  {charge * 100f:0}%";
                deadEyeText.color = motor.IsDeadEye || charge >= 1f ? GoodColor : new Color(1f, 1f, 1f, 0.5f);
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
            var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.transform;

            // Full-screen sepia tint for Dead Eye (built first so it sits behind everything else).
            deadEyeOverlay = CreateRect(root, "Dead Eye Tint", Vector2.zero, Vector2.zero, 0f, DeadEyeTint);
            var overlayRect = (RectTransform)deadEyeOverlay.transform;
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.sizeDelta = Vector2.zero;
            deadEyeOverlay.enabled = false;

            BuildCrosshair(root);
            hitmarker = BuildHitmarker(root);
            SetHitmarkerAlpha(0f);

            ammoText = CreateText(root, "Ammo", new Vector2(1, 0), new Vector2(-60, 50), 44, TextAnchor.LowerRight);
            healthText = CreateText(root, "Health", new Vector2(0, 0), new Vector2(60, 50), 36, TextAnchor.LowerLeft);
            deadEyeText = CreateText(root, "Dead Eye", new Vector2(0.5f, 0), new Vector2(0, 40), 24, TextAnchor.LowerCenter);
            dashText = CreateText(root, "Dash", new Vector2(0.5f, 0), new Vector2(0, 75), 28, TextAnchor.LowerCenter);
            statsText = CreateText(root, "Stats", new Vector2(0, 1), new Vector2(30, -30), 22, TextAnchor.UpperLeft);
            statsText.gameObject.SetActive(showMovementStats);

            calloutText = CreateText(root, "Callout", new Vector2(0.5f, 0.5f), new Vector2(0, -90), 34, TextAnchor.MiddleCenter);
            calloutText.color = Color.clear;

            var help = CreateText(root, "Controls", new Vector2(1, 1), new Vector2(-30, -30), 19, TextAnchor.UpperRight);
            help.text =
                "WASD move   Shift dash (3 charges, works in the air)   Space jump   Ctrl crouch\n" +
                "LMB shoot   R reload   Esc cursor      Dash + Space = dash jump\n" +
                "Run/dash + hold Ctrl = slide      Jump into ledge + W = mantle\n" +
                "Ctrl at the END of a mantle = mantle slide\n" +
                "Space near wall in air = wall bounce      A/D + turn in air = air strafe\n" +
                "Hold Q = lasso (Space = lasso jump)      F = Dead Eye dive\n" +
                "Ctrl high in the air = slam (Space on landing = slam jump)\n" +
                "Ctrl just before a hard landing = roll      Shoot in the air = recoil hop\n" +
                "Jump again right as you land = bunny hop (keeps speed, chains add speed)";
            help.color = new Color(1f, 1f, 1f, 0.6f);
            ((RectTransform)help.transform).sizeDelta = new Vector2(1000, 260);
        }

        void BuildCrosshair(Transform parent)
        {
            const float gap = 6f, length = 9f, thickness = 2f;
            CreateRect(parent, "Dot", Vector2.zero, new Vector2(thickness, thickness), 0f, Color.white);
            CreateRect(parent, "Up", new Vector2(0, gap + length / 2), new Vector2(thickness, length), 0f, Color.white);
            CreateRect(parent, "Down", new Vector2(0, -gap - length / 2), new Vector2(thickness, length), 0f, Color.white);
            CreateRect(parent, "Left", new Vector2(-gap - length / 2, 0), new Vector2(length, thickness), 0f, Color.white);
            CreateRect(parent, "Right", new Vector2(gap + length / 2, 0), new Vector2(length, thickness), 0f, Color.white);
        }

        Image[] BuildHitmarker(Transform parent)
        {
            const float offset = 14f, length = 10f, thickness = 2.5f;
            var images = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                float angle = 45f + 90f * i;
                Vector2 dir = Quaternion.Euler(0, 0, angle) * Vector2.right;
                images[i] = CreateRect(parent, "Hitmarker", dir * offset, new Vector2(length, thickness), angle, HitColor);
            }
            return images;
        }

        static Image CreateRect(Transform parent, string name, Vector2 position, Vector2 size, float rotation, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            rt.localRotation = Quaternion.Euler(0, 0, rotation);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Text CreateText(Transform parent, string name, Vector2 anchor, Vector2 position, int size, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Shadow));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(800, 200);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            go.GetComponent<Shadow>().effectDistance = new Vector2(2, -2);
            return text;
        }
    }
}
