using System.Collections.Generic;
using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// 奥の道路を走る車。左側通行で、遠くから手前へ来て、横断歩道の手前でUターンして奥へ戻っていく周回ルート。
    /// 車種・車体色・速度はバラバラ、前の車との車間を保つ(追い越し・重なりなし)。
    /// 車の数は上限つきのプールで使い回し、メインの建物崩壊より目立たない小ささ/速度にする。
    /// </summary>
    public class CityTraffic : MonoBehaviour
    {
        private class Car
        {
            public GameObject Object;
            public Transform Transform;
            public MeshRenderer Renderer;
            public float Length;
            public float DesiredSpeed;
            public float Speed;
            public float S;
            public bool IsTaxi;
        }

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private static readonly Color[] BodyColors =
        {
            new Color(1f, 1f, 1f), new Color(0.92f, 0.28f, 0.24f), new Color(0.3f, 0.55f, 0.92f),
            new Color(0.35f, 0.78f, 0.5f), new Color(0.82f, 0.84f, 0.9f), new Color(0.95f, 0.65f, 0.2f)
        };

        private const int MaxActiveCars = 7;
        private const int PoolPerType = 2;
        private const float LaneOffset = 3.2f;
        private const float TurnSegments = 16;
        private const float MinGap = 5f;
        private const float Acceleration = 2.5f;
        private const float MinSpawnInterval = 2.5f;
        private const float MaxSpawnInterval = 7f;

        private readonly List<Car> active = new List<Car>();
        private readonly List<Car> pool = new List<Car>();
        private PolylinePath path;
        private CityLife.RoadInfo road;
        private System.Random rng;
        private MaterialPropertyBlock block;
        private float nextSpawnTime;

        public void Init(CityLife.RoadInfo roadInfo, System.Random random)
        {
            road = roadInfo;
            rng = random;
            block = new MaterialPropertyBlock();

            GameObject[] prefabs = CityLife.LoadPrefabs("Cars");
            if (prefabs.Length == 0)
            {
                enabled = false;
                return;
            }

            BuildPool(prefabs);
            path = BuildPath();
            PopulateInitialCars();
            nextSpawnTime = Time.time + Range(MinSpawnInterval, MaxSpawnInterval);
        }

        private void BuildPool(GameObject[] prefabs)
        {
            GameObject root = new GameObject("CityCars");
            root.transform.SetParent(transform, false);

            foreach (GameObject prefab in prefabs)
            {
                MeshFilter filter = prefab.GetComponentInChildren<MeshFilter>();
                float length = filter != null && filter.sharedMesh != null
                    ? filter.sharedMesh.bounds.size.z * prefab.transform.lossyScale.z
                    : 1.3f;

                for (int i = 0; i < PoolPerType; i++)
                {
                    GameObject go = Instantiate(prefab, root.transform);
                    go.name = prefab.name;
                    foreach (Collider c in go.GetComponentsInChildren<Collider>())
                    {
                        Destroy(c);
                    }
                    go.SetActive(false);

                    pool.Add(new Car
                    {
                        Object = go,
                        Transform = go.transform,
                        Renderer = go.GetComponentInChildren<MeshRenderer>(),
                        Length = length,
                        IsTaxi = prefab.name.Contains("Taxi")
                    });
                }
            }
        }

        private PolylinePath BuildPath()
        {
            float zFar = road.ZEnd - 2f;
            float zTurn = road.CrosswalkZ + 8f;
            float cx = road.CenterX;
            float y = road.SurfaceY;
            float radius = LaneOffset;

            List<Vector3> pts = new List<Vector3>();
            pts.Add(new Vector3(cx + radius, y, zFar));
            pts.Add(new Vector3(cx + radius, y, zTurn));
            for (int i = 1; i < TurnSegments; i++)
            {
                float theta = Mathf.PI * i / TurnSegments;
                pts.Add(new Vector3(cx + radius * Mathf.Cos(theta), y, zTurn - radius * Mathf.Sin(theta)));
            }
            pts.Add(new Vector3(cx - radius, y, zTurn));
            pts.Add(new Vector3(cx - radius, y, zFar));
            return new PolylinePath(pts);
        }

        /// <summary>
        /// 開始直後から道路が空にならないよう、間隔を空けて数台を先に走らせておく。
        /// </summary>
        private void PopulateInitialCars()
        {
            float s = path.Length * Range(0.12f, 0.3f);
            int count = 0;
            while (count < MaxActiveCars - 2 && s < path.Length * 0.9f)
            {
                Car car = TakeFromPool();
                if (car == null)
                {
                    break;
                }
                Activate(car, s);
                s += car.Length + Range(MinGap, 18f);
                count++;
            }
            // s昇順で追加したので、先頭(=最前)が最大になるよう並べ替える
            active.Sort((a, b) => b.S.CompareTo(a.S));
        }

        private void Update()
        {
            if (path == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            TrySpawn();

            for (int i = 0; i < active.Count; i++)
            {
                Car car = active[i];
                Car leader = i > 0 ? active[i - 1] : null;

                float target = car.DesiredSpeed;
                if (leader != null)
                {
                    float gap = leader.S - car.S - (leader.Length + car.Length) * 0.5f;
                    if (gap < MinGap)
                    {
                        target = Mathf.Min(target, Mathf.Max(0f, leader.Speed + (gap - 1.5f) * 0.6f));
                    }
                }

                car.Speed = Mathf.MoveTowards(car.Speed, target, Acceleration * dt);
                car.S += car.Speed * dt;
                Place(car, dt);
            }

            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (active[i].S >= path.Length)
                {
                    Release(active[i]);
                    active.RemoveAt(i);
                }
            }
        }

        private void TrySpawn()
        {
            if (Time.time < nextSpawnTime || active.Count >= MaxActiveCars)
            {
                return;
            }

            Car candidate = TakeFromPool();
            if (candidate == null)
            {
                return;
            }

            // 直前に出した車との車間が十分あるときだけ出す
            if (active.Count > 0)
            {
                Car last = active[active.Count - 1];
                float needed = (last.Length + candidate.Length) * 0.5f + MinGap;
                if (last.S < needed)
                {
                    pool.Add(candidate);
                    return;
                }
            }

            Activate(candidate, 0f);
            nextSpawnTime = Time.time + Range(MinSpawnInterval, MaxSpawnInterval);
        }

        private Car TakeFromPool()
        {
            if (pool.Count == 0)
            {
                return null;
            }
            int index = rng.Next(pool.Count);
            Car car = pool[index];
            pool.RemoveAt(index);
            return car;
        }

        private void Activate(Car car, float s)
        {
            car.S = s;
            car.DesiredSpeed = Range(2.6f, 4.4f);
            car.Speed = car.DesiredSpeed;
            if (car.Renderer != null && !car.IsTaxi)
            {
                block.SetColor(ColorId, BodyColors[rng.Next(BodyColors.Length)]);
                car.Renderer.SetPropertyBlock(block);
            }
            car.Object.SetActive(true);
            active.Add(car);
            Place(car, 1f);
        }

        private void Release(Car car)
        {
            car.Object.SetActive(false);
            pool.Add(car);
        }

        private void Place(Car car, float dt)
        {
            Vector3 position;
            Vector3 forward;
            path.Evaluate(car.S, out position, out forward);
            car.Transform.position = position;
            if (forward.sqrMagnitude > 0.0001f)
            {
                Quaternion want = Quaternion.LookRotation(forward, Vector3.up);
                car.Transform.rotation = Quaternion.Slerp(car.Transform.rotation, want, Mathf.Clamp01(dt * 10f));
            }
        }

        private float Range(float min, float max)
        {
            return Mathf.Lerp(min, max, (float)rng.NextDouble());
        }
    }
}
