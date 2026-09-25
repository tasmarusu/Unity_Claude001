using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// 背景を「生きた都市」にする実行時システムの起点。シーン配置不要で自動的に立ち上がり、
    /// 背景ビルの再構成・街路樹/街灯・車・歩行者を生成する。失敗してもゲーム進行には影響しない(警告のみ)。
    /// 道路の位置はシーン上のRoadオブジェクトから読み取る(モデル制作セッションの配置に追従する)。
    /// </summary>
    public class CityLife : MonoBehaviour
    {
        /// <summary>
        /// 奥へ伸びる道路(Road)の寸法。中心線はx=CenterX、進行方向はz。
        /// </summary>
        public struct RoadInfo
        {
            public float CenterX;
            public float HalfWidth;
            public float ZStart;
            public float ZEnd;
            public float SurfaceY;
            public float CrosswalkZ;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<CityLife>() != null)
            {
                return;
            }
            new GameObject(nameof(CityLife)).AddComponent<CityLife>();
        }

        private IEnumerator Start()
        {
            // 他の初期化(GameManagerのタワー生成など)が済んでから、1フレーム空けて始める
            yield return null;
            yield return null;

            System.Random rng = new System.Random(unchecked(Environment.TickCount * 31 + 7));

            RoadInfo road;
            bool hasRoad = TryReadRoad(out road);

            Run("backdrop", () =>
            {
                GameObject backdrop = GameObject.Find("CityBackdrop");
                if (backdrop != null)
                {
                    CityBackdropRandomizer.Rebuild(backdrop, rng);
                }
            });

            if (!hasRoad)
            {
                Debug.LogWarning("[CityLife] Road object not found; skipping traffic, pedestrians and props.");
                yield break;
            }

            Run("props", () => CityStreetProps.Build(road, rng, transform));
            Run("traffic", () => gameObject.AddComponent<CityTraffic>().Init(road, rng));
            Run("pedestrians", () => gameObject.AddComponent<CityPedestrians>().Init(road, rng));
        }

        private static void Run(string label, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CityLife] " + label + " failed: " + e.Message);
            }
        }

        private static bool TryReadRoad(out RoadInfo info)
        {
            info = default;
            GameObject road = GameObject.Find("Road");
            if (road == null)
            {
                return false;
            }

            Vector3 p = road.transform.position;
            Vector3 s = road.transform.lossyScale;
            GameObject crosswalk = GameObject.Find("Crosswalk");

            info.CenterX = p.x;
            info.HalfWidth = s.x * 0.5f;
            info.ZStart = p.z - s.z * 0.5f;
            info.ZEnd = p.z + s.z * 0.5f;
            info.SurfaceY = p.y + s.y * 0.5f;
            info.CrosswalkZ = crosswalk != null ? crosswalk.transform.position.z : info.ZStart + 5f;
            return true;
        }

        /// <summary>
        /// Resources/CityLife/<folder>のプレハブを全て読み込む。無ければ空配列。
        /// </summary>
        public static GameObject[] LoadPrefabs(string folder)
        {
            return Resources.LoadAll<GameObject>("CityLife/" + folder);
        }
    }

    /// <summary>
    /// 折れ線の経路。車の周回ルートと歩行者の横断ルートの両方に使う。
    /// </summary>
    public class PolylinePath
    {
        private readonly List<Vector3> points = new List<Vector3>();
        private readonly List<float> cumulative = new List<float>();

        public float Length { get; private set; }

        public PolylinePath(IEnumerable<Vector3> pts)
        {
            float total = 0f;
            foreach (Vector3 p in pts)
            {
                if (points.Count > 0)
                {
                    total += Vector3.Distance(points[points.Count - 1], p);
                }
                points.Add(p);
                cumulative.Add(total);
            }
            Length = total;
        }

        public void Evaluate(float s, out Vector3 position, out Vector3 forward)
        {
            s = Mathf.Clamp(s, 0f, Length);
            int i = 1;
            while (i < points.Count - 1 && cumulative[i] < s)
            {
                i++;
            }

            Vector3 a = points[i - 1];
            Vector3 b = points[i];
            float segment = Mathf.Max(0.0001f, cumulative[i] - cumulative[i - 1]);
            float t = (s - cumulative[i - 1]) / segment;
            position = Vector3.Lerp(a, b, t);
            forward = (b - a).normalized;
        }
    }
}
