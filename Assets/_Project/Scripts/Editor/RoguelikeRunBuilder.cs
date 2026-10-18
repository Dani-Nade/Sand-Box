using System.Linq;
using SandHouse.Player;
using SandHouse.Run;
using SandHouse.Stats;
using SandHouse.UI;
using SandHouse.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SandHouse.Editor
{
    /// <summary>Creates a separate run scene, cloning the saved arena rig without rebuilding or saving the arena.</summary>
    public static class RoguelikeRunBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/RoguelikeRun.unity";
        const string ArenaPath = "Assets/_Project/Scenes/TestArena.unity";
        const string Materials = "Assets/_Project/Art/Materials/Prototype/";

        [MenuItem("Sand House/Build Roguelike Run", priority = 1)]
        public static void FromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Build();
        }

        public static void Build()
        {
            var arena = SceneManager.GetSceneByPath(ArenaPath);
            bool openedArena = !arena.IsValid() || !arena.isLoaded;
            if (openedArena) arena = EditorSceneManager.OpenScene(ArenaPath, OpenSceneMode.Additive);
            // Unity cannot create an additive scene while an untitled default scene is open.
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var open = SceneManager.GetSceneAt(i);
                if (open != arena && string.IsNullOrEmpty(open.path)) EditorSceneManager.CloseScene(open, true);
            }
            var sourcePlayer = arena.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<PlayerMotor>(true)).First();
            var sourceUi = arena.GetRootGameObjects().First(go => go.GetComponentInChildren<StatsPanel>(true) != null);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var player = Object.Instantiate(sourcePlayer.gameObject);
            player.name = "Player"; player.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, -13f), Quaternion.identity);
            var motor = player.GetComponent<PlayerMotor>(); var stats = player.GetComponent<PlayerStats>();
            var health = player.GetComponent<Combat.Health>(); var inventory = player.GetComponentInChildren<WeaponInventory>(true);
            var respawn = player.GetComponent<PlayerRespawn>(); if (respawn != null) respawn.enabled = false;
            var ui = Object.Instantiate(sourceUi); ui.name = "UI";
            foreach (var hud in ui.GetComponentsInChildren<DebugHUD>(true))
            {
                Set(hud, "motor", motor); Set(hud, "weapons", inventory); Set(hud, "showMovementStats", false);
                Set(hud, "showControls", false);
            }
            foreach (var panel in ui.GetComponentsInChildren<StatsPanel>(true))
            {
                Set(panel, "stats", stats); Set(panel, "health", health); Set(panel, "input", player.GetComponent<PlayerInputReader>());
            }
            var sun = new GameObject("Run Sun").AddComponent<Light>(); sun.type = LightType.Directional;
            sun.intensity = 1.4f; sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            RenderSettings.sun = sun; RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.65f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            var run = new GameObject("Roguelike Run").AddComponent<RoguelikeRun>();
            run.Configure(stats, Mat("M_Grid_Floor"), Mat("M_Grid_Wall"), Mat("M_Grid_Obstacle"), Mat("M_Grid_Accent"), Mat("M_Target_Body"), Mat("M_Target_Head"));
            // A non-play preview only. The controller replaces it with the first encounter on Start.
            var preview = new GameObject("Room preview (removed on Play)");
            RunLevelFactory.Build(preview.transform, 0, Mat("M_Grid_Floor"), Mat("M_Grid_Wall"), Mat("M_Grid_Obstacle"), Mat("M_Grid_Accent"));
            Set(run, "room", preview);
            EditorSceneManager.SaveScene(scene, ScenePath);
            // Play just the run scene, not the source arena alongside a second player.
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != scene) EditorSceneManager.CloseScene(SceneManager.GetSceneAt(i), true);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"[RoguelikeRunBuilder] Built {ScenePath}; TestArena was not modified.");
        }

        static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(Materials + name + ".mat");
        static void Set(Object obj, string field, object value)
        {
            var serialized = new SerializedObject(obj);
            var property = serialized.FindProperty(field);
            if (property == null) throw new System.InvalidOperationException($"Missing field {field}");
            if (value is bool flag) property.boolValue = flag;
            else property.objectReferenceValue = (Object)value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
