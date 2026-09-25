using System.Collections;
using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// Safe Area(ノッチ/パンチホール/ナビゲーションバー)とバナー広告の占有領域を、キャンバス単位のインセットとして返す。
    /// </summary>
    public static class SafeAreaUtil
    {
        public struct Insets
        {
            public float Left, Right, Top, Bottom;
        }

        /// <summary>
        /// safeAreaとscreenSizeを渡せる純粋関数版(エディタで各種端末の比率を疑似検証するため)。
        /// </summary>
        public static Insets Compute(Rect safeArea, Vector2 screenSize, Vector2 canvasSize, float bannerFractionOfHeight)
        {
            float sx = canvasSize.x / Mathf.Max(1f, screenSize.x);
            float sy = canvasSize.y / Mathf.Max(1f, screenSize.y);
            Insets insets = new Insets
            {
                Left = safeArea.xMin * sx,
                Right = (screenSize.x - safeArea.xMax) * sx,
                Top = (screenSize.y - safeArea.yMax) * sy,
                Bottom = safeArea.yMin * sy
            };
            insets.Bottom = Mathf.Max(insets.Bottom, bannerFractionOfHeight * canvasSize.y);
            return insets;
        }

        public static Insets Current(RectTransform canvasRect)
        {
            Camera cam = Camera.main;
            float banner = cam != null ? cam.rect.y : 0f;
            return Compute(Screen.safeArea, new Vector2(Screen.width, Screen.height), canvasRect.rect.size, banner);
        }
    }

    /// <summary>
    /// 既存(シーン配置済み)のUIのうち、画面上端/左右端に固定されている要素をSafe Areaの内側へずらす。
    /// シーンを書き換えず実行時に補正するので、他セッションによるシーン上書きの影響を受けない。
    /// </summary>
    public class SafeAreaAdapter : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<SafeAreaAdapter>() != null)
            {
                return;
            }
            new GameObject(nameof(SafeAreaAdapter)).AddComponent<SafeAreaAdapter>();
        }

        private IEnumerator Start()
        {
            // 他のUI(ミュートボタン等)の生成完了を待ってから1回だけ補正する
            yield return null;
            yield return null;

            GameObject canvasGO = GameObject.Find("ScoreCanvas");
            if (canvasGO == null)
            {
                yield break;
            }
            RectTransform canvasRect = canvasGO.transform as RectTransform;
            SafeAreaUtil.Insets insets = SafeAreaUtil.Current(canvasRect);

            foreach (RectTransform rt in canvasGO.GetComponentsInChildren<RectTransform>(true))
            {
                if (rt.parent != canvasRect)
                {
                    continue;
                }

                Vector2 pos = rt.anchoredPosition;
                bool anchoredTop = rt.anchorMin.y > 0.99f;
                if (anchoredTop)
                {
                    pos.y -= insets.Top;
                }
                if (rt.anchorMin.x < 0.01f && rt.anchorMax.x < 0.01f)
                {
                    pos.x += insets.Left;
                }
                else if (rt.anchorMin.x > 0.99f && rt.anchorMax.x > 0.99f)
                {
                    pos.x -= insets.Right;
                }
                rt.anchoredPosition = pos;
            }
        }
    }
}

namespace OneTapDemolition
{
    /// <summary>
    /// 自分のRectTransformをSafe Area(ノッチ/ナビバー)とバナー広告の占有領域の内側に収める。
    /// 端末回転・バナー読み込み完了によるカメラviewport変更にも追従する。
    /// </summary>
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rt;
        private RectTransform canvasRect;
        private SafeAreaUtil.Insets last;
        private bool hasLast;

        private void Update()
        {
            if (rt == null)
            {
                rt = transform as RectTransform;
                Canvas canvas = GetComponentInParent<Canvas>();
                canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            }
            if (rt == null || canvasRect == null)
            {
                return;
            }

            SafeAreaUtil.Insets insets = SafeAreaUtil.Current(canvasRect);
            if (hasLast
                && Mathf.Approximately(insets.Left, last.Left) && Mathf.Approximately(insets.Right, last.Right)
                && Mathf.Approximately(insets.Top, last.Top) && Mathf.Approximately(insets.Bottom, last.Bottom))
            {
                return;
            }

            last = insets;
            hasLast = true;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(insets.Left, insets.Bottom);
            rt.offsetMax = new Vector2(-insets.Right, -insets.Top);
        }
    }
}
