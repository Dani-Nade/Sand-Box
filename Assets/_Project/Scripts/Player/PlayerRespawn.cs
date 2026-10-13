using SandHouse.Combat;
using UnityEngine;

namespace SandHouse.Player
{
    /// <summary>Sends the player back to where they started if they fall out of the level or die.</summary>
    [RequireComponent(typeof(PlayerMotor))]
    public class PlayerRespawn : MonoBehaviour
    {
        [SerializeField] float killHeight = -20f;

        PlayerMotor motor;
        Health health;
        Vector3 spawnPosition;
        Quaternion spawnRotation;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            health = GetComponent<Health>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        void OnEnable()
        {
            if (health != null) health.Died += Respawn;
        }

        void OnDisable()
        {
            if (health != null) health.Died -= Respawn;
        }

        void Update()
        {
            if (transform.position.y < killHeight)
                Respawn();
        }

        public void Respawn()
        {
            motor.Teleport(spawnPosition, spawnRotation);
            if (health != null) health.ResetHealth();
        }
    }
}
