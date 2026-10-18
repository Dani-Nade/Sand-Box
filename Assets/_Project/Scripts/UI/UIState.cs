using System;
using System.Collections.Generic;
using UnityEngine;

namespace SandHouse.UI
{
    /// <summary>
    /// Tracks which menus are open. While any menu is open the cursor is free and
    /// mouse look / shooting are blocked (see PlayerLook).
    /// </summary>
    public static class UIState
    {
        static readonly HashSet<object> openMenus = new HashSet<object>();

        public static bool AnyMenuOpen => openMenus.Count > 0;
        /// <summary>True while the pause menu has the game frozen.</summary>
        public static bool IsPaused { get; internal set; }

        public static event Action Changed;

        public static void SetOpen(object menu, bool open)
        {
            bool changed = open ? openMenus.Add(menu) : openMenus.Remove(menu);
            if (changed) Changed?.Invoke();
        }

        // Static state survives between play sessions in the editor when domain reload is off - clear it.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            openMenus.Clear();
            IsPaused = false;
            Changed = null;
        }
    }
}
