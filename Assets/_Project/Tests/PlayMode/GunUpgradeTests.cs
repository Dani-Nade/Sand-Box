using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SandHouse.Combat;
using SandHouse.Stats;
using SandHouse.UI;
using SandHouse.Weapons;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SandHouse.Tests
{
    public class UpgradeTestGun : HitscanWeapon
    {
        public void Configure(WeaponData settings, Camera camera) { data = settings; aimCamera = camera; }
        public void ShootForward() => Shoot(new Ray(aimCamera.transform.position, aimCamera.transform.forward));
    }

    public class GunUpgradeTests
    {
        readonly List<Object> cleanup = new List<Object>();
        readonly Vector3 origin = new Vector3(10000f, 10000f, 10000f);
        GameObject player;
        PlayerStats stats;
        Health owner;
        GunUpgrades upgrades;
        UpgradeTestGun gun;
        WeaponData data;
        Camera camera;

        static void Set(object obj, string field, object value)
        {
            for (Type type = obj.GetType(); type != null; type = type.BaseType)
            {
                var info = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                if (info == null) continue;
                info.SetValue(obj, value); return;
            }
            throw new MissingFieldException(field);
        }
        static object Call(object obj, Type type, string method, params object[] args)
            => type.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args);
        T Track<T>(T obj) where T : Object { cleanup.Add(obj); return obj; }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            player = Track(new GameObject("Upgrade test player"));
            player.SetActive(false);
            player.transform.position = origin;
            owner = player.AddComponent<Health>();
            stats = player.AddComponent<PlayerStats>();
            var cam = new GameObject("Aim camera"); cam.transform.SetParent(player.transform, false);
            camera = cam.AddComponent<Camera>(); camera.enabled = false;
            data = Track(ScriptableObject.CreateInstance<WeaponData>());
            data.damage = 10f; data.range = 50f; data.recoilHopMultiplier = 0f; data.autoReloadWhenEmpty = false;
            var weapon = new GameObject("Test gun"); weapon.transform.SetParent(cam.transform, false);
            gun = weapon.AddComponent<UpgradeTestGun>(); gun.Configure(data, camera); gun.enabled = false;
            player.SetActive(true);
            upgrades = stats.GunUpgrades;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Test-owned visuals are spawned at the remote physics-test origin, not in the arena.
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t != null && t.parent == null && Vector3.Distance(t.position, origin) < 100f)
                    Object.Destroy(t.gameObject);
            foreach (var line in Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None))
                if (line.name == "Tracer" && line.positionCount > 0 && Vector3.Distance(line.GetPosition(0), origin) < 100f)
                    Object.Destroy(line.gameObject);
            foreach (var obj in cleanup) if (obj != null) Object.Destroy(obj);
            cleanup.Clear();
            yield return null;
        }

        Health Target(Vector3 offset, float hp = 100f, Vector3? scale = null)
        {
            var root = Track(new GameObject("Enemy")); root.transform.position = origin + offset;
            var health = root.AddComponent<Health>(); health.SetMaxHealth(hp);
            AddHitbox(root.transform, Vector3.zero, scale ?? Vector3.one, health);
            Physics.SyncTransforms();
            return health;
        }
        void AddHitbox(Transform parent, Vector3 offset, Vector3 size, Health health, float multiplier = 1f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent, false); go.transform.localPosition = offset; go.transform.localScale = size;
            go.AddComponent<Hitbox>().Configure(health, multiplier);
        }
        GameObject Wall(Vector3 offset, Vector3 scale)
        {
            var go = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            go.transform.position = origin + offset; go.transform.localScale = scale;
            Physics.SyncTransforms(); return go;
        }
        void Grant(GunUpgradeType type, int count = 1)
        {
            for (int i = 0; i < count; i++) Assert.IsTrue(upgrades.TryGrant(type));
        }

        [Test]
        public void AllTenUpgradesHaveDescriptionsAndEnforcedCaps()
        {
            Assert.AreEqual(10, Enum.GetValues(typeof(GunUpgradeType)).Length);
            foreach (GunUpgradeType type in Enum.GetValues(typeof(GunUpgradeType)))
            {
                Assert.IsNotEmpty(GunUpgrades.DisplayName(type));
                Assert.IsNotEmpty(GunUpgrades.Description(type));
                Grant(type, GunUpgrades.MaxStacks(type));
                int points = stats.UnspentPoints;
                Assert.IsFalse(upgrades.TryGrant(type));
                Assert.AreEqual(points, stats.UnspentPoints);
            }
        }

        [Test]
        public void FreeAwardsDoNotSpendBaseStatPointsAndRejectInvalidIds()
        {
            Assert.IsTrue(upgrades.TryGrant(GunUpgradeType.RapidFire));
            Assert.IsTrue(stats.Invest(StatType.MagicDamage));
            Assert.IsTrue(upgrades.TryGrant(GunUpgradeType.Multishot));
            Assert.AreEqual(2, stats.UnspentPoints);
            Assert.IsFalse(upgrades.TryGrant((GunUpgradeType)(-1)));
            Assert.IsFalse(upgrades.TryGrant((GunUpgradeType)100));
            Assert.AreEqual(0, upgrades.GetStacks(GunUpgradeType.HeavyRounds));
        }

        [Test]
        public void RateMagazineAndReloadScaleWithoutMutatingWeaponData()
        {
            Grant(GunUpgradeType.RapidFire, 2); Grant(GunUpgradeType.ExtendedMagazine, 2); Grant(GunUpgradeType.QuickReload, 2);
            Assert.That(gun.FireInterval, Is.EqualTo(data.fireInterval / 1.3f).Within(0.0001f));
            Assert.AreEqual(9, gun.MagazineSize); Assert.AreEqual(6, gun.Ammo);
            Assert.That(gun.ReloadTimeMultiplier, Is.EqualTo(0.64f).Within(0.0001f));
            Assert.AreEqual(6, data.magazineSize); Assert.AreEqual(0.22f, data.fireInterval);
        }

        [Test]
        public void BaselineGunStillDealsOriginalDamage()
        {
            var target = Target(new Vector3(0f, 0f, 5f)); gun.ShootForward();
            Assert.That(target.Current, Is.EqualTo(90f).Within(0.001f));
        }

        [Test]
        public void HeavyAndArcaneUseSeparateStatsWithOneHeadshotMultiplier()
        {
            stats.Invest(StatType.WeaponDamage); stats.Invest(StatType.MagicDamage);
            Grant(GunUpgradeType.HeavyRounds); Grant(GunUpgradeType.ArcaneRounds);
            var target = Target(new Vector3(0f, 0f, 5f));
            target.GetComponentInChildren<Hitbox>().Configure(target, 2f);
            gun.ShootForward();
            float expected = (10f * 1.1f * 1.2f + 10f * 0.2f * 1.15f) * 2f;
            Assert.That(target.Current, Is.EqualTo(100f - expected).Within(0.001f));
        }

        [Test]
        public void MultishotAddsBulletsButConsumesOnlyOneAmmo()
        {
            Grant(GunUpgradeType.Multishot, 3);
            var target = Target(new Vector3(0f, 0f, 5f), 100f, new Vector3(10f, 10f, 1f));
            int hits = 0; gun.HitTarget += _ => hits++;
            Call(gun, typeof(Weapon), "Fire");
            Assert.AreEqual(4, hits); Assert.That(target.Current, Is.EqualTo(60f).Within(0.001f));
            Assert.AreEqual(5, gun.Ammo);
        }

        [Test]
        public void PiercingHitsOnlyTwoEnemiesAndNeverDoubleHitsTheirColliders()
        {
            Grant(GunUpgradeType.PiercingRounds);
            var first = Target(new Vector3(0f, 0f, 4f));
            AddHitbox(first.transform, new Vector3(0f, 0f, 1f), Vector3.one, first);
            var second = Target(new Vector3(0f, 0f, 8f));
            var third = Target(new Vector3(0f, 0f, 12f));
            Physics.SyncTransforms(); gun.ShootForward();
            Assert.AreEqual(90f, first.Current); Assert.AreEqual(90f, second.Current); Assert.AreEqual(100f, third.Current);
        }

        [Test]
        public void WallsStillBlockPiercing()
        {
            Grant(GunUpgradeType.PiercingRounds, 5);
            Wall(new Vector3(0f, 0f, 3f), new Vector3(10f, 10f, 0.25f));
            var target = Target(new Vector3(0f, 0f, 5f)); gun.ShootForward();
            Assert.AreEqual(100f, target.Current);
        }

        [Test]
        public void RicochetReflectsAndRetainsSeventyFivePercentDamage()
        {
            Grant(GunUpgradeType.Ricochet);
            var wall = Wall(new Vector3(0f, 0f, 5f), new Vector3(10f, 10f, 0.2f));
            wall.transform.rotation = Quaternion.Euler(0f, 45f, 0f); Physics.SyncTransforms();
            Assert.IsTrue(Physics.Raycast(origin, Vector3.forward, out var hit, 10f));
            Vector3 reflected = Vector3.Reflect(Vector3.forward, hit.normal);
            var target = Target(hit.point - origin + reflected * 3f); gun.ShootForward();
            Assert.That(target.Current, Is.EqualTo(92.5f).Within(0.001f));
        }

        [Test]
        public void ExplosionsDamageEachEnemyOnceAndNeverTheShooter()
        {
            Grant(GunUpgradeType.ExplosiveRounds);
            var target = Target(new Vector3(0f, 0f, 5f));
            AddHitbox(target.transform, new Vector3(0f, 0.2f, 0f), Vector3.one, target);
            var nearby = Target(new Vector3(1.5f, 0f, 4.5f));
            var selfCollider = player.AddComponent<BoxCollider>(); selfCollider.center = new Vector3(0f, 1f, 4.5f);
            owner.TakeDamage(new DamageInfo { Amount = 20f });
            Physics.SyncTransforms(); gun.ShootForward();
            Assert.That(target.Current, Is.InRange(87f, 87.1f));
            Assert.Less(nearby.Current, 100f); Assert.AreEqual(80f, owner.Current);
            foreach (var collider in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                if (collider.name == "Explosion" || collider.name == "Impact") Assert.IsFalse(collider.enabled);
        }

        [Test]
        public void ExplosionsCannotDamageThroughCover()
        {
            Grant(GunUpgradeType.ExplosiveRounds);
            Target(new Vector3(0f, 0f, 5f));
            var behindCover = Target(new Vector3(1.7f, 0f, 4.5f));
            Wall(new Vector3(0.85f, 0f, 4.5f), new Vector3(0.2f, 3f, 3f)); gun.ShootForward();
            Assert.AreEqual(100f, behindCover.Current);
        }

        [Test]
        public void VampiricKillsHealByMaxHealthWithoutRepeatedCorpseRewards()
        {
            Grant(GunUpgradeType.VampiricRounds, 2);
            stats.Invest(StatType.Health); owner.TakeDamage(new DamageInfo { Amount = 50f });
            var target = Target(new Vector3(0f, 0f, 5f), 5f);
            int points = stats.UnspentPoints; gun.ShootForward(); gun.ShootForward();
            Assert.IsTrue(target.IsDead); Assert.That(owner.Current, Is.EqualTo(74.8f).Within(0.001f));
            Assert.AreEqual(points, stats.UnspentPoints);
        }

        [Test]
        public void SplashKillsAlsoHealAndRespectMaxHealth()
        {
            Grant(GunUpgradeType.VampiricRounds); Grant(GunUpgradeType.ExplosiveRounds);
            owner.TakeDamage(new DamageInfo { Amount = 1f });
            Target(new Vector3(0f, 0f, 5f));
            var nearby = Target(new Vector3(1.5f, 0f, 4.5f), 1f); gun.ShootForward();
            Assert.IsTrue(nearby.IsDead); Assert.AreEqual(owner.Max, owner.Current);
        }

        [Test]
        public void UpgradesFollowPlayerAcrossGunsButDoNotModifyDynamite()
        {
            Grant(GunUpgradeType.RapidFire); Grant(GunUpgradeType.ExtendedMagazine); Grant(GunUpgradeType.HeavyRounds);
            var second = new GameObject("Second gun"); second.SetActive(false); second.transform.SetParent(player.transform, false);
            var other = second.AddComponent<UpgradeTestGun>(); other.Configure(data, camera); other.enabled = false; second.SetActive(true);
            Assert.AreEqual(gun.FireInterval, other.FireInterval); Assert.AreEqual(gun.MagazineSize, other.MagazineSize);
            gun.gameObject.SetActive(false); gun.gameObject.SetActive(true);
            Assert.AreEqual(1, upgrades.GetStacks(GunUpgradeType.HeavyRounds));
            var throwable = new GameObject("Throwable"); throwable.SetActive(false); throwable.transform.SetParent(player.transform, false);
            var dynamite = throwable.AddComponent<ThrowableWeapon>(); Set(dynamite, "data", data); dynamite.enabled = false; throwable.SetActive(true);
            Assert.AreEqual(data.fireInterval, dynamite.FireInterval); Assert.AreEqual(data.magazineSize, dynamite.MagazineSize);
        }

        [UnityTest]
        public IEnumerator ExtendedMagazineCanBeFilledAndQuickReloadAffectsBothStyles()
        {
            Grant(GunUpgradeType.ExtendedMagazine); Grant(GunUpgradeType.QuickReload);
            data.reloadTime = 0.04f; data.roundReloadTime = 0.01f;
            foreach (bool oneAtATime in new[] { false, true })
            {
                data.reloadOneAtATime = oneAtATime;
                Call(gun, typeof(Weapon), "Fire");
                gun.StartCoroutine((IEnumerator)Call(gun, typeof(Weapon), "Reload"));
                yield return new WaitForSeconds(0.5f);
                Assert.AreEqual(8, gun.Ammo); Assert.IsFalse(gun.IsReloading);
            }
        }

        [UnityTest]
        public IEnumerator StatsPanelShowsReadOnlyUpgradesAndCleansUpMenuState()
        {
            var go = Track(new GameObject("Stats UI test")); go.SetActive(false);
            var panel = go.AddComponent<StatsPanel>(); Set(panel, "stats", stats); Set(panel, "health", owner);
            go.SetActive(true); yield return null;
            Call(panel, typeof(StatsPanel), "SetInvesting", true);
            Assert.IsTrue(UIState.AnyMenuOpen);
            var titles = (Text[])typeof(StatsPanel).GetField("upgradeTitles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
            Assert.AreEqual(10, titles.Length);
            Assert.AreEqual(3, go.GetComponentsInChildren<Button>().Length);
            Assert.AreEqual(0, upgrades.GetStacks(GunUpgradeType.RapidFire));
            Assert.AreEqual(3, stats.UnspentPoints);
            go.SetActive(false); Assert.IsFalse(UIState.AnyMenuOpen);
        }
    }
}
