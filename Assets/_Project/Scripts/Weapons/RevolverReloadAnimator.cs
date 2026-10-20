using System.Collections.Generic;
using UnityEngine;

namespace SandHouse.Weapons
{
    /// <summary>
    /// Procedural single-action revolver reload visuals, on top of Weapon's LoadingGate pose:
    /// spent casings drop out of the cylinder when the gate opens, then each live round slides in
    /// behind the cylinder and gets pushed into the chamber at the loading gate (the Weapon turns the
    /// cylinder to the next chamber as each one goes in).
    ///
    /// Works with any model that has a "Cylinder" pivot on the barrel axis - sizes below are in metres
    /// and match the ornate revolver (drum about 54 mm across, 64 mm long).
    /// </summary>
    [RequireComponent(typeof(Weapon))]
    public class RevolverReloadAnimator : MonoBehaviour
    {
        [SerializeField] Material brassMaterial;
        [SerializeField] Material leadMaterial;

        [Header("Cylinder")]
        [Tooltip("Distance from the cylinder pivot to its rear face, along the barrel.")]
        [SerializeField] float cylinderHalfLength = 0.032f;
        [Tooltip("How far the chambers sit from the cylinder's axis.")]
        [SerializeField] float chamberRadius = 0.016f;
        [Tooltip("Where the loading gate is, as a clock angle seen from behind (90 = 3 o'clock, right side).")]
        [SerializeField] float gateClockAngle = 110f;

        [Header("Rounds")]
        [SerializeField] float roundLength = 0.034f;
        [SerializeField] float roundDiameter = 0.0095f;
        [Tooltip("Where a new round comes from, relative to the chamber (right, up, forward in gun space).")]
        [SerializeField] Vector3 roundStartOffset = new Vector3(0.045f, -0.05f, -0.07f);

        [Header("Casings")]
        [SerializeField] float casingLifetime = 4f;
        [SerializeField] float casingEjectSpeed = 1.2f;

        Weapon weapon;
        Transform cylinder;
        Transform round;
        Vector3 barrelLocal, upLocal, rightLocal; // gun directions in model space (model rotates during the reload)
        Vector3 cylinderLocal;
        readonly List<Collider> ownerColliders = new List<Collider>();

        void Awake()
        {
            weapon = GetComponent<Weapon>();
            GetComponentsInParent(true, ownerColliders);
        }

        void Start()
        {
            cylinder = weapon.SpinPart;
            Transform model = weapon.Model;
            if (cylinder == null || model == null) return;

            // Record the gun's own axes and the cylinder position in model space while at rest.
            barrelLocal = model.InverseTransformDirection(transform.forward);
            upLocal = model.InverseTransformDirection(transform.up);
            rightLocal = model.InverseTransformDirection(transform.right);
            cylinderLocal = model.InverseTransformPoint(cylinder.position);
            round = BuildRound(model);
            round.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            weapon = weapon != null ? weapon : GetComponent<Weapon>();
            weapon.ReloadOpened += OnReloadOpened;
            weapon.RoundLoaded += OnRoundLoaded;
        }

        void OnDisable()
        {
            weapon.ReloadOpened -= OnReloadOpened;
            weapon.RoundLoaded -= OnRoundLoaded;
            if (round != null) round.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (round == null) return;

            bool loading = weapon.IsReloading && weapon.ReloadPhase == ReloadPhase.Loading;
            round.gameObject.SetActive(loading);
            if (!loading) return;

            // Slide in from below/behind, line up behind the chamber, then push home.
            float t = weapon.ReloadProgress;
            Vector3 chamber = ChamberLocal();
            Vector3 start = chamber + rightLocal * roundStartOffset.x + upLocal * roundStartOffset.y + barrelLocal * roundStartOffset.z;
            Vector3 lineUp = chamber - barrelLocal * (roundLength * 0.9f);
            Vector3 home = chamber - barrelLocal * (roundLength * 0.15f);

            Vector3 position;
            if (t < 0.55f) position = Vector3.Lerp(start, lineUp, EaseOut(t / 0.55f));
            else if (t < 0.85f) position = Vector3.Lerp(lineUp, home, EaseInOut((t - 0.55f) / 0.3f));
            else position = home;

            round.localPosition = position;
            round.localRotation = Quaternion.LookRotation(barrelLocal, upLocal);
        }

        /// <summary>Chamber at the loading gate, at the rear face of the cylinder, in model space.</summary>
        Vector3 ChamberLocal()
        {
            float angle = gateClockAngle * Mathf.Deg2Rad;
            Vector3 radial = rightLocal * Mathf.Sin(angle) + upLocal * Mathf.Cos(angle);
            return cylinderLocal - barrelLocal * cylinderHalfLength + radial * chamberRadius;
        }

        void OnRoundLoaded()
        {
            if (round != null) round.gameObject.SetActive(false); // it's in the chamber now
        }

        void OnReloadOpened(int spent)
        {
            if (cylinder == null) return;
            Transform model = weapon.Model;
            Vector3 rear = model.TransformPoint(cylinderLocal - barrelLocal * cylinderHalfLength);
            Vector3 back = -model.TransformDirection(barrelLocal);

            for (int i = 0; i < spent; i++)
            {
                // Spread the casings around the chambers they came out of.
                float angle = (gateClockAngle + i * 60f) * Mathf.Deg2Rad;
                Vector3 radial = model.TransformDirection(rightLocal * Mathf.Sin(angle) + upLocal * Mathf.Cos(angle));
                SpawnCasing(rear + radial * chamberRadius, back * casingEjectSpeed + Random.insideUnitSphere * 0.4f + Vector3.down * 0.5f);
            }
        }

        void SpawnCasing(Vector3 position, Vector3 velocity)
        {
            var casing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            casing.name = "Spent Casing";
            casing.transform.position = position;
            casing.transform.rotation = Random.rotation;
            casing.transform.localScale = new Vector3(roundDiameter, roundLength * 0.4f, roundDiameter); // case only, no bullet
            var renderer = casing.GetComponent<Renderer>();
            if (brassMaterial != null) renderer.sharedMaterial = brassMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var collider = casing.GetComponent<Collider>();
            foreach (var owner in ownerColliders) Physics.IgnoreCollision(collider, owner);

            var body = casing.AddComponent<Rigidbody>();
            body.mass = 0.01f;
            body.linearVelocity = velocity;
            body.angularVelocity = Random.insideUnitSphere * 20f;
            Destroy(casing, casingLifetime);
        }

        /// <summary>A live round: brass case with a lead bullet, pointing along +Z.</summary>
        Transform BuildRound(Transform parent)
        {
            var root = new GameObject("Reload Round").transform;
            root.SetParent(parent, false);

            var brass = Part(root, PrimitiveType.Cylinder, brassMaterial);
            brass.localScale = new Vector3(roundDiameter, roundLength * 0.35f, roundDiameter);
            brass.localRotation = Quaternion.Euler(90f, 0f, 0f);
            brass.localPosition = new Vector3(0f, 0f, -roundLength * 0.15f);

            var bullet = Part(root, PrimitiveType.Sphere, leadMaterial);
            bullet.localScale = new Vector3(roundDiameter * 0.92f, roundDiameter * 0.92f, roundDiameter * 1.6f);
            bullet.localPosition = new Vector3(0f, 0f, roundLength * 0.25f);

            var rim = Part(root, PrimitiveType.Cylinder, brassMaterial);
            rim.localScale = new Vector3(roundDiameter * 1.2f, roundDiameter * 0.08f, roundDiameter * 1.2f);
            rim.localRotation = Quaternion.Euler(90f, 0f, 0f);
            rim.localPosition = new Vector3(0f, 0f, -roundLength * 0.5f);
            return root;
        }

        static Transform Part(Transform parent, PrimitiveType type, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            Destroy(part.GetComponent<Collider>());
            part.layer = parent.gameObject.layer == 0 ? 2 : parent.gameObject.layer;
            part.transform.SetParent(parent, false);
            var renderer = part.GetComponent<Renderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return part.transform;
        }

        static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
        static float EaseInOut(float t) => Mathf.SmoothStep(0f, 1f, t);
    }
}
