using UnityEngine;

namespace SandHouse.Weapons
{
    /// <summary>Throwaway placeholder effects (tracers, bullet holes) until we have real VFX.</summary>
    public static class ShotEffects
    {
        const float TracerLifetime = 0.05f;
        const float ImpactLifetime = 6f;
        const float ImpactSize = 0.07f;

        public static void SpawnTracer(Vector3 from, Vector3 to, Material material)
        {
            var go = new GameObject("Tracer");
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startWidth = 0.03f;
            line.endWidth = 0.01f;
            line.sharedMaterial = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.Destroy(go, TracerLifetime);
        }

        public static void SpawnImpact(RaycastHit hit, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Impact";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = hit.point + hit.normal * 0.005f;
            go.transform.localScale = Vector3.one * ImpactSize;
            // Follow moving targets. Parent to the target root (unscaled) so the marker doesn't get squashed.
            var target = hit.collider.GetComponentInParent<Combat.Health>();
            if (target != null) go.transform.SetParent(target.transform, true);

            var renderer = go.GetComponent<Renderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Object.Destroy(go, ImpactLifetime);
        }
    }
}
