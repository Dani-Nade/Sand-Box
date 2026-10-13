using UnityEngine;

namespace SandHouse.Targets
{
    /// <summary>Moves back and forth along a local offset. Used for tracking practice.</summary>
    public class MovingTarget : MonoBehaviour
    {
        [SerializeField] Vector3 travel = new Vector3(6f, 0f, 0f);
        [Tooltip("Metres per second.")]
        [SerializeField] float speed = 3f;

        Vector3 start;

        void Start() => start = transform.position;

        void Update()
        {
            float distance = travel.magnitude;
            if (distance < 0.001f) return;

            float t = Mathf.PingPong(Time.time * speed / distance, 1f);
            t = Mathf.SmoothStep(0f, 1f, t);
            transform.position = start + transform.rotation * travel * (t - 0.5f);
        }

        public void Configure(Vector3 localTravel, float metresPerSecond)
        {
            travel = localTravel;
            speed = metresPerSecond;
        }
    }
}
