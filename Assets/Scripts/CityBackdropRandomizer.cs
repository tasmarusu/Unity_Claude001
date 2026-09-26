using System.Collections.Generic;
using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// 背景ビル(シーン上の静的なCityBackdrop)を、毎回違う街並みに組み直す。
    /// 配置枠(位置・幅・奥行き)はシーンの設計をそのまま使う=道路や主役のタワーに被らないことが保証される。
    /// 変えるのは「外壁の種類」と「高さ」だけで、次のルールで自然さを保つ:
    ///  - 近い枠(隣り合う数棟)が同じ外壁にならない
    ///  - 手前は控えめ、奥ほど高さのばらつきを大きく(遠景のスカイライン)
    ///  - 一部を低層/超高層にして階層感を出す。ただし道路沿いの手前側は極端にしない
    /// 元のビルは無効化し、外壁ごとにメッシュを結合して描画する(ドローコールを増やさない)。
    /// </summary>
    public static class CityBackdropRandomizer
    {
        private struct Slot
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public Material Material;
        }

        private class MeshBuffer
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();
        }

        private const float NeighborRadius = 16f;
        private const int NeighborMemory = 6;
        private const float MinHeight = 6f;
        private const float MaxHeight = 60f;

        public static void Rebuild(GameObject backdrop, System.Random rng)
        {
            MeshRenderer[] renderers = backdrop.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            BuildCube();
            List<Slot> slots = new List<Slot>();
            List<Material> materials = new List<Material>();
            foreach (MeshRenderer r in renderers)
            {
                if (r.sharedMaterial == null)
                {
                    continue;
                }

                slots.Add(new Slot
                {
                    Position = r.transform.position,
                    Rotation = r.transform.rotation,
                    Scale = r.transform.lossyScale,
                    Material = r.sharedMaterial
                });
                if (!materials.Contains(r.sharedMaterial))
                {
                    materials.Add(r.sharedMaterial);
                }
            }

            int[] order = new int[slots.Count];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }
            System.Array.Sort(order, (a, b) =>
            {
                int byZ = slots[a].Position.z.CompareTo(slots[b].Position.z);
                return byZ != 0 ? byZ : slots[a].Position.x.CompareTo(slots[b].Position.x);
            });

            Material[] assigned = new Material[slots.Count];
            Dictionary<Material, MeshBuffer> buffers = new Dictionary<Material, MeshBuffer>();
            List<KeyValuePair<Material, KeyValuePair<Vector3, MeshBuffer>>> individuals =
                new List<KeyValuePair<Material, KeyValuePair<Vector3, MeshBuffer>>>();

            foreach (int index in order)
            {
                Slot slot = slots[index];
                Material chosen = PickMaterial(index, order, slots, assigned, materials, rng);
                assigned[index] = chosen;

                float heightFactor = PickHeightFactor(slot, rng);
                float newHeight = Mathf.Clamp(slot.Scale.y * heightFactor, MinHeight, MaxHeight);
                float baseY = slot.Position.y - slot.Scale.y * 0.5f;

                MeshBuffer buffer;
                if (!buffers.TryGetValue(chosen, out buffer))
                {
                    buffer = new MeshBuffer();
                    buffers[chosen] = buffer;
                }

                Vector3 position = new Vector3(slot.Position.x, baseY + newHeight * 0.5f, slot.Position.z);
                Vector3 scale = new Vector3(slot.Scale.x, newHeight, slot.Scale.z);
                Append(buffer, Matrix4x4.TRS(position, slot.Rotation, scale), newHeight / slot.Scale.y);

                // クリア演出用の1棟ずつの版(足元が原点)。普段は使わず、お祝い中だけ切り替える
                MeshBuffer own = new MeshBuffer();
                Append(own, Matrix4x4.TRS(new Vector3(0f, newHeight * 0.5f, 0f), slot.Rotation, scale), newHeight / slot.Scale.y);
                individuals.Add(new KeyValuePair<Material, KeyValuePair<Vector3, MeshBuffer>>(
                    chosen, new KeyValuePair<Vector3, MeshBuffer>(new Vector3(slot.Position.x, baseY, slot.Position.z), own)));
            }

            GameObject root = new GameObject("CityBackdropRuntime");
            int groupIndex = 0;
            foreach (KeyValuePair<Material, MeshBuffer> pair in buffers)
            {
                Mesh mesh = new Mesh { name = "BackdropCombined_" + pair.Key.name };
                mesh.SetVertices(pair.Value.Vertices);
                mesh.SetNormals(pair.Value.Normals);
                mesh.SetUVs(0, pair.Value.Uvs);
                mesh.SetTriangles(pair.Value.Triangles, 0);
                mesh.RecalculateBounds();

                GameObject go = new GameObject("Backdrop_" + pair.Key.name);
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = pair.Key;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;
                groupIndex++;
            }

            // クリア時はビルが1棟ずつチンアナゴになる(個別のメッシュは、初めてクリアしたときに作る)
            CityCelebration.RegisterEelBackdrop(root, () => BuildEels(individuals));

            backdrop.SetActive(false);
        }

        private static GameObject BuildEels(List<KeyValuePair<Material, KeyValuePair<Vector3, MeshBuffer>>> individuals)
        {
            GameObject eelRoot = new GameObject("CityBackdropEels");
            foreach (KeyValuePair<Material, KeyValuePair<Vector3, MeshBuffer>> item in individuals)
            {
                MeshBuffer own = item.Value.Value;
                Mesh mesh = new Mesh { name = "BackdropEel" };
                mesh.SetVertices(own.Vertices);
                mesh.SetNormals(own.Normals);
                mesh.SetUVs(0, own.Uvs);
                mesh.SetTriangles(own.Triangles, 0);
                mesh.RecalculateBounds();

                GameObject go = new GameObject("Eel");
                go.transform.SetParent(eelRoot.transform, false);
                go.transform.position = item.Value.Key;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = item.Key;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;
            }
            return eelRoot;
        }

        private static Material PickMaterial(int index, int[] order, List<Slot> slots, Material[] assigned,
            List<Material> materials, System.Random rng)
        {
            // 近くの割り当て済みビルの外壁を避けて選ぶ(同じ外壁が並ばない)
            List<Material> banned = new List<Material>();
            Vector3 p = slots[index].Position;
            int found = 0;
            for (int k = 0; k < order.Length && found < NeighborMemory; k++)
            {
                int other = order[k];
                if (other == index || assigned[other] == null)
                {
                    continue;
                }
                Vector3 d = slots[other].Position - p;
                d.y = 0f;
                if (d.magnitude <= NeighborRadius)
                {
                    banned.Add(assigned[other]);
                    found++;
                }
            }

            List<Material> candidates = new List<Material>();
            foreach (Material m in materials)
            {
                if (!banned.Contains(m))
                {
                    candidates.Add(m);
                }
            }
            if (candidates.Count == 0)
            {
                candidates = materials;
            }
            return candidates[rng.Next(candidates.Count)];
        }

        private static float PickHeightFactor(Slot slot, System.Random rng)
        {
            float z = slot.Position.z;
            bool roadside = Mathf.Abs(slot.Position.x) < 16f && z < 30f;

            float min;
            float max;
            if (z < 45f)
            {
                min = 0.75f;
                max = 1.1f;
            }
            else if (z < 100f)
            {
                min = 0.8f;
                max = 1.3f;
            }
            else
            {
                min = 0.9f;
                max = 1.45f;
            }

            float factor = Mathf.Lerp(min, max, (float)rng.NextDouble());
            if (roadside)
            {
                return factor;
            }

            double roll = rng.NextDouble();
            if (roll < 0.12)
            {
                factor *= 0.5f;
            }
            else if (roll > 0.94 && z > 60f)
            {
                factor *= 1.5f;
            }
            return factor;
        }

        private static Vector3[] cubeVertices;
        private static Vector3[] cubeNormals;
        private static Vector2[] cubeUvs;
        private static int[] cubeTriangles;

        /// <summary>
        /// 単位キューブを自前で生成する(組み込みのCubeメッシュはスクリプトから読めないため)。
        /// 側面はU=横・V=縦。三角形はUnityの表面判定(Cross(b-a, c-a)が外向き)に合わせて並べる。
        /// </summary>
        private static void BuildCube()
        {
            if (cubeVertices != null)
            {
                return;
            }

            // {法線, U軸, V軸}。すべて U x V = 法線
            Vector3[][] faces =
            {
                new[] { new Vector3(1, 0, 0), new Vector3(0, 0, -1), new Vector3(0, 1, 0) },
                new[] { new Vector3(-1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 1, 0) },
                new[] { new Vector3(0, 0, 1), new Vector3(1, 0, 0), new Vector3(0, 1, 0) },
                new[] { new Vector3(0, 0, -1), new Vector3(-1, 0, 0), new Vector3(0, 1, 0) },
                new[] { new Vector3(0, 1, 0), new Vector3(0, 0, 1), new Vector3(1, 0, 0) },
                new[] { new Vector3(0, -1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1) }
            };

            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            foreach (Vector3[] f in faces)
            {
                Vector3 n = f[0];
                Vector3 u = f[1];
                Vector3 v = f[2];
                Vector3 center = n * 0.5f;
                int start = vertices.Count;
                vertices.Add(center - u * 0.5f - v * 0.5f);
                vertices.Add(center + u * 0.5f - v * 0.5f);
                vertices.Add(center + u * 0.5f + v * 0.5f);
                vertices.Add(center - u * 0.5f + v * 0.5f);
                for (int i = 0; i < 4; i++)
                {
                    normals.Add(n);
                }
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(0f, 1f));
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            cubeVertices = vertices.ToArray();
            cubeNormals = normals.ToArray();
            cubeUvs = uvs.ToArray();
            cubeTriangles = triangles.ToArray();
        }

        /// <summary>
        /// キューブを変換して結合バッファへ追加する。側面のUV縦を高さの変化に合わせて伸縮し、窓の大きさを保つ。
        /// </summary>
        private static void Append(MeshBuffer buffer, Matrix4x4 matrix, float uvScaleY)
        {
            int baseIndex = buffer.Vertices.Count;
            Matrix4x4 normalMatrix = matrix.inverse.transpose;

            for (int i = 0; i < cubeVertices.Length; i++)
            {
                buffer.Vertices.Add(matrix.MultiplyPoint3x4(cubeVertices[i]));
                buffer.Normals.Add(normalMatrix.MultiplyVector(cubeNormals[i]).normalized);

                Vector2 uv = cubeUvs[i];
                if (Mathf.Abs(cubeNormals[i].y) < 0.5f)
                {
                    uv.y *= uvScaleY;
                }
                buffer.Uvs.Add(uv);
            }
            for (int i = 0; i < cubeTriangles.Length; i++)
            {
                buffer.Triangles.Add(baseIndex + cubeTriangles[i]);
            }
        }
    }
}
