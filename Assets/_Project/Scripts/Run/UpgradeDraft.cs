using System;
using System.Collections.Generic;
using SandHouse.Stats;

namespace SandHouse.Run
{
    public static class UpgradeDraft
    {
        /// <summary>Sample without replacement from eligible upgrades. Never offers capped stacks.</summary>
        public static GunUpgradeType[] Roll(GunUpgrades upgrades, Random random, int count = 3)
        {
            if (upgrades == null) throw new ArgumentNullException(nameof(upgrades));
            if (random == null) throw new ArgumentNullException(nameof(random));
            var eligible = new List<GunUpgradeType>();
            for (int i = 0; i < GunUpgrades.Count; i++)
                if (upgrades.CanGrant((GunUpgradeType)i)) eligible.Add((GunUpgradeType)i);
            int length = Math.Min(Math.Max(0, count), eligible.Count);
            var result = new GunUpgradeType[length];
            for (int i = 0; i < length; i++)
            {
                int selected = random.Next(i, eligible.Count);
                var swap = eligible[i]; eligible[i] = eligible[selected]; eligible[selected] = swap;
                result[i] = eligible[i];
            }
            return result;
        }
    }
}
