using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SandHouse.Combat;
using SandHouse.Player;
using SandHouse.Run;
using SandHouse.UI;
using SandHouse.Weapons;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SandHouse.Tests
{
    public class RunSceneSmokeTests
    {
        Scene loaded;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (loaded.IsValid() && loaded.isLoaded) yield return SceneManager.UnloadSceneAsync(loaded);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(UIState.IsRunMenuOpen);
            Cursor.lockState = CursorLockMode.None;
        }

        [UnityTest]
        public IEnumerator SavedSceneRunsWithRealPlayerWeaponsAndRewardUI()
        {
            if (!Application.CanStreamedLevelBeLoaded("RoguelikeRun")) Assert.Ignore("Build the Roguelike Run scene first.");
            yield return SceneManager.LoadSceneAsync("RoguelikeRun", LoadSceneMode.Additive);
            loaded = SceneManager.GetSceneByName("RoguelikeRun");
            SceneManager.SetActiveScene(loaded);
            yield return null;
            var director = loaded.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<RoguelikeRun>()).Single();
            Assert.AreEqual(RunPhase.Combat, director.Phase);
            var player = director.Player;
            Assert.IsNotNull(player.GetComponent<PlayerInputReader>());
            Assert.IsFalse(player.GetComponent<PlayerRespawn>().enabled);
            var inventory = player.GetComponentInChildren<WeaponInventory>();
            Assert.AreEqual(4, inventory.Weapons.Count); Assert.AreEqual(inventory.Current.MagazineSize, inventory.Current.Ammo);
            var gun = inventory.Current as HitscanWeapon; Assert.IsNotNull(gun);
            var target = Object.FindObjectsByType<RunEnemy>().First(e => e.name == "Melee Pursuer");
            var victim = target.GetComponent<Health>();
            Assert.AreEqual(loaded, target.gameObject.scene);
            var camera = player.GetComponentInChildren<Camera>();
            var shoot = typeof(HitscanWeapon).GetMethod("Shoot", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < 8 && !victim.IsDead; i++)
                shoot.Invoke(gun, new object[] { new Ray(camera.transform.position, (target.transform.position + Vector3.up * 1.5f - camera.transform.position).normalized) });
            Assert.IsTrue(victim.IsDead, "The saved player rig can shoot the spawned enemies");
            Assert.AreEqual(3, director.EnemiesRemaining);
            foreach (var enemy in Object.FindObjectsByType<RunEnemy>())
                enemy.GetComponent<Health>().TakeDamage(new DamageInfo { Amount = 10000f, Source = gun.gameObject });
            Assert.AreEqual(RunPhase.Reward, director.Phase);
            Assert.AreEqual(3, director.Choices.Count);
            Assert.IsNotNull(director.GetComponent<RunRewardPanel>());
            yield return new WaitForSecondsRealtime(0.25f);
            var rewardUI = director.GetComponent<RunRewardPanel>();
            var buttons = (UnityEngine.UI.Button[])typeof(RunRewardPanel).GetField("buttons", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(rewardUI);
            buttons[0].onClick.Invoke();
            Assert.AreEqual(1, director.LevelIndex); Assert.AreEqual(6, director.EnemiesRemaining);
            Assert.AreEqual(inventory.Current.MagazineSize, inventory.Current.Ammo);
        }
    }
}
