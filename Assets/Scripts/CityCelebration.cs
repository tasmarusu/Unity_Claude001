using System.Collections.Generic;
using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// ステージクリア時に、街(歩行者・車・背景ビル・街路樹)を愉快に弾ませる。
    /// 歩行者はぴょんぴょん跳ねて体をひねり、車はホップして傾く。背景のビルは1棟ずつ「チンアナゴ」になり、
    /// 地面からにゅーっと伸びて縮み、ゆらゆら首をふる(隣のビルと位相をずらした波になる)。街路樹は地面を支点にぼよんと伸び縮みする。
    /// ☆3つ(PERFECT)はより派手に。次のステージが始まると止まる。
    /// 歩行者・車は Intensity/Clock を読んで自分の動きに足す。ビル・街路樹は合成メッシュの塊ごとに位相をずらして揺らす。
    /// シーン配置不要で自動的に立ち上がる。
    /// </summary>
    public class CityCelebration : MonoBehaviour
    {
        private class Swayer
        {
            public Transform Transform;
            public Vector3 Pivot;
            public float Phase;
            public float Amplitude;
        }

        private class Eel
        {
            public Transform Transform;
            public float X;
            public float Z;
            public float Phase;
        }

        private static readonly List<Eel> eels = new List<Eel>();
        private static GameObject mergedBackdrop;
        private static GameObject eelBackdrop;
        private bool eelsShown;

        /// <summary>お祝いの強さ(0で通常、1で全開)。なめらかに出入りする。</summary>
        public static float Intensity { get; private set; }

        /// <summary>お祝い用の時計(秒)。強さが0の間は進めない。</summary>
        public static float Clock { get; private set; }

        private const float BeatsPerSecond = 2.2f;

        private static readonly List<Swayer> swayers = new List<Swayer>();
        private float target;
        private bool applied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            swayers.Clear();
            eels.Clear();
            mergedBackdrop = null;
            eelBackdrop = null;
            Intensity = 0f;
            Clock = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<CityCelebration>() != null)
            {
                return;
            }
            new GameObject(nameof(CityCelebration)).AddComponent<CityCelebration>();
        }

        /// <summary>
        /// 合成メッシュの塊を、地面を支点に伸び縮みさせる対象として登録する。
        /// </summary>
        public static void RegisterSwayer(Transform transform, Bounds worldBounds, float phase, float amplitude)
        {
            swayers.Add(new Swayer
            {
                Transform = transform,
                Pivot = new Vector3(worldBounds.center.x, worldBounds.min.y, worldBounds.center.z),
                Phase = phase,
                Amplitude = amplitude
            });
        }

        /// <summary>
        /// 背景ビルを「1棟ずつ動かせる版」と入れ替える準備。通常は結合メッシュ(軽い)を描き、お祝い中だけ個別版に切り替える。
        /// 個別版の各ビルは、足元(地面の中心)が原点のオブジェクト。
        /// </summary>
        public static void RegisterEelBackdrop(GameObject merged, GameObject individual, List<Transform> buildings)
        {
            mergedBackdrop = merged;
            eelBackdrop = individual;
            eels.Clear();
            foreach (Transform t in buildings)
            {
                eels.Add(new Eel
                {
                    Transform = t,
                    X = t.position.x,
                    Z = t.position.z,
                    Phase = Mathf.Repeat(t.position.x * 12.9898f + t.position.z * 78.233f, 6.2832f)
                });
            }
            individual.SetActive(false);
        }

        /// <summary>
        /// 拍に合わせた0〜1のなめらかな上下(歩行者・車がこれで跳ねる)。
        /// </summary>
        public static float Hop(float phase, float speed = 1f)
        {
            return Mathf.Abs(Mathf.Sin(Clock * Mathf.PI * BeatsPerSecond * speed + phase));
        }

        private void Start()
        {
            Subscribe();
        }

        private bool subscribed;

        private void Subscribe()
        {
            GameManager gm = GameManager.Instance;
            if (subscribed || gm == null)
            {
                return;
            }
            subscribed = true;
            gm.StageEnded += OnStageEnded;
            gm.StageStarted += OnStageStarted;
        }

        private void OnDestroy()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null && subscribed)
            {
                gm.StageEnded -= OnStageEnded;
                gm.StageStarted -= OnStageStarted;
            }
        }

        private void OnStageEnded(StageResult result)
        {
            target = result.Failed ? 0f : (result.Stars >= 3 ? 1f : 0.7f);
        }

        private void OnStageStarted(StageSpec spec, int shots)
        {
            target = 0f;
        }

        private void Update()
        {
            Subscribe();
            float dt = Time.unscaledDeltaTime;
            Intensity = Mathf.MoveTowards(Intensity, target, dt * (target > Intensity ? 3.5f : 2.5f));
            if (Intensity > 0f)
            {
                Clock += dt;
            }
        }

        private void AnimateEels()
        {
            if (eels.Count == 0 || mergedBackdrop == null || eelBackdrop == null)
            {
                return;
            }

            bool want = Intensity > 0f;
            if (want != eelsShown)
            {
                eelsShown = want;
                mergedBackdrop.SetActive(!want);
                eelBackdrop.SetActive(want);
            }
            if (!want)
            {
                return;
            }

            // チンアナゴ: 地面から伸びたり引っ込んだり(伸びるとき細く)、首を左右前後にゆらゆら。位置ごとに位相をずらした波にする
            float clock = Clock * Mathf.PI * 2f * 0.45f;
            foreach (Eel e in eels)
            {
                if (e.Transform == null)
                {
                    continue;
                }
                float theta = clock - e.X * 0.28f - e.Z * 0.18f;
                float peek = 0.5f + 0.5f * Mathf.Sin(theta);
                float sy = 1f + Intensity * Mathf.Lerp(-0.1f, 0.42f, peek);
                float sx = 1f / Mathf.Sqrt(sy);
                float tiltX = Intensity * 9f * Mathf.Sin(theta + 1.6f + e.Phase);
                float tiltZ = Intensity * 9f * Mathf.Sin(theta * 0.8f + e.Phase * 2f);
                e.Transform.localScale = new Vector3(sx, sy, sx);
                e.Transform.rotation = Quaternion.Euler(tiltX, 0f, tiltZ);
            }
        }

        private void LateUpdate()
        {
            AnimateEels();
            if (Intensity <= 0f)
            {
                if (applied)
                {
                    foreach (Swayer s in swayers)
                    {
                        if (s.Transform != null)
                        {
                            s.Transform.position = Vector3.zero;
                            s.Transform.localScale = Vector3.one;
                        }
                    }
                    applied = false;
                }
                return;
            }

            applied = true;
            float beat = Clock * Mathf.PI * BeatsPerSecond;
            foreach (Swayer s in swayers)
            {
                if (s.Transform == null)
                {
                    continue;
                }

                // 地面(支点のy)を固定して縦に伸び縮み。伸びるとき少し細く、縮むとき少し太く(ぼよん)
                float wave = Mathf.Sin(beat + s.Phase);
                float stretch = 1f + s.Amplitude * Intensity * wave;
                float squash = 1f - 0.4f * s.Amplitude * Intensity * wave;
                Vector3 scale = new Vector3(squash, stretch, squash);
                s.Transform.localScale = scale;
                Vector3 scaledPivot = Vector3.Scale(scale, s.Pivot);
                s.Transform.position = s.Pivot - scaledPivot;
            }
        }
    }
}
