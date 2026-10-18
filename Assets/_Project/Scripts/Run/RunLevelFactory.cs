using UnityEngine;

namespace SandHouse.Run
{
    /// <summary>Three distinct greybox combat layouts; geometry is rebuilt only between encounters.</summary>
    public static class RunLevelFactory
    {
        public static string LevelName(int index) => index == 0 ? "The Courtyard" : index == 1 ? "Crossfire Hall" : "The Crucible";

        public static void Build(Transform root, int index, Material floor, Material wall, Material cover, Material accent)
        {
            Box(root, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(34f, 1f, 38f), floor);
            Box(root, "West wall", new Vector3(-17f, 4f, 0f), new Vector3(1f, 8f, 38f), wall);
            Box(root, "East wall", new Vector3(17f, 4f, 0f), new Vector3(1f, 8f, 38f), wall);
            Box(root, "Entrance", new Vector3(0f, 4f, -19f), new Vector3(34f, 8f, 1f), wall);
            Box(root, "Far wall", new Vector3(0f, 4f, 19f), new Vector3(34f, 8f, 1f), wall);
            Box(root, "Entrance pad", new Vector3(0f, 0.015f, -13f), new Vector3(5f, 0.03f, 4f), accent);
            // Long side walls support wall runs; overhead bars support lasso movement.
            Box(root, "Lasso beam", new Vector3(0f, 7f, 0f), new Vector3(33f, 0.4f, 0.4f), accent);
            if (index == 0)
            {
                Box(root, "Low cover left", new Vector3(-6f, 0.6f, 0f), new Vector3(5f, 1.2f, 2f), cover);
                Box(root, "Low cover right", new Vector3(6f, 0.6f, 4f), new Vector3(5f, 1.2f, 2f), cover);
                Box(root, "Center column", new Vector3(0f, 2f, 8f), new Vector3(2f, 4f, 2f), wall);
            }
            else if (index == 1)
            {
                foreach (float x in new[] { -7f, 7f })
                    foreach (float z in new[] { -3f, 7f })
                        Box(root, "Crossfire pillar", new Vector3(x, 2.5f, z), new Vector3(2f, 5f, 2f), wall);
                Box(root, "Central cover", new Vector3(0f, 0.6f, 3f), new Vector3(6f, 1.2f, 2f), cover);
            }
            else
            {
                foreach (float x in new[] { -9f, 9f })
                {
                    Box(root, "Climb wall", new Vector3(x, 1.8f, 4f), new Vector3(1f, 3.6f, 9f), wall);
                    Box(root, "Low cover", new Vector3(x, 0.65f, -5f), new Vector3(4f, 1.3f, 2f), cover);
                }
                Box(root, "Central pillar", new Vector3(0f, 2.5f, 6f), new Vector3(3f, 5f, 3f), cover);
                Box(root, "Second beam", new Vector3(0f, 7f, 11f), new Vector3(33f, 0.4f, 0.4f), accent);
            }
        }

        public static Vector3[] EnemySpawns(int index)
        {
            if (index == 0) return new[] { new Vector3(-9f, 0.05f, 6f), new Vector3(9f, 0.05f, 9f), new Vector3(-4f, 0.05f, 13f), new Vector3(5f, 0.05f, 15f) };
            if (index == 1) return new[] { new Vector3(-12f, 0.05f, 3f), new Vector3(12f, 0.05f, 4f), new Vector3(-4f, 0.05f, 9f), new Vector3(4f, 0.05f, 12f), new Vector3(-11f, 0.05f, 15f), new Vector3(11f, 0.05f, 15f) };
            return new[] { new Vector3(-13f, 0.05f, 1f), new Vector3(13f, 0.05f, 1f), new Vector3(-5f, 0.05f, 7f), new Vector3(5f, 0.05f, 7f), new Vector3(-13f, 0.05f, 14f), new Vector3(13f, 0.05f, 14f), new Vector3(-4f, 0.05f, 16f), new Vector3(5f, 0.05f, 16f) };
        }

        static void Box(Transform root, string name, Vector3 position, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name; box.transform.SetParent(root, false);
            box.transform.localPosition = position; box.transform.localScale = size;
            if (material != null) box.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
