using UnityEngine;

namespace SandHouse.UI
{
    /// <summary>Floating world-space damage number that rises, faces the camera and fades out.</summary>
    public class DamageNumber : MonoBehaviour
    {
        const float Lifetime = 0.8f;
        const float RiseSpeed = 1.2f;

        static Font font;

        TextMesh text;
        Color color;
        float age;

        public static void Spawn(Vector3 position, float amount, bool critical, bool killed)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var go = new GameObject("DamageNumber");
            go.transform.position = position + Random.insideUnitSphere * 0.15f;

            var number = go.AddComponent<DamageNumber>();
            number.text = go.AddComponent<TextMesh>();
            number.text.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            number.text.text = killed ? $"{amount:0}  KILL" : $"{amount:0}";
            number.text.fontSize = 64;
            number.text.characterSize = critical ? 0.045f : 0.032f;
            number.text.anchor = TextAnchor.MiddleCenter;
            number.color = critical ? new Color(1f, 0.85f, 0.2f) : Color.white;
            if (killed) number.color = new Color(1f, 0.35f, 0.25f);
            number.text.color = number.color;
        }

        void LateUpdate()
        {
            age += Time.deltaTime;
            if (age >= Lifetime)
            {
                Destroy(gameObject);
                return;
            }

            transform.position += Vector3.up * (RiseSpeed * Time.deltaTime);
            var cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;

            Color c = color;
            c.a = 1f - Mathf.Pow(age / Lifetime, 2f);
            text.color = c;
        }
    }
}
