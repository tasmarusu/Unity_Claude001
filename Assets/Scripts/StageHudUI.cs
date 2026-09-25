using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace OneTapDemolition
{
    /// <summary>
    /// プレイ中のHUD。ステージ番号・残りショット・星の目標バーと、狙っている間の予測スコアを出す。
    /// 進行ロジックは持たず、GameManager/ScoreManager/TapDemolishControllerのイベントを表示するだけ。
    /// シーンに手動配置せず、実行時に自分でCanvasを生成する。
    /// </summary>
    public class StageHudUI : MonoBehaviour
    {
        private const float BarWidth = 640f;

        private static readonly Color BarBg = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color BarFill = new Color(0.45f, 0.85f, 1f, 1f);
        private static readonly Color StarOff = new Color(0.35f, 0.35f, 0.4f, 1f);
        private static readonly Color StarOn = new Color(1f, 0.85f, 0.2f, 1f);
        private static readonly Color PipOn = new Color(1f, 0.9f, 0.3f, 1f);
        private static readonly Color PipOff = new Color(0.25f, 0.25f, 0.28f, 0.9f);
        private static readonly Color GoodColor = new Color(0.6f, 1f, 0.55f, 1f);
        private static readonly Color BadColor = new Color(1f, 0.4f, 0.4f, 1f);

        private RectTransform canvasRect;
        private RectTransform safeRoot;
        private Text stageText;
        private RectTransform pipsRoot;
        private Image[] pips = new Image[0];
        private RectTransform fill;
        private Image star2;
        private Image star3;
        private Text aimText;
        private RectTransform aimRect;

        private StageSpec spec;
        private bool star2Lit;
        private bool star3Lit;
        private TapDemolishController controller;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<StageHudUI>() != null)
            {
                return;
            }
            new GameObject(nameof(StageHudUI)).AddComponent<StageHudUI>();
        }

        private void Start()
        {
            Canvas canvas = UiKit.CreateCanvas("StageHudCanvas", 5);
            canvasRect = canvas.transform as RectTransform;
            safeRoot = UiKit.CreateSafeRoot(canvas);
            BuildHud();

            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                gm.StageStarted += OnStageStarted;
                gm.ShotsChanged += OnShotsChanged;
                if (gm.CurrentSpec != null)
                {
                    OnStageStarted(gm.CurrentSpec, gm.ShotsLeft);
                    OnShotsChanged(gm.ShotsLeft, gm.ShotsLeft);
                }
            }

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.ScoreChanged += OnScoreChanged;
            }

            controller = FindFirstObjectByType<TapDemolishController>();
            if (controller != null)
            {
                controller.AimChanged += OnAimChanged;
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StageStarted -= OnStageStarted;
                GameManager.Instance.ShotsChanged -= OnShotsChanged;
            }
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.ScoreChanged -= OnScoreChanged;
            }
            if (controller != null)
            {
                controller.AimChanged -= OnAimChanged;
            }
        }

        private void BuildHud()
        {
            // スコア(シーン側のScoreCanvas)は上端中央の約180ユニットを使うので、その直下に並べる
            RectTransform panel = UiKit.NewRect("HudPanel", safeRoot);
            UiKit.SetAnchored(panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(BarWidth + 40f, 210f));

            stageText = UiKit.Label(panel, "StageText", "STAGE 1", 46, Color.white, TextAnchor.MiddleLeft);
            UiKit.SetAnchored(stageText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, 0f), new Vector2(300f, 60f));

            pipsRoot = UiKit.NewRect("Pips", panel);
            UiKit.SetAnchored(pipsRoot, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -6f), new Vector2(300f, 50f));

            RectTransform barRoot = UiKit.NewRect("Bar", panel);
            UiKit.SetAnchored(barRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(BarWidth, 30f));
            Image bg = barRoot.gameObject.AddComponent<Image>();
            bg.sprite = UiSprites.RoundedRect;
            bg.type = Image.Type.Sliced;
            bg.color = BarBg;
            bg.raycastTarget = false;

            Image fillImage = UiKit.Panel(barRoot, "Fill", BarFill);
            fill = fillImage.rectTransform;
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            fill.sizeDelta = new Vector2(0f, 0f);

            star2 = MakeStarMarker(barRoot, "Star2", 0.667f);
            star3 = MakeStarMarker(barRoot, "Star3", 1f);

            aimText = UiKit.Label(canvasRect, "AimText", "", 84, GoodColor);
            aimRect = aimText.rectTransform;
            aimRect.sizeDelta = new Vector2(500f, 120f);
            aimText.gameObject.SetActive(false);
        }

        private static Image MakeStarMarker(RectTransform bar, string name, float normalizedX)
        {
            Image star = UiKit.Panel(bar, name, StarOff, false);
            star.sprite = UiSprites.Star;
            RectTransform rt = star.rectTransform;
            UiKit.SetAnchored(rt, new Vector2(normalizedX, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(58f, 58f));
            return star;
        }

        private void OnStageStarted(StageSpec newSpec, int shots)
        {
            spec = newSpec;
            star2Lit = false;
            star3Lit = false;
            stageText.text = "STAGE " + newSpec.StageIndex;
            star2.color = StarOff;
            star3.color = StarOff;
            star2.rectTransform.localScale = Vector3.one;
            star3.rectTransform.localScale = Vector3.one;
            fill.sizeDelta = new Vector2(0f, 0f);
            RebuildPips(shots);
        }

        private void RebuildPips(int total)
        {
            foreach (Image p in pips)
            {
                if (p != null)
                {
                    Destroy(p.gameObject);
                }
            }

            pips = new Image[total];
            float spacing = 46f;
            for (int i = 0; i < total; i++)
            {
                Image pip = UiKit.Panel(pipsRoot, "Pip" + i, PipOn, false);
                pip.sprite = UiSprites.Circle;
                UiKit.SetAnchored(pip.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-(total - 1 - i) * spacing, 0f), new Vector2(38f, 38f));
                pips[i] = pip;
            }
        }

        private void OnShotsChanged(int left, int total)
        {
            if (pips.Length != total)
            {
                RebuildPips(total);
            }
            for (int i = 0; i < pips.Length; i++)
            {
                // 右端から使い切っていく(左に残る=まだ撃てる)
                bool available = i < left;
                pips[i].color = available ? PipOn : PipOff;
            }
        }

        private void OnScoreChanged(int added, int total)
        {
            if (spec == null || spec.ThreeStarScore <= 0)
            {
                return;
            }

            float t = Mathf.Clamp01(total / (float)spec.ThreeStarScore);
            fill.sizeDelta = new Vector2(BarWidth * t, 0f);

            if (!star2Lit && total >= spec.TwoStarScore)
            {
                star2Lit = true;
                LightStar(star2);
            }
            if (!star3Lit && total >= spec.ThreeStarScore)
            {
                star3Lit = true;
                LightStar(star3);
            }
        }

        private void LightStar(Image star)
        {
            star.color = StarOn;
            JuiceManager.Instance?.PlayStarNote(star == star2 ? 1 : 2);
            StartCoroutine(Pop(star.rectTransform));
        }

        private static IEnumerator Pop(RectTransform rt)
        {
            float elapsed = 0f;
            const float duration = 0.3f;
            while (elapsed < duration && rt != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float s = 1f + 0.7f * Mathf.Sin(t * Mathf.PI);
                rt.localScale = Vector3.one * s;
                yield return null;
            }
            if (rt != null)
            {
                rt.localScale = Vector3.one;
            }
        }

        private void OnAimChanged(bool aiming, Vector2 screenPosition, int predicted, bool danger, bool valid)
        {
            if (!aiming || !valid)
            {
                aimText.gameObject.SetActive(false);
                return;
            }

            aimText.gameObject.SetActive(true);
            aimText.text = danger ? "NG!" : "+" + predicted;
            aimText.color = danger ? BadColor : GoodColor;

            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition + new Vector2(0f, Screen.height * 0.12f), null, out local);
            aimRect.anchorMin = new Vector2(0.5f, 0.5f);
            aimRect.anchorMax = new Vector2(0.5f, 0.5f);
            aimRect.anchoredPosition = local;
        }
    }
}
