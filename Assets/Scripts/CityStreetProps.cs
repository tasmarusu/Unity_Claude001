using System.Collections.Generic;
using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// 奥の道路の両脇に街路樹と街灯を並べる(中景のレイヤー)。全て静的なので1枚のメッシュに結合して1ドローコールにする。
    /// ルール: 同じ木が連続しない / 街灯は等間隔で腕が道路側を向く / 歩行者の通り道(路肩の内側)と横断歩道の手前には置かない。
    /// </summary>
    public static class CityStreetProps
    {
        private const float StartMargin = 14f;
        private const float EndMargin = 8f;
        private const float Spacing = 8f;
        private const int LampEvery = 3;
        private const float EdgeInset = 0.55f;

        private class Buffer
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();
        }

        public static void Build(CityLife.RoadInfo road, System.Random rng, Transform parent)
        {
            GameObject[] all = CityLife.LoadPrefabs("Props");
            List<GameObject> trees = new List<GameObject>();
            GameObject lamp = null;
            foreach (GameObject prefab in all)
            {
                if (prefab.name.StartsWith("Tree"))
                {
                    trees.Add(prefab);
                }
                else if (prefab.name.StartsWith("Prop_Lamp"))
                {
                    lamp = prefab;
                }
            }
            if (trees.Count == 0 && lamp == null)
            {
                return;
            }

            Dictionary<Material, Buffer> buffers = new Dictionary<Material, Buffer>();
            int lastTree = -1;

            for (int side = -1; side <= 1; side += 2)
            {
                float x = road.CenterX + side * (road.HalfWidth - EdgeInset);
                int slot = 0;
                for (float z = road.ZStart + StartMargin; z < road.ZEnd - EndMargin; z += Spacing, slot++)
                {
                    float jitterZ = ((float)rng.NextDouble() - 0.5f) * 1.2f;
                    Vector3 position = new Vector3(x + ((float)rng.NextDouble() - 0.5f) * 0.2f, road.SurfaceY, z + jitterZ);

                    bool useLamp = lamp != null && (slot + (side > 0 ? 1 : 0)) % LampEvery == 0;
                    if (useLamp)
                    {
                        // 腕は+X方向。道路側を向くよう、右側(+x)の街灯は180度回す
                        Quaternion rotation = Quaternion.Euler(0f, side > 0 ? 180f : 0f, 0f);
                        AddProp(buffers, lamp, position, rotation, 1f);
                    }
                    else if (trees.Count > 0)
                    {
                        int pick = rng.Next(trees.Count);
                        if (trees.Count > 1 && pick == lastTree)
                        {
                            pick = (pick + 1 + rng.Next(trees.Count - 1)) % trees.Count;
                        }
                        lastTree = pick;
                        Quaternion rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                        float scale = Mathf.Lerp(0.9f, 1.15f, (float)rng.NextDouble());
                        AddProp(buffers, trees[pick], position, rotation, scale);
                    }
                }
            }

            GameObject root = new GameObject("CityStreetProps");
            root.transform.SetParent(parent, false);
            foreach (KeyValuePair<Material, Buffer> pair in buffers)
            {
                Mesh mesh = new Mesh { name = "StreetPropsCombined" };
                mesh.SetVertices(pair.Value.Vertices);
                mesh.SetNormals(pair.Value.Normals);
                mesh.SetUVs(0, pair.Value.Uvs);
                mesh.SetTriangles(pair.Value.Triangles, 0);
                mesh.RecalculateBounds();

                GameObject go = new GameObject("Props_" + pair.Key.name);
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = pair.Key;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;
            }
        }

        private static void AddProp(Dictionary<Material, Buffer> buffers, GameObject prefab, Vector3 position, Quaternion rotation, float scale)
        {
            MeshFilter filter = prefab.GetComponentInChildren<MeshFilter>();
            MeshRenderer renderer = prefab.GetComponentInChildren<MeshRenderer>();
            if (filter == null || renderer == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable)
            {
                return;
            }

            Material material = renderer.sharedMaterial;
            Buffer buffer;
            if (!buffers.TryGetValue(material, out buffer))
            {
                buffer = new Buffer();
                buffers[material] = buffer;
            }

            Mesh mesh = filter.sharedMesh;
            Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, filter.transform.lossyScale * scale);
            Matrix4x4 normalMatrix = matrix.inverse.transpose;
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uvs = mesh.uv;
            int[] triangles = mesh.triangles;
            int baseIndex = buffer.Vertices.Count;

            for (int i = 0; i < vertices.Length; i++)
            {
                buffer.Vertices.Add(matrix.MultiplyPoint3x4(vertices[i]));
                buffer.Normals.Add(normalMatrix.MultiplyVector(normals.Length > i ? normals[i] : Vector3.up).normalized);
                buffer.Uvs.Add(uvs.Length > i ? uvs[i] : Vector2.zero);
            }
            for (int i = 0; i < triangles.Length; i++)
            {
                buffer.Triangles.Add(baseIndex + triangles[i]);
            }
        }
    }
}
