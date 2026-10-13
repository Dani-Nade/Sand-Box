using System.Collections;
using SandHouse.Combat;
using SandHouse.UI;
using UnityEngine;

namespace SandHouse.Targets
{
    /// <summary>
    /// Shooting-range dummy: flashes when hit, shows damage numbers,
    /// disappears when killed and respawns after a delay.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class TargetDummy : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] float respawnDelay = 2f;
        [SerializeField] float flashDuration = 0.08f;
        [SerializeField] Color flashColor = Color.white;
        [SerializeField] bool showDamageNumbers = true;

        Health health;
        Renderer[] renderers;
        Collider[] colliders;
        Color[] baseColors;
        MaterialPropertyBlock block;
        Coroutine flashRoutine;

        void Awake()
        {
            health = GetComponent<Health>();
            renderers = GetComponentsInChildren<Renderer>();
            colliders = GetComponentsInChildren<Collider>();
            block = new MaterialPropertyBlock();

            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var mat = renderers[i].sharedMaterial;
                baseColors[i] = mat != null && mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : Color.white;
            }
        }

        void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        void OnDamaged(DamageInfo info)
        {
            if (showDamageNumbers)
                DamageNumber.Spawn(info.Point, info.Amount, info.IsCritical, health.IsDead);

            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(Flash());
        }

        void OnDied() => StartCoroutine(RespawnAfterDelay());

        IEnumerator Flash()
        {
            SetColor(_ => flashColor);
            yield return new WaitForSeconds(flashDuration);
            SetColor(i => baseColors[i]);
            flashRoutine = null;
        }

        IEnumerator RespawnAfterDelay()
        {
            SetVisible(false);
            yield return new WaitForSeconds(respawnDelay);
            health.ResetHealth();
            SetColor(i => baseColors[i]);
            SetVisible(true);
        }

        void SetVisible(bool visible)
        {
            foreach (var r in renderers) r.enabled = visible;
            foreach (var c in colliders) c.enabled = visible;
        }

        void SetColor(System.Func<int, Color> colorFor)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].GetPropertyBlock(block);
                block.SetColor(BaseColorId, colorFor(i));
                renderers[i].SetPropertyBlock(block);
            }
        }
    }
}
