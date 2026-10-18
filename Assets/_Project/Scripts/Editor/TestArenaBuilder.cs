using System.Collections.Generic;
using System.IO;
using SandHouse.Combat;
using SandHouse.Player;
using SandHouse.Targets;
using SandHouse.UI;
using SandHouse.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SandHouse.Editor
{
    /// <summary>
    /// Generates the greybox Test Arena scene from code, so it can be rebuilt at any time
    /// (menu: Sand House > Build Test Arena). Hand-made changes to the scene are lost on rebuild,
    /// so tweak this script instead - or duplicate the scene if you want to hand-edit it.
    ///
    /// Layout (top-down, spawn at the south looking north):
    ///   west  = shooting range (10 / 25 / 50 m targets + moving targets)
    ///   centre = cover area with a tower
    ///   east  = movement course (step heights, jump gaps, slopes, crouch tunnel)
    ///   north door = AngeloMaN87 Prototype Map (if the pack is in the project)
    /// </summary>
    public static class TestArenaBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/TestArena.unity";
        const string MaterialFolder = "Assets/_Project/Art/Materials/Prototype";
        const string PackFolder = "Assets/AngeloMaN87/Prototype Map";
        const string GridShaderName = "SandHouse/Prototype Grid";
        const int IgnoreRaycastLayer = 2;

        const float ArenaHalfSize = 50f;
        const float WallHeight = 6f;
        static readonly Vector3 SpawnPosition = new Vector3(0f, 0.05f, -44f);

        class MaterialSet
        {
            public Material Floor, Wall, Obstacle, Accent, Target, TargetHead, Tracer, Impact, GunMetal, GunWood, Rope;
        }

        static MaterialSet mats;
        static Font labelFont;

        [MenuItem("Sand House/Build Test Arena", priority = 0)]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        /// <summary>
        /// Batch mode entry point:
        /// Unity.exe -batchmode -quit -projectPath &lt;path&gt; -executeMethod SandHouse.Editor.TestArenaBuilder.BuildFromCommandLine
        /// </summary>
        public static void BuildFromCommandLine() => Build();

        static void Build()
        {
            FixPackMaterials();
            mats = CreateMaterials();
            labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLighting();

            var level = new GameObject("Level").transform;
            BuildShell(Group("Shell", level));
            BuildSpawn(Group("Spawn", level));
            BuildShootingRange(Group("Shooting Range", level));
            BuildCoverArea(Group("Cover Area", level));
            BuildMovementCourse(Group("Movement Course", level));
            BuildMovementTech(Group("Movement Tech", level));
            BuildCrazyTech(Group("Crazy Tech", level));
            PlacePrototypeMap(level);

            var player = BuildPlayer(out var motor, out var health, out var revolver);
            BuildHud(motor, health, revolver);
            player.transform.SetAsFirstSibling();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TestArenaBuilder] Built {ScenePath}");
        }

        // ------------------------------------------------------------------ materials

        /// <summary>The Prototype Map pack ships with Built-in pipeline materials (pink in URP). Convert them to URP Lit.</summary>
        static void FixPackMaterials()
        {
            if (!AssetDatabase.IsValidFolder(PackFolder)) return;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return;

            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { PackFolder }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat == null || mat.shader == lit) continue;

                var texture = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                var color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                mat.shader = lit;
                if (texture != null) mat.SetTexture("_BaseMap", texture);
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Smoothness", 0.15f);
                EditorUtility.SetDirty(mat);
                Debug.Log($"[TestArenaBuilder] Converted {mat.name} to URP Lit");
            }
        }

        static MaterialSet CreateMaterials()
        {
            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var grid = Shader.Find(GridShaderName);
            if (grid == null)
            {
                Debug.LogWarning($"[TestArenaBuilder] Shader '{GridShaderName}' not found, falling back to URP Lit.");
                grid = lit;
            }

            var grey = FindTexture("PrototypeMap_Grey");
            var blue = FindTexture("PrototypeMap_Blue");
            var orange = FindTexture("PrototypeMap_Orange");

            return new MaterialSet
            {
                Floor = GridMaterial("M_Grid_Floor", grid, grey, Color.white),
                Wall = GridMaterial("M_Grid_Wall", grid, blue, Color.white),
                Obstacle = GridMaterial("M_Grid_Obstacle", grid, orange, Color.white),
                Accent = GridMaterial("M_Grid_Accent", grid, grey, new Color(1f, 0.95f, 0.75f)),
                Target = SolidMaterial("M_Target_Body", lit, new Color(0.8f, 0.2f, 0.15f), 0f, 0.3f),
                TargetHead = SolidMaterial("M_Target_Head", lit, new Color(1f, 0.78f, 0.2f), 0f, 0.3f),
                Impact = SolidMaterial("M_Impact", lit, new Color(0.05f, 0.05f, 0.05f), 0f, 0f),
                GunMetal = SolidMaterial("M_Gun_Metal", lit, new Color(0.22f, 0.22f, 0.24f), 0.8f, 0.6f),
                GunWood = SolidMaterial("M_Gun_Wood", lit, new Color(0.36f, 0.2f, 0.1f), 0f, 0.35f),
                Tracer = SolidMaterial("M_Tracer", unlit, new Color(1f, 0.85f, 0.5f), 0f, 0f),
                Rope = SolidMaterial("M_Rope", unlit, new Color(0.55f, 0.4f, 0.22f), 0f, 0f),
            };
        }

        static Texture2D FindTexture(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{name} t:Texture2D"))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                if (tex != null && tex.name == name) return tex;
            }
            Debug.LogWarning($"[TestArenaBuilder] Texture '{name}' not found - grid will be untextured.");
            return null;
        }

        static Material GridMaterial(string name, Shader shader, Texture texture, Color tint)
        {
            return GetOrCreateMaterial(name, shader, m =>
            {
                if (texture != null) m.SetTexture("_BaseMap", texture);
                m.SetColor("_BaseColor", tint);
                if (m.HasProperty("_TextureWorldSize")) m.SetFloat("_TextureWorldSize", 2f); // pack textures cover 2 x 2 m
            });
        }

        static Material SolidMaterial(string name, Shader shader, Color color, float metallic, float smoothness)
        {
            return GetOrCreateMaterial(name, shader, m =>
            {
                m.SetColor("_BaseColor", color);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            });
        }

        static Material GetOrCreateMaterial(string name, Shader shader, System.Action<Material> setup)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            setup(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ lighting

        static void SetupLighting()
        {
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            RenderSettings.sun = sun;

            Color sky = new Color(0.55f, 0.62f, 0.75f);
            Color equator = new Color(0.45f, 0.45f, 0.47f);
            Color ground = new Color(0.28f, 0.26f, 0.24f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;

            // Set the ambient probe directly so lighting looks right without a lighting bake.
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(equator);
            probe.AddDirectionalLight(Vector3.up, (sky - equator) * 0.8f, 1f);
            probe.AddDirectionalLight(Vector3.down, (ground - equator) * 0.8f, 1f);
            RenderSettings.ambientProbe = probe;

            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.fog = false;
        }

        // ------------------------------------------------------------------ level sections

        static void BuildShell(Transform parent)
        {
            float size = ArenaHalfSize * 2f;
            float h = WallHeight;
            float edge = ArenaHalfSize + 0.5f;

            Box("Floor", parent, new Vector3(0f, -0.5f, 0f), new Vector3(size + 2f, 1f, size + 2f), mats.Floor);
            Box("Wall South", parent, new Vector3(0f, h / 2, -edge), new Vector3(size + 2f, h, 1f), mats.Wall);
            Box("Wall West", parent, new Vector3(-edge, h / 2, 0f), new Vector3(1f, h, size), mats.Wall);
            Box("Wall East", parent, new Vector3(edge, h / 2, 0f), new Vector3(1f, h, size), mats.Wall);

            // North wall has an 8 m doorway in the middle leading to the Prototype Map.
            float doorHalf = 4f;
            float pieceLength = ArenaHalfSize + 1f - doorHalf;
            Box("Wall North L", parent, new Vector3(-(doorHalf + pieceLength / 2), h / 2, edge), new Vector3(pieceLength, h, 1f), mats.Wall);
            Box("Wall North R", parent, new Vector3(doorHalf + pieceLength / 2, h / 2, edge), new Vector3(pieceLength, h, 1f), mats.Wall);
            Box("Door Lintel", parent, new Vector3(0f, h - 1f, edge), new Vector3(doorHalf * 2, 2f, 1f), mats.Wall);
            Label("PROTOTYPE MAP", parent, new Vector3(0f, h - 1f, edge - 0.55f), Quaternion.identity, 0.12f);
        }

        static void BuildSpawn(Transform parent)
        {
            Box("Spawn Pad", parent, new Vector3(0f, 0.025f, SpawnPosition.z), new Vector3(4f, 0.05f, 4f), mats.Accent);
            Label("SPAWN", parent, new Vector3(0f, 0.06f, SpawnPosition.z - 1.4f), Quaternion.Euler(90f, 0f, 0f), 0.1f);
            Label("<  RANGE", parent, new Vector3(-5f, 2f, SpawnPosition.z + 4f), Quaternion.identity, 0.08f);
            Label("MOVEMENT  >", parent, new Vector3(5f, 2f, SpawnPosition.z + 4f), Quaternion.identity, 0.08f);
        }

        static void BuildShootingRange(Transform parent)
        {
            const float firingLineZ = -38f;
            const float centreX = -30f;
            const float width = 32f;
            float[] lanes = { -40f, -30f, -20f };
            float[] distances = { 10f, 25f, 50f };

            // Firing line: a waist-high rail you shoot over.
            Box("Firing Rail", parent, new Vector3(centreX, 0.5f, firingLineZ + 0.5f), new Vector3(width, 1f, 0.3f), mats.Accent);
            Label("FIRING LINE", parent, new Vector3(centreX, 0.06f, firingLineZ - 1f), Quaternion.Euler(90f, 0f, 0f), 0.1f);

            // Lane dividers painted on the floor.
            for (int i = 0; i <= lanes.Length; i++)
            {
                float x = centreX - width / 2 + i * (width / lanes.Length);
                Box($"Lane Line {i}", parent, new Vector3(x, 0.01f, firingLineZ + 26f), new Vector3(0.1f, 0.02f, 52f), mats.Accent);
            }

            foreach (float d in distances)
            {
                float z = firingLineZ + d;
                Box($"Distance Line {d}m", parent, new Vector3(centreX, 0.01f, z), new Vector3(width, 0.02f, 0.1f), mats.Accent);
                Label($"{d:0} m", parent, new Vector3(centreX, 0.04f, z - 1.2f), Quaternion.Euler(90f, 0f, 0f), 0.14f);
                foreach (float x in lanes)
                    Dummy(parent, new Vector3(x, 0f, z + 0.5f));
            }

            // Moving targets for tracking practice.
            Dummy(parent, new Vector3(centreX, 0f, firingLineZ + 18f), new Vector3(24f, 0f, 0f), 3f);
            Dummy(parent, new Vector3(centreX, 0f, firingLineZ + 35f), new Vector3(24f, 0f, 0f), 5.5f);
            Label("MOVING 18 m", parent, new Vector3(centreX - 14f, 2.4f, firingLineZ + 18f), Quaternion.identity, 0.06f);
            Label("MOVING 35 m", parent, new Vector3(centreX - 14f, 2.4f, firingLineZ + 35f), Quaternion.identity, 0.06f);

            // Backstop.
            Box("Backstop", parent, new Vector3(centreX, WallHeight / 2, firingLineZ + 58f), new Vector3(width, WallHeight, 1f), mats.Wall);
            Label("SHOOTING RANGE", parent, new Vector3(centreX, 4.5f, firingLineZ + 57.4f), Quaternion.identity, 0.2f);
        }

        static void BuildCoverArea(Transform parent)
        {
            // Crates (1 m and 2 m) and half walls (1.2 m: hides you when crouched, shoot over when standing).
            BoxOnFloor("Crate 1m", parent, -6f, -26f, new Vector3(1f, 1f, 1f), mats.Obstacle);
            BoxOnFloor("Crate 1m", parent, -5f, -26f, new Vector3(1f, 1f, 1f), mats.Obstacle);
            BoxOnFloor("Crate 2m", parent, 6f, -24f, new Vector3(2f, 2f, 2f), mats.Obstacle);
            BoxOnFloor("Crate 1m", parent, 6f, -22.5f, new Vector3(1f, 1f, 1f), mats.Obstacle);
            BoxOnFloor("Half Wall 1.2m", parent, 0f, -18f, new Vector3(6f, 1.2f, 0.5f), mats.Obstacle);
            BoxOnFloor("Half Wall 1.2m", parent, -7f, -10f, new Vector3(0.5f, 1.2f, 5f), mats.Obstacle);
            BoxOnFloor("Half Wall 1.2m", parent, 7f, -10f, new Vector3(0.5f, 1.2f, 5f), mats.Obstacle);
            BoxOnFloor("Full Wall 3m", parent, -3f, -2f, new Vector3(4f, 3f, 0.5f), mats.Wall);
            BoxOnFloor("Full Wall 3m", parent, 4f, 2f, new Vector3(4f, 3f, 0.5f), mats.Wall);
            Label("COVER", parent, new Vector3(0f, 0.04f, -28f), Quaternion.Euler(90f, 0f, 0f), 0.12f);

            // Tower: 4 m high platform with a ramp, for shooting from / at elevation.
            const float towerHeight = 4f;
            BoxOnFloor("Tower", parent, 0f, 15f, new Vector3(6f, towerHeight, 6f), mats.Wall);
            Ramp("Tower Ramp", parent, new Vector3(0f, 0f, 12f), 3f, towerHeight, 25f, endAtZ: true);
            BoxOnFloor("Tower Parapet", parent, 0f, 17.75f, new Vector3(6f, 1f, 0.5f), mats.Obstacle, baseY: towerHeight);
            Label("TOWER 4 m", parent, new Vector3(0f, towerHeight + 1.8f, 12f), Quaternion.identity, 0.07f);

            Dummy(parent, new Vector3(-1.5f, towerHeight, 16.5f));
            Dummy(parent, new Vector3(-6f, 0f, -11f));
            Dummy(parent, new Vector3(4f, 0f, 3f));
        }

        static void BuildMovementCourse(Transform parent)
        {
            // Step heights - stepOffset is 0.4 m, so 0.25 is walkable and anything higher needs a jump.
            float[] steps = { 0.25f, 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f };
            for (int i = 0; i < steps.Length; i++)
            {
                float x = 16f + i * 4.2f;
                BoxOnFloor($"Step {steps[i]}m", parent, x, -36f, new Vector3(3f, steps[i], 3f), mats.Obstacle);
                Label($"{steps[i]:0.##} m", parent, new Vector3(x, 0.04f, -38.3f), Quaternion.Euler(90f, 0f, 0f), 0.08f);
            }
            Label("STEP HEIGHT", parent, new Vector3(28.6f, 3f, -36f), Quaternion.identity, 0.08f);

            // Jump gaps between 1 m high platforms.
            float[] gaps = { 2f, 3f, 4f, 5f, 6f, 7f };
            for (int i = 0; i < gaps.Length; i++)
            {
                float x = 16f + i * 5.5f;
                float startZ = -28f;
                BoxOnFloor($"Gap {gaps[i]}m Takeoff", parent, x, startZ + 3f, new Vector3(3f, 1f, 6f), mats.Obstacle);
                BoxOnFloor($"Gap {gaps[i]}m Landing", parent, x, startZ + 6f + gaps[i] + 2f, new Vector3(3f, 1f, 4f), mats.Obstacle);
                Label($"{gaps[i]:0} m", parent, new Vector3(x, 1.04f, startZ + 1f), Quaternion.Euler(90f, 0f, 0f), 0.08f);
            }
            Label("JUMP GAPS", parent, new Vector3(29.75f, 3f, -28.5f), Quaternion.identity, 0.08f);

            // Slopes up to a 2.5 m platform - slopeLimit is 45 degrees.
            float[] angles = { 15f, 30f, 45f, 60f };
            for (int i = 0; i < angles.Length; i++)
            {
                float x = 18f + i * 8f;
                Ramp($"Slope {angles[i]}", parent, new Vector3(x, 0f, 0f), 4f, 2.5f, angles[i], endAtZ: false);
                Label($"{angles[i]:0}°", parent, new Vector3(x, 0.04f, -1.5f), Quaternion.Euler(90f, 0f, 0f), 0.1f);
            }
            Label("SLOPES", parent, new Vector3(30f, 4.5f, 6f), Quaternion.identity, 0.08f);

            // Crouch tunnel: 1.3 m clearance (crouch height is 1.1 m, standing is 1.8 m).
            const float tunnelX = 21f, tunnelZ = 33f, tunnelLength = 8f, clearance = 1.3f, inner = 2.5f;
            BoxOnFloor("Tunnel Wall L", parent, tunnelX - inner / 2 - 0.5f, tunnelZ, new Vector3(1f, 3f, tunnelLength), mats.Wall);
            BoxOnFloor("Tunnel Wall R", parent, tunnelX + inner / 2 + 0.5f, tunnelZ, new Vector3(1f, 3f, tunnelLength), mats.Wall);
            BoxOnFloor("Tunnel Roof", parent, tunnelX, tunnelZ, new Vector3(inner + 2f, 3f - clearance, tunnelLength), mats.Wall, baseY: clearance);
            Label("CROUCH 1.3 m", parent, new Vector3(tunnelX, 3.6f, tunnelZ - tunnelLength / 2), Quaternion.identity, 0.07f);

            // A physics crate pile to shoot and push around.
            for (int i = 0; i < 6; i++)
            {
                var crate = BoxOnFloor("Physics Crate", parent, 36f + (i % 3) * 1.1f, 32f, new Vector3(1f, 1f, 1f), mats.Obstacle, baseY: (i / 3) * 1.05f);
                var body = crate.AddComponent<Rigidbody>();
                body.mass = 20f;
            }
            Label("PHYSICS", parent, new Vector3(37.1f, 3.2f, 31f), Quaternion.identity, 0.07f);
        }

        static void BuildMovementTech(Transform parent)
        {
            // Mantle walls. Max mantle reach is 1.5 m above your feet, so ~2.8 m walls need a jump at the peak;
            // the 3.2 m wall is out of reach on purpose. Run/dash at them and press Ctrl at the end of the climb to mantle-slide.
            float[] heights = { 1f, 1.5f, 2f, 2.5f, 3.2f };
            for (int i = 0; i < heights.Length; i++)
            {
                float x = -11f + i * 4f;
                BoxOnFloor($"Mantle Wall {heights[i]}m", parent, x, 24f, new Vector3(3f, heights[i], 2f), mats.Obstacle);
                Label($"{heights[i]:0.#} m", parent, new Vector3(x, 0.04f, 21.8f), Quaternion.Euler(90f, 0f, 0f), 0.08f);
            }
            Label("MANTLE  (jump + hold W)   /   MANTLE SLIDE  (run in, press Ctrl as you reach the top)", parent, new Vector3(-3f, 4f, 25.2f), Quaternion.identity, 0.045f);

            // Wall run corridor: hold Space beside either wall, release and press again to jump off.
            const float corridorLeft = 8f, corridorRight = 12f, corridorStart = 28f, corridorLength = 16f, goalHeight = 1.5f;
            float corridorMid = (corridorLeft + corridorRight) / 2f;
            BoxOnFloor("Run Wall L", parent, corridorLeft, corridorStart + corridorLength / 2, new Vector3(0.5f, 9f, corridorLength), mats.Wall);
            BoxOnFloor("Run Wall R", parent, corridorRight, corridorStart + corridorLength / 2, new Vector3(0.5f, 9f, corridorLength), mats.Wall);
            BoxOnFloor($"Run Goal {goalHeight}m", parent, corridorMid, corridorStart + corridorLength - 2f, new Vector3(corridorRight - corridorLeft - 0.5f, goalHeight, 4f), mats.Obstacle);
            Label("WALL RUN", parent, new Vector3(corridorMid, 6f, corridorStart - 0.3f), Quaternion.identity, 0.07f);
            Label("Hold Space to run; release + press to jump off", parent, new Vector3(corridorMid, 5.3f, corridorStart - 0.3f), Quaternion.identity, 0.035f);

            // Slide hill: walk up the 12 degree ramp, then slide down it to pick up speed.
            Ramp("Slide Hill", parent, new Vector3(-8f, 0f, 29f), 5f, 3f, 12f, endAtZ: false);
            Label("SLIDE HILL 12°", parent, new Vector3(-8f, 0.04f, 27.5f), Quaternion.Euler(90f, 0f, 0f), 0.08f);

            // Long gap: needs a slide jump (and some air strafing helps).
            const float gapZ = 44f, takeoffEnd = 32f, gap = 9f;
            BoxOnFloor("Long Gap Takeoff", parent, takeoffEnd - 3f, gapZ, new Vector3(6f, 1f, 4f), mats.Obstacle);
            BoxOnFloor("Long Gap Landing", parent, takeoffEnd + gap + 2.5f, gapZ, new Vector3(5f, 1f, 4f), mats.Obstacle);
            Label($"LONG GAP {gap:0} m", parent, new Vector3(takeoffEnd + gap / 2, 3f, gapZ - 2.2f), Quaternion.identity, 0.07f);
            Label("slide-jump + air strafe", parent, new Vector3(takeoffEnd + gap / 2, 2.4f, gapZ - 2.2f), Quaternion.identity, 0.035f);
        }

        static void BuildCrazyTech(Transform parent)
        {
            // Lasso beams 13 m up, spanning the whole arena - swing from anywhere.
            const float beamHeight = 13f, beamSize = 0.5f, edge = ArenaHalfSize - 0.6f;
            float[] beamZs = { -15f, 8f };
            foreach (float z in beamZs)
            {
                Box($"Lasso Beam z{z}", parent, new Vector3(0f, beamHeight, z), new Vector3(edge * 2f, beamSize, beamSize), mats.Wall);
                BoxOnFloor("Beam Pillar", parent, -edge, z, new Vector3(0.8f, beamHeight, 0.8f), mats.Wall);
                BoxOnFloor("Beam Pillar", parent, edge, z, new Vector3(0.8f, beamHeight, 0.8f), mats.Wall);
            }
            const float beamX = 25f;
            Box("Lasso Beam x25", parent, new Vector3(beamX, beamHeight, 0f), new Vector3(beamSize, beamSize, edge * 2f), mats.Wall);
            BoxOnFloor("Beam Pillar", parent, beamX, -edge, new Vector3(0.8f, beamHeight, 0.8f), mats.Wall);
            BoxOnFloor("Beam Pillar", parent, beamX, edge, new Vector3(0.8f, beamHeight, 0.8f), mats.Wall);
            Label("LASSO BEAMS 13 m  -  hold Q to swing", parent, new Vector3(0f, beamHeight - 1f, -15.3f), Quaternion.identity, 0.07f);

            // Roll / slam tower: lasso up, then jump off. Roll (Ctrl before landing) or slam (Ctrl high up).
            const float towerX = -10f, towerZ = 5f, towerHeight = 8f;
            BoxOnFloor("Roll Tower 8m", parent, towerX, towerZ, new Vector3(4f, towerHeight, 4f), mats.Wall);
            Label("ROLL / SLAM TOWER 8 m", parent, new Vector3(towerX, towerHeight + 1.2f, towerZ - 2f), Quaternion.identity, 0.06f);
            Label("lasso up (Q)", parent, new Vector3(towerX, 3f, towerZ - 2.05f), Quaternion.identity, 0.05f);

            // Slam pad with dummies and crates around it to blow away.
            const float padX = -10f, padZ = 11f;
            Box("Slam Pad", parent, new Vector3(padX, 0.015f, padZ), new Vector3(3f, 0.03f, 3f), mats.Accent);
            Label("SLAM PAD", parent, new Vector3(padX, 0.05f, padZ), Quaternion.Euler(90f, 0f, 0f), 0.08f);
            Dummy(parent, new Vector3(padX, 0f, padZ + 3.5f));
            Dummy(parent, new Vector3(padX - 3.5f, 0f, padZ));
            Dummy(parent, new Vector3(padX + 3.5f, 0f, padZ));
            foreach (var pos in new[] { new Vector2(padX - 2.5f, padZ + 5f), new Vector2(padX + 2.5f, padZ + 5f), new Vector2(padX - 4f, padZ + 3f) })
            {
                var crate = BoxOnFloor("Physics Crate", parent, pos.x, pos.y, Vector3.one, mats.Obstacle);
                crate.AddComponent<Rigidbody>().mass = 20f;
            }
        }

        static void PlacePrototypeMap(Transform parent)
        {
            var guids = AssetDatabase.FindAssets("PrototypeMap t:Prefab", new[] { "Assets" });
            GameObject prefab = null;
            foreach (var guid in guids)
            {
                var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (candidate != null && candidate.name == "PrototypeMap") { prefab = candidate; break; }
            }
            if (prefab == null)
            {
                Debug.Log("[TestArenaBuilder] PrototypeMap prefab not found - skipping.");
                return;
            }

            var map = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            map.transform.localPosition = Vector3.zero;

            foreach (var filter in map.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.GetComponent<Collider>() == null)
                    filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }

            var renderers = map.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            // Line it up north of the arena, centred on the doorway, with its lowest point on the arena floor.
            float gap = 2f;
            Vector3 offset = new Vector3(-bounds.center.x, -bounds.min.y, ArenaHalfSize + 1f + gap - bounds.min.z);
            map.transform.position += offset;
            Debug.Log($"[TestArenaBuilder] Prototype Map size {bounds.size}, placed with offset {offset}");

            // Walkway across the gap between the doorway and the map.
            Box("Walkway", parent, new Vector3(0f, -0.5f, ArenaHalfSize + 1f + gap / 2), new Vector3(8f, 1f, gap + 2f), mats.Floor);
        }

        // ------------------------------------------------------------------ player, HUD, targets

        static GameObject BuildPlayer(out PlayerMotor motor, out Health health, out Revolver revolver)
        {
            var player = new GameObject("Player") { layer = IgnoreRaycastLayer };
            player.transform.SetPositionAndRotation(SpawnPosition, Quaternion.identity);

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.4f;
            controller.slopeLimit = 45f;
            controller.skinWidth = 0.04f;
            controller.minMoveDistance = 0f;

            player.AddComponent<PlayerInputReader>();
            health = player.AddComponent<Health>();

            var cameraRoot = new GameObject("CameraRoot").transform;
            cameraRoot.SetParent(player.transform, false);
            cameraRoot.localPosition = new Vector3(0f, 1.65f, 0f);

            // CameraRoot (crouch height) > HeadBob (bob/tilt/landing) > Main Camera (look pitch)
            var headBobGo = new GameObject("HeadBob");
            headBobGo.transform.SetParent(cameraRoot, false);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraGo.transform.SetParent(headBobGo.transform, false);
            var camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 80f;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 500f;
            cameraGo.AddComponent<AudioListener>();

            motor = player.AddComponent<PlayerMotor>();
            SetField(motor, "cameraRoot", cameraRoot);

            var look = player.AddComponent<PlayerLook>();
            SetField(look, "cameraPivot", cameraGo.transform);

            player.AddComponent<PlayerRespawn>();

            var headBob = headBobGo.AddComponent<HeadBob>();
            SetField(headBob, "motor", motor);
            SetField(headBob, "targetCamera", camera);

            // Main Camera > WeaponHolder (sway/bob/poses) > Revolver (recoil/reload on its model)
            var holder = new GameObject("WeaponHolder") { layer = IgnoreRaycastLayer };
            holder.transform.SetParent(cameraGo.transform, false);

            revolver = BuildRevolver(holder.transform, camera);

            var sway = holder.AddComponent<WeaponSway>();
            SetField(sway, "motor", motor);
            SetField(sway, "headBob", headBob);

            // Lasso: the rope leaves from a point at the bottom-left of the view.
            var lassoHand = new GameObject("Lasso Hand") { layer = IgnoreRaycastLayer }.transform;
            lassoHand.SetParent(cameraGo.transform, false);
            lassoHand.localPosition = new Vector3(-0.3f, -0.3f, 0.5f);

            var ropeGo = new GameObject("Lasso Rope") { layer = IgnoreRaycastLayer };
            ropeGo.transform.SetParent(player.transform, false);
            var rope = ropeGo.AddComponent<LineRenderer>();
            rope.useWorldSpace = true;
            rope.positionCount = 2;
            rope.startWidth = 0.035f;
            rope.endWidth = 0.025f;
            rope.sharedMaterial = mats.Rope;
            rope.shadowCastingMode = ShadowCastingMode.Off;
            rope.enabled = false;

            SetField(motor, "aimTransform", cameraGo.transform);
            SetField(motor, "lassoRope", rope);
            SetField(motor, "lassoHand", lassoHand);
            return player;
        }

        static Revolver BuildRevolver(Transform holder, Camera camera)
        {
            var gun = new GameObject("Revolver") { layer = IgnoreRaycastLayer };
            gun.transform.SetParent(holder, false);
            gun.transform.localPosition = new Vector3(0.22f, -0.2f, 0.42f);

            var model = new GameObject("Model (placeholder)") { layer = IgnoreRaycastLayer }.transform;
            model.SetParent(gun.transform, false);

            GunPart(model, PrimitiveType.Cube, "Barrel", new Vector3(0f, 0.025f, 0.16f), new Vector3(0.04f, 0.04f, 0.3f), Vector3.zero, mats.GunMetal);
            GunPart(model, PrimitiveType.Cube, "Frame", new Vector3(0f, 0f, 0f), new Vector3(0.055f, 0.09f, 0.14f), Vector3.zero, mats.GunMetal);
            GunPart(model, PrimitiveType.Cylinder, "Cylinder", new Vector3(0f, 0.012f, 0.035f), new Vector3(0.085f, 0.045f, 0.085f), new Vector3(90f, 0f, 0f), mats.GunMetal);
            GunPart(model, PrimitiveType.Cube, "Grip", new Vector3(0f, -0.09f, -0.07f), new Vector3(0.048f, 0.14f, 0.06f), new Vector3(-20f, 0f, 0f), mats.GunWood);
            GunPart(model, PrimitiveType.Cube, "Hammer", new Vector3(0f, 0.055f, -0.07f), new Vector3(0.018f, 0.03f, 0.03f), new Vector3(-30f, 0f, 0f), mats.GunMetal);

            var muzzle = new GameObject("Muzzle") { layer = IgnoreRaycastLayer }.transform;
            muzzle.SetParent(model, false);
            muzzle.localPosition = new Vector3(0f, 0.025f, 0.32f);

            var flash = muzzle.gameObject.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.range = 5f;
            flash.intensity = 4f;
            flash.color = new Color(1f, 0.75f, 0.4f);
            flash.shadows = LightShadows.None;
            flash.enabled = false;

            var revolver = gun.AddComponent<Revolver>();
            SetField(revolver, "aimCamera", camera);
            SetField(revolver, "model", model);
            SetField(revolver, "muzzle", muzzle);
            SetField(revolver, "muzzleFlash", flash);
            SetField(revolver, "tracerMaterial", mats.Tracer);
            SetField(revolver, "impactMaterial", mats.Impact);
            return revolver;
        }

        static void GunPart(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Vector3 euler, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.layer = IgnoreRaycastLayer;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(euler);
            part.transform.localScale = scale;
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        static void BuildHud(PlayerMotor motor, Health health, Revolver revolver)
        {
            var hud = new GameObject("HUD").AddComponent<DebugHUD>();
            SetField(hud, "motor", motor);
            SetField(hud, "revolver", revolver);
            SetField(hud, "playerHealth", health);
        }

        static void Dummy(Transform parent, Vector3 position, Vector3 movingTravel = default, float movingSpeed = 0f)
        {
            var root = new GameObject("Target Dummy");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var health = root.AddComponent<Health>();
            root.AddComponent<TargetDummy>();

            var post = Box("Post", root.transform, new Vector3(0f, 0.35f, 0f), new Vector3(0.12f, 0.7f, 0.12f), mats.GunMetal);
            Object.DestroyImmediate(post.GetComponent<Collider>()); // bullets pass the post

            var body = Box("Body", root.transform, new Vector3(0f, 1.1f, 0f), new Vector3(0.6f, 0.8f, 0.3f), mats.Target);
            body.AddComponent<Hitbox>().Configure(health, 1f);

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.72f, 0f);
            head.transform.localScale = Vector3.one * 0.34f;
            head.GetComponent<Renderer>().sharedMaterial = mats.TargetHead;
            head.AddComponent<Hitbox>().Configure(health, 2f);

            if (movingSpeed > 0f)
                root.AddComponent<MovingTarget>().Configure(movingTravel, movingSpeed);
        }

        // ------------------------------------------------------------------ helpers

        static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        static GameObject BoxOnFloor(string name, Transform parent, float x, float z, Vector3 size, Material material, float baseY = 0f)
        {
            return Box(name, parent, new Vector3(x, baseY + size.y / 2, z), size, material);
        }

        /// <summary>
        /// A ramp rising along +Z to <paramref name="height"/> at <paramref name="angle"/> degrees.
        /// If endAtZ, <paramref name="anchor"/> is where the ramp reaches the top; otherwise where it starts.
        /// When starting at the anchor, a 4 m deep platform is added at the top.
        /// </summary>
        static void Ramp(string name, Transform parent, Vector3 anchor, float width, float height, float angle, bool endAtZ)
        {
            const float thickness = 0.4f;
            float rad = angle * Mathf.Deg2Rad;
            float length = height / Mathf.Sin(rad);
            float run = height / Mathf.Tan(rad);
            Vector3 start = endAtZ ? anchor - new Vector3(0f, 0f, run) : anchor;

            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = start;
            pivot.localRotation = Quaternion.Euler(-angle, 0f, 0f);
            Box("Surface", pivot, new Vector3(0f, -thickness / 2, length / 2), new Vector3(width, thickness, length), mats.Obstacle);

            if (!endAtZ)
                BoxOnFloor($"{name} Top", parent, start.x, start.z + run + 2f, new Vector3(width, height, 4f), mats.Wall);
        }

        static void Label(string text, Transform parent, Vector3 position, Quaternion rotation, float size)
        {
            var go = new GameObject($"Label {text}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = labelFont;
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.characterSize = size;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Color.white;
            go.GetComponent<MeshRenderer>().sharedMaterial = labelFont.material;
        }

        static void SetField(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[TestArenaBuilder] {target.GetType().Name} has no serialized field '{fieldName}'");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
