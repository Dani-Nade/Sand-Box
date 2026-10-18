using System;
using System.Collections.Generic;
using SandHouse.Combat;
using SandHouse.Player;
using SandHouse.Stats;
using SandHouse.UI;
using SandHouse.Weapons;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SandHouse.Run
{
    public enum RunPhase { NotStarted, Combat, Reward, Transition, Complete, Defeated }

    /// <summary>Three-room prototype run: fight -> draft one reward -> next room. No kill currency.</summary>
    public class RoguelikeRun : MonoBehaviour
    {
        public const int LevelCount = 3;
        [SerializeField] PlayerStats player;
        [SerializeField] Material floorMaterial, wallMaterial, coverMaterial, accentMaterial, enemyMaterial, headMaterial;
        readonly HashSet<Health> remaining = new HashSet<Health>();
        GunUpgradeType[] choices = Array.Empty<GunUpgradeType>();
        System.Random random;
        [SerializeField, HideInInspector] GameObject room;
        Health health;
        PlayerMotor motor;
        float savedTimeScale = 1f, selectionReadyAt;
        bool ownsOverlay;

        public PlayerStats Player => player;
        public RunPhase Phase { get; private set; }
        public int LevelIndex { get; private set; }
        public int EnemiesRemaining => remaining.Count;
        public IReadOnlyList<GunUpgradeType> Choices => Array.AsReadOnly(choices);
        public string LevelName => RunLevelFactory.LevelName(LevelIndex);
        public event Action Changed;

        public void Configure(PlayerStats owner, Material floor, Material wall, Material cover, Material accent, Material enemy, Material head)
        {
            player = owner; floorMaterial = floor; wallMaterial = wall; coverMaterial = cover;
            accentMaterial = accent; enemyMaterial = enemy; headMaterial = head;
        }

        void Start()
        {
            if (player == null) player = FindAnyObjectByType<PlayerStats>();
            if (player == null) { Debug.LogError("RoguelikeRun needs a player."); enabled = false; return; }
            motor = player.GetComponent<PlayerMotor>();
            health = player.GetComponent<Health>();
            var arenaRespawn = player.GetComponent<PlayerRespawn>();
            if (arenaRespawn != null) arenaRespawn.enabled = false;
            health.Died += OnPlayerDied;
            var ui = GetComponent<RunRewardPanel>();
            if (ui == null) ui = gameObject.AddComponent<RunRewardPanel>();
            ui.Bind(this);
            RestartRun();
        }

        void OnEnable()
        {
            Health.AnyKilled += OnAnyKilled;
            if (health != null) health.Died += OnPlayerDied;
        }
        void OnDisable()
        {
            Health.AnyKilled -= OnAnyKilled;
            if (health != null) health.Died -= OnPlayerDied;
            CloseOverlay();
        }

        void OnDestroy()
        {
            if (room != null) Destroy(room);
        }

        void Update()
        {
            if (Phase == RunPhase.Combat && !UIState.IsPaused && player.transform.position.y < -12f)
                health.TakeDamage(new DamageInfo { Amount = health.Max, Point = player.transform.position });
        }

        public void RestartRun()
        {
            if (player == null || UIState.IsPaused) return;
            Phase = RunPhase.Transition;
            CloseOverlay();
            player.ResetRun();
            random = new System.Random();
            LevelIndex = 0;
            EnterRoom();
        }

        void EnterRoom()
        {
            Phase = RunPhase.Transition;
            choices = Array.Empty<GunUpgradeType>();
            remaining.Clear();
            if (room != null) { room.SetActive(false); Destroy(room); }
            DynamiteProjectile.ClearOwnedBy(player.transform);
            room = new GameObject($"Level {LevelIndex + 1} - {LevelName}");
            SceneManager.MoveGameObjectToScene(room, gameObject.scene);
            RunLevelFactory.Build(room.transform, LevelIndex, floorMaterial, wallMaterial, coverMaterial, accentMaterial);
            Vector3 spawn = new Vector3(0f, 0.05f, -13f);
            if (motor != null) motor.Teleport(spawn, Quaternion.identity);
            else player.transform.SetPositionAndRotation(spawn, Quaternion.identity);
            foreach (var weapon in player.GetComponentsInChildren<Weapon>(true)) weapon.Restock();
            foreach (Vector3 position in RunLevelFactory.EnemySpawns(LevelIndex))
            {
                var enemy = RunEnemy.Spawn(room.transform, position, player.transform, LevelIndex,
                    remaining.Count % 2 == 1, enemyMaterial, headMaterial);
                remaining.Add(enemy.GetComponent<Health>());
            }
            Physics.SyncTransforms();
            Phase = RunPhase.Combat;
            Changed?.Invoke();
        }

        void OnAnyKilled(Health victim, DamageInfo info)
        {
            if (Phase != RunPhase.Combat || !remaining.Remove(victim)) return;
            if (remaining.Count == 0 && !health.IsDead)
            {
                Phase = RunPhase.Reward;
                choices = UpgradeDraft.Roll(player.GunUpgrades, random);
                player.AddPoints(1); // Base stats are separate from the free gun draft.
                selectionReadyAt = Time.unscaledTime + 0.2f;
                OpenOverlay();
            }
            Changed?.Invoke();
        }

        public bool TrySelectReward(int index)
        {
            if (Phase != RunPhase.Reward || Time.unscaledTime < selectionReadyAt || index < 0 || index >= choices.Length) return false;
            var type = choices[index];
            if (!player.GunUpgrades.CanGrant(type))
            {
                choices = UpgradeDraft.Roll(player.GunUpgrades, random);
                Changed?.Invoke(); return false;
            }
            // Change phase before firing upgrade events: double-clicks cannot grant two rewards.
            Phase = RunPhase.Transition;
            player.GunUpgrades.TryGrant(type);
            Advance();
            return true;
        }

        public void ContinueWithoutReward()
        {
            if (Phase != RunPhase.Reward || choices.Length != 0 || Time.unscaledTime < selectionReadyAt) return;
            Phase = RunPhase.Transition;
            health.Heal(health.Max * 0.2f);
            Advance();
        }

        void Advance()
        {
            choices = Array.Empty<GunUpgradeType>();
            if (LevelIndex + 1 >= LevelCount)
            {
                Phase = RunPhase.Complete;
                Changed?.Invoke(); // Final level also awards a choice, then shows victory.
                return;
            }
            CloseOverlay();
            LevelIndex++;
            EnterRoom();
        }

        void OnPlayerDied()
        {
            if (Phase != RunPhase.Combat) return;
            Phase = RunPhase.Defeated;
            OpenOverlay();
            Changed?.Invoke();
        }

        void OpenOverlay()
        {
            if (ownsOverlay) return;
            // End movement slow-mo before taking ownership of time.
            if (motor != null) motor.Teleport(player.transform.position, player.transform.rotation);
            savedTimeScale = Time.timeScale;
            ownsOverlay = true;
            UIState.IsRunMenuOpen = true;
            Time.timeScale = 0f;
            UIState.SetOpen(this, true);
        }

        void CloseOverlay()
        {
            if (!ownsOverlay) return;
            ownsOverlay = false;
            UIState.IsRunMenuOpen = false;
            Time.timeScale = savedTimeScale;
            UIState.SetOpen(this, false);
        }
    }
}
