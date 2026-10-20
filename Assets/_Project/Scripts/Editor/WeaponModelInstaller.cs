using System.IO;
using System.Linq;
using SandHouse.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SandHouse.Editor
{
    /// <summary>
    /// Swaps weapon placeholders for real models exported from Blender.
    /// Menu: Sand House > Install Ornate Revolver - patches the Revolver in TestArena and RoguelikeRun
    /// in place (no rebuild, so hand edits in those scenes are kept). TestArenaBuilder also calls this,
    /// so rebuilt arenas get the model automatically.
    ///
    /// The model comes from Source/Blender/Scripts/export_ornate_revolver.py. Blender's procedural
    /// materials don't survive FBX export, so matching URP materials are created here from the same values.
    /// Also adds the procedural round-by-round reload (RevolverReloadAnimator) and Russian roulette.
    /// </summary>
    public static class WeaponModelInstaller
    {
        public const string OrnateRevolverPath = "Assets/_Project/Art/Models/Weapons/SM_OrnateRevolver.fbx";
        const string MaterialFolder = "Assets/_Project/Art/Materials/Weapons";
        const int IgnoreRaycastLayer = 2;

        static readonly string[] Scenes =
        {
            "Assets/_Project/Scenes/TestArena.unity",
            "Assets/_Project/Scenes/RoguelikeRun.unity",
        };

        /// <summary>Blender material number prefix -> look (linear colours from the .blend, metallic, smoothness).</summary>
        struct MaterialLook
        {
            public string Name;
            public Color LinearColor;
            public float Metallic, Smoothness;
        }

        static readonly (string Prefix, MaterialLook Look)[] OrnateMaterials =
        {
            ("01", new MaterialLook { Name = "M_Ornate_Silver", LinearColor = new Color(0.23f, 0.25f, 0.28f), Metallic = 0.94f, Smoothness = 0.75f }),
            ("02", new MaterialLook { Name = "M_Ornate_BluedSteel", LinearColor = new Color(0.035f, 0.045f, 0.055f), Metallic = 0.92f, Smoothness = 0.73f }),
            ("03", new MaterialLook { Name = "M_Ornate_Bronze", LinearColor = new Color(0.19f, 0.07f, 0.021f), Metallic = 0.85f, Smoothness = 0.66f }),
            ("04", new MaterialLook { Name = "M_Ornate_BrightEdges", LinearColor = new Color(0.4f, 0.205f, 0.075f), Metallic = 0.87f, Smoothness = 0.7f }),
            ("05", new MaterialLook { Name = "M_Ornate_RecessShadow", LinearColor = new Color(0.007f, 0.009f, 0.011f), Metallic = 0.35f, Smoothness = 0.6f }),
            ("06", new MaterialLook { Name = "M_Ornate_Walnut", LinearColor = new Color(0.12f, 0.03f, 0.008f), Metallic = 0f, Smoothness = 0.6f }),
        };

        [MenuItem("Sand House/Install Ornate Revolver", priority = 20)]
        static void InstallInScenes()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(OrnateRevolverPath) == null)
            {
                EditorUtility.DisplayDialog("Ornate Revolver", $"Model not found at {OrnateRevolverPath}.\nRun the Blender export script first.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string returnTo = SceneManager.GetActiveScene().path;
            foreach (string path in Scenes)
            {
                if (!File.Exists(path)) continue;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int installed = 0;
                foreach (var weapon in Object.FindObjectsByType<HitscanWeapon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (weapon.name == "Revolver" && InstallOrnateRevolver(weapon)) installed++;

                if (installed > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                Debug.Log($"[WeaponModelInstaller] {path}: installed the ornate revolver on {installed} weapon(s).");
            }

            if (!string.IsNullOrEmpty(returnTo) && File.Exists(returnTo))
                EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        }

        /// <summary>
        /// Replaces whatever is under the weapon's model slot with the ornate revolver and wires up
        /// muzzle, flash, cylinder and inspect animation. Returns false if the model isn't imported yet.
        /// </summary>
        public static bool InstallOrnateRevolver(Weapon weapon)
        {
            EnsureReloadAnimator(weapon); // works on the placeholder too

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(OrnateRevolverPath);
            if (asset == null) return false;

            var so = new SerializedObject(weapon);
            var model = so.FindProperty("model").objectReferenceValue as Transform;
            if (model == null)
            {
                Debug.LogWarning($"[WeaponModelInstaller] {weapon.name} has no model slot.");
                return false;
            }

            // Clear the placeholder (or a previous install).
            for (int i = model.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(model.GetChild(i).gameObject);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, model);
            instance.name = "OrnateRevolver";
            instance.transform.localPosition = Vector3.zero;

            foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = IgnoreRaycastLayer;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off; // first-person viewmodel
                renderer.sharedMaterials = renderer.sharedMaterials.Select(OrnateMaterialFor).ToArray();
            }

            // Muzzle (exported as an empty) gets the flash light.
            Transform muzzle = instance.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Muzzle");
            Light flash = null;
            if (muzzle != null)
            {
                flash = muzzle.gameObject.AddComponent<Light>();
                flash.type = LightType.Point;
                flash.range = 5f;
                flash.intensity = 4f;
                flash.color = new Color(1f, 0.75f, 0.4f);
                flash.shadows = LightShadows.None;
                flash.enabled = false;
            }

            var inspect = AssetDatabase.LoadAllAssetsAtPath(OrnateRevolverPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

            so.FindProperty("muzzle").objectReferenceValue = muzzle;
            so.FindProperty("muzzleFlash").objectReferenceValue = flash;
            so.FindProperty("spinPart").objectReferenceValue = null; // re-found by name ("Cylinder") at runtime
            so.FindProperty("inspectClip").objectReferenceValue = inspect;
            so.FindProperty("inspectTarget").objectReferenceValue = inspect != null ? instance : null;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (inspect == null) Debug.LogWarning("[WeaponModelInstaller] No inspect animation found in the FBX.");
            return true;
        }

        static readonly MaterialLook Brass = new MaterialLook { Name = "M_Round_Brass", LinearColor = new Color(0.55f, 0.36f, 0.1f), Metallic = 0.9f, Smoothness = 0.7f };
        static readonly MaterialLook Lead = new MaterialLook { Name = "M_Round_Lead", LinearColor = new Color(0.12f, 0.12f, 0.13f), Metallic = 0.6f, Smoothness = 0.4f };

        /// <summary>Adds the procedural round-by-round reload visuals (casings + rounds going in).</summary>
        static void EnsureReloadAnimator(Weapon weapon)
        {
            if (weapon.GetComponent<RussianRoulette>() == null) weapon.gameObject.AddComponent<RussianRoulette>();

            var animator = weapon.GetComponent<RevolverReloadAnimator>();
            if (animator == null) animator = weapon.gameObject.AddComponent<RevolverReloadAnimator>();
            var so = new SerializedObject(animator);
            so.FindProperty("brassMaterial").objectReferenceValue = GetOrCreate(Brass);
            so.FindProperty("leadMaterial").objectReferenceValue = GetOrCreate(Lead);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Material OrnateMaterialFor(Material imported)
        {
            string name = imported != null ? imported.name : "";
            foreach (var (prefix, look) in OrnateMaterials)
                if (name.StartsWith(prefix)) return GetOrCreate(look);
            return GetOrCreate(OrnateMaterials[0].Look); // unnamed slots: silver
        }

        static Material GetOrCreate(MaterialLook look)
        {
            string path = $"{MaterialFolder}/{look.Name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat; // keep any tweaks made in the Inspector

            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", look.LinearColor.gamma); // Blender values are linear; material colours are sRGB
            mat.SetFloat("_Metallic", look.Metallic);
            mat.SetFloat("_Smoothness", look.Smoothness);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
