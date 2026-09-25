using System.Collections.Generic;
using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// 奥の道路の路肩を歩き、横断歩道で道路を渡っていく歩行者。
    /// 主役(手前のタワー)の前には出さず、奥の路肩と横断歩道だけを使う。人数は上限つきのプールで使い回す。
    /// 経路の両端ではスケールを絞って出入りを目立たなくする。
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

        private const int MaxActive = 8;
        private const int PoolPerType = 2;
        private const float ShoulderInset = 1.7f;
        private const float WalkDepth = 82f;
        private const float EdgeFade = 3f;
        private const float MinSpawnInterval = 3f;
        private const float MaxSpawnInterval = 9f;
        private const float StartClearance = 4f;

        private readonly List<Walker> active = new List<Walker>();
        private readonly List<Walker> pool = new List<Walker>();
        private readonly List<PolylinePath> paths = new List<PolylinePath>();
        private System.Random rng;
        private CityLife.RoadInfo road;
        private float nextSpawnTime;

        public void Init(CityLife.RoadInfo roadInfo, System.Random random)
        {
            road = roadInfo;
            rng = random;

            GameObject[] prefabs = CityLife.LoadPrefabs("People");
            if (prefabs.Length == 0)
            {
                enabled = false;
                return;
            }

            BuildPool(prefabs);
            BuildPaths();
            PopulateInitialWalkers();
            nextSpawnTime = Time.time + Range(MinSpawnInterval, MaxSpawnInterval);
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
        private void BuildPaths()
        {
            float y = road.SurfaceY;
            float zFar = Mathf.Min(road.ZEnd - 10f, road.ZStart + WalkDepth);
            float zCross = road.CrosswalkZ;
            float x = road.HalfWidth - ShoulderInset;

            for (int side = -1; side <= 1; side += 2)
            {
                float a = road.CenterX + side * x;
                float b = road.CenterX - side * x;
                paths.Add(new PolylinePath(new[]
                {
                    new Vector3(a, y, zFar),
                    new Vector3(a, y, zCross),
                    new Vector3(b, y, zCross),
                    new Vector3(b, y, zFar)
                }));
            }
        }

        private void PopulateInitialWalkers()
        {
            for (int i = 0; i < MaxActive / 2; i++)
            {
                Walker w = TakeFromPool();
                if (w == null)
                {
                    break;
                }
                PolylinePath path = paths[rng.Next(paths.Count)];
                Activate(w, path, path.Length * Range(0.05f, 0.9f));
            }
        }

        private void Update()
        {
            if (paths.Count == 0)
            {
                return;
            }

            float dt = Time.deltaTime;
            TrySpawn();

            for (int i = active.Count - 1; i >= 0; i--)
            {
                Walker w = active[i];
                w.S += w.Speed * dt;
                w.Phase += w.Speed * dt * 9f;
                if (w.S >= w.Path.Length)
                {
                    w.Object.SetActive(false);
                    pool.Add(w);
                    active.RemoveAt(i);
                    continue;
                }
                Place(w, dt);
            }
        }

        private void TrySpawn()
        {
            if (Time.time < nextSpawnTime || active.Count >= MaxActive)
            {
                return;
            }

            PolylinePath path = paths[rng.Next(paths.Count)];
            foreach (Walker w in active)
            {
                // 出口(経路の始点)付近が混み合っているなら今回は見送る
                if (w.Path == path && w.S < StartClearance)
                {
                    nextSpawnTime = Time.time + 1f;
                    return;
                }
            }

            Walker walker = TakeFromPool();
            if (walker == null)
            {
                return;
            }
            Activate(walker, path, 0f);
            nextSpawnTime = Time.time + Range(MinSpawnInterval, MaxSpawnInterval);
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

        private void Activate(Walker w, PolylinePath path, float s)
        {
            w.Path = path;
            w.S = s;
            w.Speed = Range(0.65f, 0.95f);
            w.Phase = Range(0f, 6.28f);
            w.LateralOffset = Range(-0.35f, 0.35f);
            w.Object.SetActive(true);
            active.Add(w);
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
            w.Transform.position = position;

            if (forward.sqrMagnitude > 0.0001f)
            {
                Quaternion want = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, 0f, Mathf.Sin(w.Phase) * 3f);
                w.Transform.rotation = Quaternion.Slerp(w.Transform.rotation, want, Mathf.Clamp01(dt * 8f));
            }

            float edge = Mathf.Min(w.S, w.Path.Length - w.S);
            float fade = Mathf.Clamp01(edge / EdgeFade);
            w.Transform.localScale = new Vector3(1f, Mathf.Max(0.02f, fade), 1f) * w.BaseHeightScale;
        }

        private float Range(float min, float max)
        {
            return Mathf.Lerp(min, max, (float)rng.NextDouble());
        }
    }
}
