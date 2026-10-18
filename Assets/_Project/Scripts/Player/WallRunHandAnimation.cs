using UnityEngine;
using UnityEngine.Rendering;

namespace SandHouse.Player
{
    /// <summary>
    /// Placeholder first-person glove and sleeve. The wall-side hand reaches out,
    /// follows a running rhythm, then withdraws when the player leaves the wall.
    /// Built on the camera so weapon recoil and reload animation stay independent.
    /// </summary>
    public sealed class WallRunHandAnimation : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] float blendSpeed = 7f;
        [SerializeField] Vector3 reachPosition = new Vector3(0.29f, -0.12f, 0.48f);
        [SerializeField] Vector3 reachRotation = new Vector3(12f, 45f, -25f);
        [SerializeField, Min(0.1f)] float metresPerCycle = 3.6f;
        [SerializeField] float bobAmount = 0.012f;

        sealed class Hand
        {
            public Transform root, wrist, forearm;
            public readonly Transform[] fingers = new Transform[4];
            public float blend;
            public int side;
        }

        PlayerMotor motor;
        Transform view, visualRoot;
        Material glove, cuff, sleeve;
        Hand left, right;
        float phase;
        float climbBlend;

        public void Initialize(PlayerMotor playerMotor, Transform cameraTransform)
        {
            motor = playerMotor;
            view = cameraTransform;
            if (visualRoot != null) return;

            visualRoot = new GameObject("Wall Run Hands").transform;
            visualRoot.SetParent(view, false);
            glove = MakeMaterial("Wall Run Leather", new Color(0.38f, 0.19f, 0.085f));
            cuff = MakeMaterial("Wall Run Cuff", new Color(0.12f, 0.085f, 0.055f));
            sleeve = MakeMaterial("Wall Run Sleeve", new Color(0.18f, 0.27f, 0.32f));
            left = BuildHand(-1);
            right = BuildHand(1);
        }

        void LateUpdate()
        {
            if (visualRoot == null || motor == null || view == null) return;
            bool running = motor.IsWallRunning;
            bool climbing = motor.IsWallClimbing;
            climbBlend = Mathf.MoveTowards(climbBlend, climbing ? 1f : 0f, blendSpeed * Time.deltaTime);
            float wallSide = running ? Vector3.Dot(view.right, -motor.WallRunNormal) : 0f;
            float speed = climbing ? Mathf.Max(0.5f, motor.Velocity.y) : motor.HorizontalSpeed;
            if (running || climbing) phase += speed * Time.deltaTime * Mathf.PI * 2f / metresPerCycle;
            AnimateHand(left, climbing || (running && wallSide < 0f), Time.deltaTime);
            AnimateHand(right, climbing || (running && wallSide >= 0f), Time.deltaTime);
        }

        void AnimateHand(Hand hand, bool reaching, float dt)
        {
            hand.blend = Mathf.MoveTowards(hand.blend, reaching ? 1f : 0f, blendSpeed * dt);
            hand.root.gameObject.SetActive(hand.blend > 0f);
            if (hand.blend <= 0f) return;
            float ease = Mathf.SmoothStep(0f, 1f, hand.blend);
            float rhythm = Mathf.Sin(phase);
            Vector3 tucked = new Vector3(hand.side * 0.2f, -0.5f, 0.22f);
            Vector3 reached = new Vector3(hand.side * reachPosition.x, reachPosition.y, reachPosition.z);
            reached += new Vector3(hand.side * Mathf.Cos(phase) * bobAmount * 0.4f,
                rhythm * bobAmount, Mathf.Cos(phase) * bobAmount * 0.6f);
            float stroke = Mathf.Sin(phase + (hand.side < 0 ? 0f : Mathf.PI));
            Vector3 climbReach = new Vector3(hand.side * 0.2f, 0.015f + stroke * 0.055f, 0.43f - stroke * 0.015f);
            reached = Vector3.Lerp(reached, climbReach, climbBlend);
            hand.wrist.localPosition = Vector3.Lerp(tucked, reached, ease);
            Quaternion rotation = Quaternion.Euler(reachRotation.x + rhythm * 2f,
                hand.side * reachRotation.y, hand.side * reachRotation.z);
            rotation = Quaternion.Slerp(rotation, Quaternion.Euler(20f + stroke * 6f, hand.side * 12f, hand.side * -8f), climbBlend);
            hand.wrist.localRotation = Quaternion.Slerp(Quaternion.Euler(45f, 0f, hand.side * -50f), rotation, ease);

            // Connect the sleeve to the animated wrist, with no physics or aim changes.
            Vector3 elbow = new Vector3(hand.side * 0.23f, -0.43f, 0.18f);
            Vector3 wrist = hand.wrist.localPosition;
            Vector3 arm = wrist - elbow;
            hand.forearm.localPosition = (elbow + wrist) * 0.5f;
            hand.forearm.localRotation = Quaternion.FromToRotation(Vector3.up, arm);
            hand.forearm.localScale = new Vector3(0.072f, arm.magnitude * 0.5f, 0.072f);
            for (int i = 0; i < hand.fingers.Length; i++)
                hand.fingers[i].localRotation = Quaternion.Euler(-8f + Mathf.Sin(phase + i * 0.35f) * 3f,
                    0f, (i - 1.5f) * -4f);
        }

        Hand BuildHand(int side)
        {
            var hand = new Hand { side = side };
            hand.root = new GameObject(side < 0 ? "Left Wall Hand" : "Right Wall Hand").transform;
            hand.root.SetParent(visualRoot, false);
            hand.wrist = new GameObject("Wrist").transform;
            hand.wrist.SetParent(hand.root, false);
            hand.forearm = Part(hand.root, PrimitiveType.Cylinder, "Sleeve", Vector3.zero, Vector3.one, sleeve);
            Part(hand.wrist, PrimitiveType.Cube, "Cuff", Vector3.zero, new Vector3(0.072f, 0.035f, 0.055f), cuff);
            Part(hand.wrist, PrimitiveType.Sphere, "Palm", new Vector3(0f, 0.065f, 0f), new Vector3(0.085f, 0.12f, 0.045f), glove);
            for (int i = 0; i < hand.fingers.Length; i++)
            {
                var finger = new GameObject("Finger " + (i + 1)).transform;
                finger.SetParent(hand.wrist, false);
                finger.localPosition = new Vector3((i - 1.5f) * 0.021f, 0.095f, 0f);
                hand.fingers[i] = finger;
                float length = i == 3 ? 0.058f : i == 1 ? 0.085f : 0.075f;
                Part(finger, PrimitiveType.Capsule, "Gloved Finger", new Vector3(0f, length * 0.5f, 0f),
                    new Vector3(0.018f, length * 0.5f, 0.019f), glove);
            }
            Transform thumb = Part(hand.wrist, PrimitiveType.Capsule, "Thumb",
                new Vector3(-side * 0.05f, 0.065f, 0.008f), new Vector3(0.025f, 0.035f, 0.025f), glove);
            thumb.localRotation = Quaternion.Euler(-15f, 0f, side * 40f);
            hand.root.gameObject.SetActive(false);
            return hand;
        }

        static Material MakeMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.18f);
            return material;
        }

        static Transform Part(Transform parent, PrimitiveType shape, string name, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.layer = 2; // Ignore Raycast: the hands must not block shots, lasso or wall checks.
            var collider = part.GetComponent<Collider>();
            collider.enabled = false;
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return part.transform;
        }

        void OnEnable()
        {
            if (visualRoot != null) visualRoot.gameObject.SetActive(true);
        }

        void OnDisable()
        {
            if (visualRoot != null) visualRoot.gameObject.SetActive(false);
            if (left != null) left.blend = 0f;
            if (right != null) right.blend = 0f;
            climbBlend = 0f;
        }

        void OnDestroy()
        {
            if (visualRoot != null) Destroy(visualRoot.gameObject);
            if (glove != null) Destroy(glove);
            if (cuff != null) Destroy(cuff);
            if (sleeve != null) Destroy(sleeve);
        }
    }
}
