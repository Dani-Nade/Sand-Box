using UnityEngine;

namespace SandHouse.Weapons
{
    /// <summary>Placeholder explosion: a fireball that pops out and shrinks away, plus a flash of light.</summary>
    public class ExplosionEffect : MonoBehaviour
    {
        const float Duration = 0.4f;
        const float VisualScale = 0.6f; // fireball looks smaller than the actual damage radius

        float radius;
        float age;
        Light flash;

        public static void Spawn(Vector3 position, float radius, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Explosion";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = position;
            var renderer = go.GetComponent<Renderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var effect = go.AddComponent<ExplosionEffect>();
            effect.radius = radius;
            effect.flash = go.AddComponent<Light>();
            effect.flash.type = LightType.Point;
            effect.flash.color = new Color(1f, 0.6f, 0.25f);
            effect.flash.range = radius * 3f;
            effect.flash.shadows = LightShadows.None;
            effect.Update();
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / Duration);
            float size = t < 0.25f ? Mathf.Lerp(0.3f, 1f, t / 0.25f) : Mathf.Lerp(1f, 0f, (t - 0.25f) / 0.75f);
            transform.localScale = Vector3.one * (radius * 2f * VisualScale * size);
            flash.intensity = 10f * (1f - t);
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
