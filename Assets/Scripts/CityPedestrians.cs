using System.Collections.Generic;
using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// 街を歩く歩行者。複数の「流れ」を同じプールから出す。
    ///  - 奥の路肩: 路肩を歩いて横断歩道で道路を渡る
    ///  - 手前の広場: タワーの手前を左右に横切る(ゆっくり)
    ///  - 手前の横断歩道: 手前の道路を渡ってカメラ側へ歩いてくる/広場へ戻っていく
    /// 人にはColliderが無く、タップ判定や崩落に影響しない。経路の両端ではスケールを絞って出入りを目立たなくする。
    /// </summary>
    public class CityPedestrians : MonoBehaviour
    {
        private class Walker
        {
            public GameObject Object;
            public Transform Transform;
            public PolylinePath Path;
            public float S;
            public float Speed;
            public float Phase;
            public float LateralOffset;
            public float BaseHeightScale = 1f;
        }

        private class Group
        {
            public readonly List<PolylinePath> Paths = new List<PolylinePath>();
            public readonly List<Walker> Active = new List<Walker>();
            public int MaxActive;
            public float SpeedMin;
            public float SpeedMax;
            public float IntervalMin;
            public float IntervalMax;
            public float LateralRange;
            public float NextSpawn;
        }

        private const int PoolPerType = 4;
        private const float ShoulderInset = 1.7f;
        private const float WalkDepth = 82f;
        private const float EdgeFade = 3f;
        private const float StartClearance = 3f;

        private readonly List<Walker> pool = new List<Walker>();
        private readonly List<Group> groups = new List<Group>();
        private System.Random rng;

        public void Init(CityLife.RoadInfo road, CityLife.FrontInfo front, System.Random random)
        {
            rng = random;

            GameObject[] prefabs = CityLife.LoadPrefabs("People");
            if (prefabs.Length == 0)
            {
                enabled = false;
                return;
            }

            BuildPool(prefabs);

            Group deep = BuildDeepGroup(road);
            groups.Add(deep);
            PopulateInitial(deep, deep.MaxActive / 2);

            if (front.Valid)
            {
                Group plaza = BuildPlazaGroup(front);
                Group crosswalk = BuildFrontCrosswalkGroup(front);
                groups.Add(plaza);
                groups.Add(crosswalk);
                PopulateInitial(plaza, 1);
                PopulateInitial(crosswalk, 1);
            }

            foreach (Group g in groups)
            {
                g.NextSpawn = Time.time + Range(g.IntervalMin, g.IntervalMax) * 0.5f;
            }
        }

        private void BuildPool(GameObject[] prefabs)
        {
            GameObject root = new GameObject("CityPeople");
            root.transform.SetParent(transform, false);

            foreach (GameObject prefab in prefabs)
            {
                for (int i = 0; i < PoolPerType; i++)
                {
                    GameObject go = Instantiate(prefab, root.transform);
                    go.name = prefab.name;
                    foreach (Collider c in go.GetComponentsInChildren<Collider>())
                    {
                        Destroy(c);
                    }
                    go.SetActive(false);
                    pool.Add(new Walker { Object = go, Transform = go.transform, BaseHeightScale = go.transform.localScale.y });
                }
            }
        }

        /// <summary>
        /// 左右の路肩どちらかから来て、横断歩道で反対側へ渡り、その路肩を奥へ戻る経路(左右対称の2本)。
        /// </summary>
        private Group BuildDeepGroup(CityLife.RoadInfo road)
        {
            Group g = new Group { MaxActive = 8, SpeedMin = 0.65f, SpeedMax = 0.95f, IntervalMin = 3f, IntervalMax = 9f, LateralRange = 0.35f };
            float y = road.SurfaceY;
            float zFar = Mathf.Min(road.ZEnd - 10f, road.ZStart + WalkDepth);
            float zCross = road.CrosswalkZ;
            float x = road.HalfWidth - ShoulderInset;

            for (int side = -1; side <= 1; side += 2)
            {
                float a = road.CenterX + side * x;
                float b = road.CenterX - side * x;
                g.Paths.Add(new PolylinePath(new[]
                {
                    new Vector3(a, y, zFar),
                    new Vector3(a, y, zCross),
                    new Vector3(b, y, zCross),
                    new Vector3(b, y, zFar)
                }));
            }
            return g;
        }

        /// <summary>
        /// タワーの手前(道路との間の広場)を左右に横切る。
        /// </summary>
        private Group BuildPlazaGroup(CityLife.FrontInfo front)
        {
            Group g = new Group { MaxActive = 3, SpeedMin = 0.6f, SpeedMax = 0.9f, IntervalMin = 5f, IntervalMax = 11f, LateralRange = 0.25f };
            float z = front.PlazaZ;
            float y = front.GroundY;
            float half = front.PlazaHalfWidth;
            g.Paths.Add(new PolylinePath(new[] { new Vector3(-half, y, z), new Vector3(half, y, z) }));
            g.Paths.Add(new PolylinePath(new[] { new Vector3(half, y, z), new Vector3(-half, y, z) }));
            return g;
        }

        /// <summary>
        /// 手前の横断歩道を、広場からカメラ側へ渡ってくる/戻っていく2本。
        /// </summary>
        private Group BuildFrontCrosswalkGroup(CityLife.FrontInfo front)
        {
            Group g = new Group { MaxActive = 2, SpeedMin = 0.75f, SpeedMax = 1f, IntervalMin = 7f, IntervalMax = 14f, LateralRange = 0.2f };
            float zStart = front.PlazaZ - 0.6f;
            float zEnd = front.RoadCenterZ - 1.5f;
            float yRoad = front.RoadSurfaceY;
            float xa = front.CrosswalkX - 1.4f;
            float xb = front.CrosswalkX + 1.4f;

            g.Paths.Add(new PolylinePath(new[]
            {
                new Vector3(xa, front.GroundY, zStart),
                new Vector3(xa, yRoad, zStart - 1.5f),
                new Vector3(xa, yRoad, zEnd)
            }));
            g.Paths.Add(new PolylinePath(new[]
            {
                new Vector3(xb, yRoad, zEnd),
                new Vector3(xb, yRoad, zStart - 1.5f),
                new Vector3(xb, front.GroundY, zStart)
            }));
            return g;
        }

        private void PopulateInitial(Group g, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Walker w = TakeFromPool();
                if (w == null)
                {
                    break;
                }
                PolylinePath path = g.Paths[rng.Next(g.Paths.Count)];
                Activate(g, w, path, path.Length * Range(0.05f, 0.9f));
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            foreach (Group g in groups)
            {
                TrySpawn(g);
                Advance(g, dt);
            }
        }

        private void Advance(Group g, float dt)
        {
            for (int i = g.Active.Count - 1; i >= 0; i--)
            {
                Walker w = g.Active[i];
                w.S += w.Speed * dt;
                w.Phase += w.Speed * dt * 9f;
                if (w.S >= w.Path.Length)
                {
                    w.Object.SetActive(false);
                    pool.Add(w);
                    g.Active.RemoveAt(i);
                    continue;
                }
                Place(w, dt);
            }
        }

        private void TrySpawn(Group g)
        {
            if (Time.time < g.NextSpawn || g.Active.Count >= g.MaxActive)
            {
                return;
            }

            PolylinePath path = g.Paths[rng.Next(g.Paths.Count)];
            foreach (Walker w in g.Active)
            {
                // 出口(経路の始点)付近が混み合っているなら今回は見送る
                if (w.Path == path && w.S < StartClearance)
                {
                    g.NextSpawn = Time.time + 1f;
                    return;
                }
            }

            Walker walker = TakeFromPool();
            if (walker == null)
            {
                return;
            }
            Activate(g, walker, path, 0f);
            g.NextSpawn = Time.time + Range(g.IntervalMin, g.IntervalMax);
        }

        private Walker TakeFromPool()
        {
            if (pool.Count == 0)
            {
                return null;
            }
            int index = rng.Next(pool.Count);
            Walker w = pool[index];
            pool.RemoveAt(index);
            return w;
        }

        private void Activate(Group g, Walker w, PolylinePath path, float s)
        {
            w.Path = path;
            w.S = s;
            w.Speed = Range(g.SpeedMin, g.SpeedMax);
            w.Phase = Range(0f, 6.28f);
            w.LateralOffset = Range(-g.LateralRange, g.LateralRange);
            w.Object.SetActive(true);
            g.Active.Add(w);
            Place(w, 1f);
        }

        private void Place(Walker w, float dt)
        {
            Vector3 position;
            Vector3 forward;
            w.Path.Evaluate(w.S, out position, out forward);

            Vector3 right = Vector3.Cross(Vector3.up, forward);
            position += right * w.LateralOffset;
            position.y += Mathf.Abs(Mathf.Sin(w.Phase)) * 0.015f;

            // クリアのお祝い: 拍に合わせてぴょんぴょん跳ね、体をひねる
            float party = CityCelebration.Intensity;
            float hop = party > 0f ? CityCelebration.Hop(w.Phase) : 0f;
            position.y += hop * 0.55f * party;
            w.Transform.position = position;

            if (forward.sqrMagnitude > 0.0001f)
            {
                Quaternion want = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, 0f, Mathf.Sin(w.Phase) * 3f);
                if (party > 0f)
                {
                    want *= Quaternion.Euler(0f, Mathf.Sin(CityCelebration.Clock * 6f + w.Phase) * 40f * party, Mathf.Sin(CityCelebration.Clock * 8f + w.Phase) * 12f * party);
                }
                w.Transform.rotation = Quaternion.Slerp(w.Transform.rotation, want, Mathf.Clamp01(dt * 8f));
            }

            float edge = Mathf.Min(w.S, w.Path.Length - w.S);
            float fade = Mathf.Clamp01(edge / EdgeFade);
            // 跳ね上がるときは縦に伸び、着地でつぶれる
            float squash = 1f + 0.18f * party * (0.5f - hop);
            w.Transform.localScale = new Vector3(1f / squash, Mathf.Max(0.02f, fade) * squash, 1f / squash) * w.BaseHeightScale;
        }

        private float Range(float min, float max)
        {
            return Mathf.Lerp(min, max, (float)rng.NextDouble());
        }
    }
}
