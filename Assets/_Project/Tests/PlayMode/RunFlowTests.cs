using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SandHouse.Combat;
using SandHouse.Run;
using SandHouse.Stats;
using SandHouse.UI;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SandHouse.Tests
{
    public class RunFlowTests
    {
        GameObject player, root;
        PlayerStats stats;
        RoguelikeRun run;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            player = new GameObject("Run test player");
            stats = player.AddComponent<PlayerStats>();
            root = new GameObject("Run test director");
            run = root.AddComponent<RoguelikeRun>();
            run.Configure(stats, null, null, null, null, null, null);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(root); Object.Destroy(player);
            yield return null;
            Assert.IsFalse(UIState.IsRunMenuOpen);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        static RunEnemy[] Enemies() => Object.FindObjectsByType<RunEnemy>(FindObjectsSortMode.None);
        void Kill(Health victim) => victim.TakeDamage(new DamageInfo { Amount = 10000f, Source = player });
        void ClearRoom()
        {
            foreach (var enemy in Enemies()) Kill(enemy.GetComponent<Health>());
        }
        int Stacks() => Enumerable.Range(0, GunUpgrades.Count).Sum(i => stats.GunUpgrades.GetStacks((GunUpgradeType)i));

        [Test]
        public void ThreeLevelsHaveIncreasingEncountersAndDistinctLayouts()
        {
            Assert.AreEqual(RunPhase.Combat, run.Phase); Assert.AreEqual(4, run.EnemiesRemaining);
            Assert.AreEqual(6, RunLevelFactory.EnemySpawns(1).Length);
            Assert.AreEqual(8, RunLevelFactory.EnemySpawns(2).Length);
            Assert.AreNotEqual(RunLevelFactory.LevelName(0), RunLevelFactory.LevelName(1));
            Assert.AreNotEqual(RunLevelFactory.LevelName(1), RunLevelFactory.LevelName(2));
            Assert.AreEqual(0, Stacks());
        }

        [UnityTest]
        public IEnumerator OnlyFullClearRewardsThreeUniqueChoicesAndOneStatPoint()
        {
            var enemies = Enemies();
            Kill(enemies[0].GetComponent<Health>());
            Assert.AreEqual(RunPhase.Combat, run.Phase); Assert.AreEqual(3, stats.UnspentPoints);
            foreach (var enemy in enemies.Skip(1)) Kill(enemy.GetComponent<Health>());
            Assert.AreEqual(RunPhase.Reward, run.Phase); Assert.AreEqual(4, stats.UnspentPoints);
            Assert.AreEqual(3, run.Choices.Count); Assert.AreEqual(3, run.Choices.Distinct().Count());
            Assert.IsTrue(UIState.IsRunMenuOpen); Assert.IsTrue(UIState.AnyMenuOpen); Assert.AreEqual(0f, Time.timeScale);
            var offered = run.Choices.ToArray();
            Assert.IsFalse(run.TrySelectReward(0), "Debounces the shot that cleared the room");
            yield return new WaitForSecondsRealtime(0.25f);
            CollectionAssert.AreEqual(offered, run.Choices, "Waiting does not reroll the choices");
            Assert.IsTrue(run.TrySelectReward(0));
            Assert.AreEqual(1, stats.GunUpgrades.GetStacks(offered[0]));
            Assert.AreEqual(1, run.LevelIndex); Assert.AreEqual(RunPhase.Combat, run.Phase);
            Assert.AreEqual(6, run.EnemiesRemaining); Assert.AreEqual(4, stats.UnspentPoints);
            Assert.IsFalse(UIState.IsRunMenuOpen); Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(run.TrySelectReward(0), "Double-click cannot award twice");
        }

        [UnityTest]
        public IEnumerator FinalLevelAlsoRewardsThenWinsAndRestartResetsTheBuild()
        {
            stats.Invest(StatType.Health); stats.Invest(StatType.WeaponDamage);
            for (int i = 0; i < RoguelikeRun.LevelCount; i++)
            {
                Assert.AreEqual(i, run.LevelIndex);
                ClearRoom(); yield return new WaitForSecondsRealtime(0.25f);
                Assert.IsTrue(run.TrySelectReward(0));
                Assert.AreEqual(i + 1, Stacks());
                yield return null;
            }
            Assert.AreEqual(RunPhase.Complete, run.Phase); Assert.IsTrue(UIState.IsRunMenuOpen);
            Assert.IsFalse(run.TrySelectReward(0));
            run.RestartRun(); yield return null;
            Assert.AreEqual(RunPhase.Combat, run.Phase); Assert.AreEqual(0, run.LevelIndex);
            Assert.AreEqual(4, run.EnemiesRemaining); Assert.AreEqual(0, Stacks());
            Assert.AreEqual(3, stats.UnspentPoints); Assert.AreEqual(0, stats.GetLevel(StatType.WeaponDamage));
            Assert.AreEqual(100f, player.GetComponent<Health>().Max); Assert.AreEqual(100f, player.GetComponent<Health>().Current);
        }

        [UnityTest]
        public IEnumerator DeathAndFallingEndTheRunAndCanRestart()
        {
            Kill(player.GetComponent<Health>());
            Assert.AreEqual(RunPhase.Defeated, run.Phase); Assert.AreEqual(0f, Time.timeScale);
            run.RestartRun(); yield return null;
            Assert.AreEqual(100f, player.GetComponent<Health>().Current);
            player.transform.position = Vector3.down * 15f;
            yield return null;
            Assert.AreEqual(RunPhase.Defeated, run.Phase);
        }

        [Test]
        public void DraftsAreRandomWithoutDuplicatesAndExcludeCappedUpgrades()
        {
            var seen = new HashSet<GunUpgradeType>();
            for (int seed = 0; seed < 100; seed++)
            {
                var choices = UpgradeDraft.Roll(stats.GunUpgrades, new System.Random(seed));
                Assert.AreEqual(3, choices.Distinct().Count());
                foreach (var type in choices) seen.Add(type);
            }
            Assert.AreEqual(GunUpgrades.Count, seen.Count);
            for (int i = 0; i < GunUpgrades.Count - 2; i++)
                while (stats.GunUpgrades.TryGrant((GunUpgradeType)i)) { }
            var eligible = UpgradeDraft.Roll(stats.GunUpgrades, new System.Random(1));
            Assert.AreEqual(2, eligible.Length);
            CollectionAssert.AreEquivalent(new[] { GunUpgradeType.ArcaneRounds, GunUpgradeType.VampiricRounds }, eligible);
        }

        [UnityTest]
        public IEnumerator AllCappedFallbackContinuesWithoutSoftlock()
        {
            foreach (GunUpgradeType type in Enum.GetValues(typeof(GunUpgradeType)))
                while (stats.GunUpgrades.TryGrant(type)) { }
            ClearRoom();
            Assert.AreEqual(0, run.Choices.Count); Assert.AreEqual(RunPhase.Reward, run.Phase);
            yield return new WaitForSecondsRealtime(0.25f);
            run.ContinueWithoutReward();
            Assert.AreEqual(1, run.LevelIndex); Assert.AreEqual(RunPhase.Combat, run.Phase);
        }

        [UnityTest]
        public IEnumerator InvalidSelectionAndUnrelatedKillsCannotAdvanceOrGrantRewards()
        {
            Assert.IsFalse(run.TrySelectReward(0));
            var other = new GameObject("Unrelated health"); var health = other.AddComponent<Health>(); Kill(health);
            Object.Destroy(other);
            Assert.AreEqual(4, run.EnemiesRemaining); Assert.AreEqual(3, stats.UnspentPoints);
            ClearRoom(); yield return new WaitForSecondsRealtime(0.25f);
            Assert.IsFalse(run.TrySelectReward(-1)); Assert.IsFalse(run.TrySelectReward(3));
            run.ContinueWithoutReward(); Assert.AreEqual(RunPhase.Reward, run.Phase);
            Assert.AreEqual(0, Stacks()); Assert.AreEqual(0, run.LevelIndex);
        }

        [UnityTest]
        public IEnumerator EnemiesMoveAndTakeDamageWithoutRespawning()
        {
            var enemy = Enemies().First(e => e.name == "Melee Pursuer");
            float before = Vector3.Distance(enemy.transform.position, player.transform.position);
            yield return new WaitForSeconds(0.3f);
            Assert.Less(Vector3.Distance(enemy.transform.position, player.transform.position), before);
            Kill(enemy.GetComponent<Health>());
            Assert.IsFalse(enemy.gameObject.activeSelf);
            Assert.AreEqual(3, run.EnemiesRemaining);
        }
    }
}
