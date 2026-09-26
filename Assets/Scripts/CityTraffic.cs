using System.Collections.Generic;
using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// 街を走る車。次の2つの流れがある(どちらも同じプールを使い回す)。
    ///  - 奥の道路: 左側通行で、遠くから手前へ来て、横断歩道の手前でUターンして奥へ戻る周回ルート
    ///  - 手前の道路: 画面下を横切る手前の道路を、ゆっくり左から右へ通り過ぎる
    /// 車種・車体色・速度はバラバラ、前の車との車間を保つ(追い越し・重なりなし)。
    /// 車にはColliderが無く、タップ判定や崩落に一切影響しない。
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
            public Vector3 BaseScale = Vector3.one;
        }

        private class Flow
        {
            public PolylinePath Path;
            public readonly List<Car> Active = new List<Car>();
            public int MaxActive;
            public float SpeedMin;
            public float SpeedMax;
            public float IntervalMin;
            public float IntervalMax;
            public float NextSpawn;
        }

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private static readonly Color[] BodyColors =
        {
            new Color(1f, 1f, 1f), new Color(0.92f, 0.28f, 0.24f), new Color(0.3f, 0.55f, 0.92f),
            new Color(0.35f, 0.78f, 0.5f), new Color(0.82f, 0.84f, 0.9f), new Color(0.95f, 0.65f, 0.2f)
        };

        private const int PoolPerType = 3;
        private const float LaneOffset = 3.2f;
        private const int TurnSegments = 16;
        private const float MinGap = 5f;
        private const float Acceleration = 2.5f;

        private readonly List<Car> pool = new List<Car>();
        private readonly List<Flow> flows = new List<Flow>();
        private System.Random rng;
        private MaterialPropertyBlock block;

        public void Init(CityLife.RoadInfo road, CityLife.FrontInfo front, System.Random random)
        {
            rng = random;
            block = new MaterialPropertyBlock();

            GameObject[] prefabs = CityLife.LoadPrefabs("Cars");
            if (prefabs.Length == 0)
            {
                enabled = false;
                return;
            }

            BuildPool(prefabs);

            Flow deep = new Flow
            {
                Path = BuildDeepPath(road),
                MaxActive = 5,
                SpeedMin = 2.6f,
                SpeedMax = 4.4f,
                IntervalMin = 2.5f,
                IntervalMax = 7f
            };
            flows.Add(deep);
            PopulateInitialCars(deep);

            if (front.Valid)
            {
                Flow near = new Flow
                {
                    Path = BuildFrontPath(front),
                    MaxActive = 3,
                    SpeedMin = 2f,
                    SpeedMax = 3.2f,
                    IntervalMin = 2.5f,
                    IntervalMax = 5.5f
                };
                flows.Add(near);
                near.NextSpawn = Time.time + 0.5f;
            }

            foreach (Flow f in flows)
            {
                f.NextSpawn = Mathf.Max(f.NextSpawn, Time.time + Range(f.IntervalMin, f.IntervalMax));
            }
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
                        BaseScale = go.transform.localScale,
                        IsTaxi = prefab.name.Contains("Taxi")
                    });
                }
            }
        }

        private PolylinePath BuildDeepPath(CityLife.RoadInfo road)
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
        /// 手前の道路(画面の下側を横切る)の、タワー側の車線を+X方向へ走る(左側通行)。
        /// </summary>
        private PolylinePath BuildFrontPath(CityLife.FrontInfo front)
        {
            float z = front.RoadCenterZ + front.NearLaneOffset;
            float xStart = Mathf.Max(front.RoadXMin, front.ViewCenterX - 26f);
            float xEnd = Mathf.Min(front.RoadXMax, front.ViewCenterX + 34f);
            return new PolylinePath(new[]
            {
                new Vector3(xStart, front.RoadSurfaceY, z),
                new Vector3(xEnd, front.RoadSurfaceY, z)
            });
        }

        /// <summary>
        /// 開始直後から道路が空にならないよう、間隔を空けて数台を先に走らせておく。
        /// </summary>
        private void PopulateInitialCars(Flow flow)
        {
            float s = flow.Path.Length * Range(0.12f, 0.3f);
            int count = 0;
            while (count < flow.MaxActive - 1 && s < flow.Path.Length * 0.9f)
            {
                Car car = TakeFromPool();
                if (car == null)
                {
                    break;
                }
                Activate(flow, car, s);
                s += car.Length + Range(MinGap, 18f);
                count++;
            }
            flow.Active.Sort((a, b) => b.S.CompareTo(a.S));
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            foreach (Flow flow in flows)
            {
                TrySpawn(flow);
                Advance(flow, dt);
            }
        }

        private void Advance(Flow flow, float dt)
        {
            List<Car> active = flow.Active;
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
                Place(flow, car, dt);
            }

            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (active[i].S >= flow.Path.Length)
                {
                    active[i].Object.SetActive(false);
                    pool.Add(active[i]);
                    active.RemoveAt(i);
                }
            }
        }

        private void TrySpawn(Flow flow)
        {
            if (Time.time < flow.NextSpawn || flow.Active.Count >= flow.MaxActive)
            {
                return;
            }

            Car candidate = TakeFromPool();
            if (candidate == null)
            {
                return;
            }

            // 直前に出した車との車間が十分あるときだけ出す
            if (flow.Active.Count > 0)
            {
                Car last = flow.Active[flow.Active.Count - 1];
                float needed = (last.Length + candidate.Length) * 0.5f + MinGap;
                if (last.S < needed)
                {
                    pool.Add(candidate);
                    return;
                }
            }

            Activate(flow, candidate, 0f);
            flow.NextSpawn = Time.time + Range(flow.IntervalMin, flow.IntervalMax);
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

        private void Activate(Flow flow, Car car, float s)
        {
            car.S = s;
            car.DesiredSpeed = Range(flow.SpeedMin, flow.SpeedMax);
            car.Speed = car.DesiredSpeed;
            if (car.Renderer != null && !car.IsTaxi)
            {
                block.SetColor(ColorId, BodyColors[rng.Next(BodyColors.Length)]);
                car.Renderer.SetPropertyBlock(block);
            }
            car.Object.SetActive(true);
            flow.Active.Add(car);
            Place(flow, car, 1f);
        }

        private void Place(Flow flow, Car car, float dt)
        {
            Vector3 position;
            Vector3 forward;
            flow.Path.Evaluate(car.S, out position, out forward);

            // クリアのお祝い: 車がホップして左右に傾き、着地でむにっとつぶれる
            float party = CityCelebration.Intensity;
            float phase = car.Length * 1.3f;
            float hop = party > 0f ? CityCelebration.Hop(phase, 0.9f) : 0f;
            position.y += hop * 0.7f * party;
            car.Transform.position = position;
            if (party > 0f || car.Transform.localScale != car.BaseScale)
            {
                float squash = 1f + 0.12f * party * (0.5f - hop);
                car.Transform.localScale = new Vector3(car.BaseScale.x / squash, car.BaseScale.y * squash, car.BaseScale.z / squash);
            }
            if (forward.sqrMagnitude > 0.0001f)
            {
                Quaternion want = Quaternion.LookRotation(forward, Vector3.up);
                if (party > 0f)
                {
                    want *= Quaternion.Euler(Mathf.Sin(CityCelebration.Clock * 7f + phase) * 6f * party, 0f, Mathf.Sin(CityCelebration.Clock * 5f + phase) * 9f * party);
                }
                car.Transform.rotation = Quaternion.Slerp(car.Transform.rotation, want, Mathf.Clamp01(dt * 10f));
            }
        }

        private float Range(float min, float max)
        {
            return Mathf.Lerp(min, max, (float)rng.NextDouble());
        }
    }
}
