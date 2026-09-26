using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace OneTapDemolition
{
    /// <summary>
    /// プレイ中のHUD。ステージ・残りショット・ポイントゲージ(現在/MAX)・☆カウンター・コンボ表示と、
    /// 狙っている間の予測スコア(指の上+ゲージ上のゴースト)を出す。
    /// ゲージは「表示スコア」(壊した階から飛んできたポイントが着いた分)に連動するので、
    /// 壊す→ポイントが飛ぶ→着く→ゲージが伸びる、が一続きに見える。
    /// 進行ロジックは持たず、GameManager/ScoreManager/TapDemolishControllerのイベントを表示するだけ。
    /// </summary>
    public class StageHudUI : MonoBehaviour
    {
        private const float BarWidth = 640f;
        private const float BarHeight = 54f;
        private const float BarInset = 4f;

        private static readonly Color BarBg = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color FillNormal = new Color(0.35f, 0.8f, 1f, 1f);
        private static readonly Color FillMax = new Color(1f, 0.82f, 0.2f, 1f);
        private static readonly Color OvershootZone = new Color(1f, 0.85f, 0.3f, 0.22f);
        private static readonly Color StarOff = new Color(0.35f, 0.35f, 0.4f, 1f);
        private static readonly Color StarOn = new Color(1f, 0.85f, 0.2f, 1f);
        private static readonly Color PipOn = new Color(1f, 0.9f, 0.3f, 1f);
        private static readonly Color PipOff = new Color(0.25f, 0.25f, 0.28f, 0.9f);
        private static readonly Color GoodColor = new Color(0.6f, 1f, 0.55f, 1f);

        private RectTransform canvasRect;
        private RectTransform safeRoot;
        private RectTransform hudPanel;
        private Text stageText;
        private RectTransform pipsRoot;
        private Image[] pips = new Image[0];

        private RectTransform barRoot;
        private RectTransform fill;
        private Image fillImage;
        private RectTransform ghost;
        private RectTransform maxTick;
        private Text maxTickLabel;
        private Text gaugeLabel;
        private Image starMark;
        private Image flash;
        private readonly Image[] slots = new Image[3];
        private Text comboText;
        private Image comboStar;
        private Text aimText;
        private RectTransform aimRect;
        private readonly Image[] aimStars = new Image[3];
        private RectTransform guideLine;
        private RectTransform guideCapLeft;
        private RectTransform guideCapRight;
        private int pulseSlotCount;

        private StageSpec spec;
        private float shownScore;
        private int displayedScore;
        private float fillNormalized;
        private float ghostNormalized;
        private bool gaugeMaxed;
        private int starsAssigned;
        private int stageToken;
        private int comboStep;
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
                gm.StageEnded += OnStageEnded;
                gm.ShotsChanged += OnShotsChanged;
                gm.StarAwarded += OnStarAwarded;
                gm.GaugeMaxReached += OnGaugeMax;
                gm.ComboChanged += OnComboChanged;
                if (gm.CurrentSpec != null)
                {
                    OnStageStarted(gm.CurrentSpec, gm.ShotsLeft);
                    OnShotsChanged(gm.ShotsLeft, gm.ShotsLeft);
                }
            }

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.DisplayedScoreChanged += OnDisplayedScoreChanged;
            }

            controller = FindFirstObjectByType<TapDemolishController>();
            if (controller != null)
            {
                controller.AimChanged += OnAimChanged;
            }
        }

        private void OnDestroy()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                gm.StageStarted -= OnStageStarted;
                gm.StageEnded -= OnStageEnded;
                gm.ShotsChanged -= OnShotsChanged;
                gm.StarAwarded -= OnStarAwarded;
                gm.GaugeMaxReached -= OnGaugeMax;
                gm.ComboChanged -= OnComboChanged;
            }
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.DisplayedScoreChanged -= OnDisplayedScoreChanged;
            }
            if (controller != null)
            {
                controller.AimChanged -= OnAimChanged;
            }
        }

        // ---------------- Build ----------------

        private void BuildHud()
        {
            // スコア(シーン側のScoreCanvas)は上端中央の約180ユニットを使うので、その直下に並べる
            RectTransform panel = UiKit.NewRect("HudPanel", safeRoot);
            hudPanel = panel;
            UiKit.SetAnchored(panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -186f), new Vector2(BarWidth + 60f, 250f));

            stageText = UiKit.Label(panel, "StageText", "STAGE 1", 46, Color.white, TextAnchor.MiddleLeft);
            UiKit.SetAnchored(stageText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, 0f), new Vector2(300f, 60f));

            pipsRoot = UiKit.NewRect("Pips", panel);
            UiKit.SetAnchored(pipsRoot, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -6f), new Vector2(300f, 50f));

            BuildGauge(panel);
            BuildStarSlotsAndCombo(panel);

            aimText = UiKit.Label(canvasRect, "AimText", "", 84, GoodColor);
            aimRect = aimText.rectTransform;
            aimRect.sizeDelta = new Vector2(620f, 120f);
            aimText.gameObject.SetActive(false);

            BuildAimGuide();

            // 狙っている階で取れる☆を、指の上のラベルの下に☆アイコンで予告する
            for (int i = 0; i < aimStars.Length; i++)
            {
                Image star = UiKit.Panel(aimRect, "AimStar" + i, StarOn, false);
                star.sprite = UiSprites.Star;
                UiKit.SetAnchored(star.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2((i - 1) * 84f, -4f), new Vector2(76f, 76f));
                star.gameObject.SetActive(false);
                aimStars[i] = star;
            }
        }

        /// <summary>
        /// 狙っている階の下端に引く「切断ライン」。ビルより左右にはみ出して描くので、指で隠れても
        /// どの階を狙っているかが分かる。
        /// </summary>
        private void BuildAimGuide()
        {
            Color guideColor = new Color(1f, 0.95f, 0.45f, 0.95f);

            Image line = UiKit.Panel(canvasRect, "AimGuideLine", guideColor, false);
            guideLine = line.rectTransform;
            guideLine.sizeDelta = new Vector2(100f, 8f);

            Image left = UiKit.Panel(canvasRect, "AimGuideCapL", guideColor, false);
            left.sprite = UiSprites.Circle;
            guideCapLeft = left.rectTransform;
            guideCapLeft.sizeDelta = new Vector2(30f, 30f);

            Image right = UiKit.Panel(canvasRect, "AimGuideCapR", guideColor, false);
            right.sprite = UiSprites.Circle;
            guideCapRight = right.rectTransform;
            guideCapRight.sizeDelta = new Vector2(30f, 30f);

            SetGuideVisible(false);
        }

        private void SetGuideVisible(bool visible)
        {
            guideLine.gameObject.SetActive(visible);
            guideCapLeft.gameObject.SetActive(visible);
            guideCapRight.gameObject.SetActive(visible);
        }

        private void UpdateAimGuide(Floor floor)
        {
            Camera cam = Camera.main;
            if (floor == null || cam == null)
            {
                SetGuideVisible(false);
                return;
            }

            Bounds b = floor.GetComponent<Renderer>().bounds;
            Vector3 bottomCenter = new Vector3(b.center.x, b.min.y, b.center.z);
            Vector3 right = cam.transform.right;
            Vector3 a = cam.WorldToScreenPoint(bottomCenter - right * (b.extents.x + 1.2f));
            Vector3 c = cam.WorldToScreenPoint(bottomCenter + right * (b.extents.x + 1.2f));
            if (a.z <= 0f || c.z <= 0f)
            {
                SetGuideVisible(false);
                return;
            }

            SetGuideVisible(true);
            Vector2 delta = c - a;
            guideLine.position = (a + c) * 0.5f;
            guideLine.sizeDelta = new Vector2(delta.magnitude, 8f);
            guideLine.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            guideCapLeft.position = a;
            guideCapRight.position = c;
        }

        private void BuildGauge(RectTransform panel)
        {
            barRoot = UiKit.NewRect("Gauge", panel);
            UiKit.SetAnchored(barRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(BarWidth, BarHeight));
            Image bg = barRoot.gameObject.AddComponent<Image>();
            bg.sprite = UiSprites.RoundedRect;
            bg.type = Image.Type.Sliced;
            bg.color = BarBg;
            bg.raycastTarget = false;

            // MAXから☆マークまでは「ここまで伸ばすと☆」の帯として薄い金色にする
            Image zone = UiKit.Panel(barRoot, "OvershootZone", OvershootZone, false);
            zone.rectTransform.anchorMin = new Vector2(0f, 0f);
            zone.rectTransform.anchorMax = new Vector2(0f, 1f);
            zone.rectTransform.pivot = new Vector2(0f, 0.5f);
            zoneRect = zone.rectTransform;

            Image ghostImage = UiKit.Panel(barRoot, "Ghost", new Color(1f, 1f, 1f, 0.35f));
            ghost = ghostImage.rectTransform;
            SetupFillRect(ghost);

            fillImage = UiKit.Panel(barRoot, "Fill", FillNormal);
            fill = fillImage.rectTransform;
            SetupFillRect(fill);

            Image tick = UiKit.Panel(barRoot, "MaxTick", Color.white, false);
            maxTick = tick.rectTransform;
            UiKit.SetAnchored(maxTick, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, BarHeight + 16f));
            maxTickLabel = UiKit.Label(maxTick, "MaxLabel", "MAX", 28, Color.white);
            UiKit.SetAnchored(maxTickLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(120f, 34f));

            gaugeLabel = UiKit.Label(barRoot, "GaugeLabel", "0 / 0", 36, Color.white);
            gaugeLabel.rectTransform.anchorMin = Vector2.zero;
            gaugeLabel.rectTransform.anchorMax = Vector2.one;
            gaugeLabel.rectTransform.sizeDelta = Vector2.zero;

            starMark = UiKit.Panel(barRoot, "StarMark", StarOff, false);
            starMark.sprite = UiSprites.Star;
            UiKit.SetAnchored(starMark.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 70f));

            flash = UiKit.Panel(barRoot, "Flash", new Color(1f, 1f, 1f, 0f));
            flash.rectTransform.anchorMin = Vector2.zero;
            flash.rectTransform.anchorMax = Vector2.one;
            flash.rectTransform.sizeDelta = Vector2.zero;
        }

        private RectTransform zoneRect;

        private static void SetupFillRect(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(BarInset, 0f);
            rt.sizeDelta = new Vector2(0f, -BarInset * 2f);
        }

        private void BuildStarSlotsAndCombo(RectTransform panel)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                Image slot = UiKit.Panel(panel, "StarSlot" + i, StarOff, false);
                slot.sprite = UiSprites.Star;
                UiKit.SetAnchored(slot.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f + i * 66f, -172f), new Vector2(60f, 60f));
                slots[i] = slot;
            }

            comboText = UiKit.Label(panel, "ComboText", "COMBO", 42, Color.white, TextAnchor.MiddleRight);
            UiKit.SetAnchored(comboText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-110f, -178f), new Vector2(360f, 56f));

            comboStar = UiKit.Panel(panel, "ComboStar", StarOff, false);
            comboStar.sprite = UiSprites.Star;
            UiKit.SetAnchored(comboStar.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -170f), new Vector2(68f, 68f));
            Text five = UiKit.Label(comboStar.rectTransform, "Five", ScoreRules.ComboStarStep.ToString(), 32, Color.white);
            five.rectTransform.anchorMin = Vector2.zero;
            five.rectTransform.anchorMax = Vector2.one;
            five.rectTransform.sizeDelta = Vector2.zero;
        }

        // ---------------- Stage / shots ----------------

        private void OnStageEnded(StageResult result)
        {
            // リザルトが前面に出るので、ゲージ/コンボ等のHUDは畳んで重なりを避ける
            hudPanel.gameObject.SetActive(false);
            aimText.gameObject.SetActive(false);
            SetGuideVisible(false);
            pulseSlotCount = 0;
        }

        private void OnStageStarted(StageSpec newSpec, int shots)
        {
            hudPanel.gameObject.SetActive(true);
            stageToken++;
            spec = newSpec;
            displayedScore = 0;
            shownScore = 0f;
            fillNormalized = 0f;
            ghostNormalized = 0f;
            gaugeMaxed = false;
            starsAssigned = 0;
            comboStep = 0;

            stageText.text = "STAGE " + newSpec.StageIndex;
            starMark.color = StarOff;
            comboStar.color = StarOff;
            comboStar.rectTransform.localScale = Vector3.one;
            foreach (Image slot in slots)
            {
                slot.color = StarOff;
                slot.rectTransform.localScale = Vector3.one;
            }
            fillImage.color = FillNormal;
            barRoot.localScale = Vector3.one;
            comboText.text = "COMBO";

            float targetNorm = TargetNormalized();
            maxTick.anchoredPosition = new Vector2(BarInset + targetNorm * (BarWidth - BarInset * 2f), 0f);
            zoneRect.anchoredPosition = new Vector2(BarInset + targetNorm * (BarWidth - BarInset * 2f), 0f);
            zoneRect.sizeDelta = new Vector2((1f - targetNorm) * (BarWidth - BarInset * 2f), -BarInset * 2f);
            ghost.sizeDelta = new Vector2(0f, -BarInset * 2f);
            fill.sizeDelta = new Vector2(0f, -BarInset * 2f);
            aimText.gameObject.SetActive(false);
            pulseSlotCount = 0;
            UpdateGaugeLabel();
            RebuildPips(shots);
        }

        private float TargetNormalized()
        {
            return spec != null && spec.StarScore > 0 ? Mathf.Clamp01(spec.TargetScore / (float)spec.StarScore) : 0.66f;
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
                pips[i].color = i < left ? PipOn : PipOff;
            }
        }

        // ---------------- Gauge ----------------

        private void OnDisplayedScoreChanged(int added, int displayed)
        {
            displayedScore = displayed;
            if (added > 0)
            {
                StartCoroutine(Pop(barRoot, 1.06f, 0.12f));
            }
        }

        private void Update()
        {
            if (spec == null || spec.StarScore <= 0)
            {
                return;
            }

            float k = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
            shownScore = Mathf.Lerp(shownScore, displayedScore, k);
            if (Mathf.Abs(displayedScore - shownScore) < 1f)
            {
                shownScore = displayedScore;
            }

            float targetNorm = Mathf.Clamp01(shownScore / spec.StarScore);
            fillNormalized = targetNorm;
            float maxWidth = BarWidth - BarInset * 2f;
            fill.sizeDelta = new Vector2(fillNormalized * maxWidth, -BarInset * 2f);
            ghost.sizeDelta = new Vector2(Mathf.Max(fillNormalized, ghostNormalized) * maxWidth, -BarInset * 2f);

            bool reachedMax = shownScore >= spec.TargetScore - 0.5f && spec.TargetScore > 0;
            Color wantColor = reachedMax ? FillMax : FillNormal;
            fillImage.color = Color.Lerp(fillImage.color, wantColor, Mathf.Clamp01(Time.unscaledDeltaTime * 10f));
            UpdateGaugeLabel();

            // 今の狙いで取れる☆の数だけ、☆カウンターの次の空きスロットを脈動させて「ここに入る」と示す
            for (int i = 0; i < slots.Length; i++)
            {
                bool wouldFill = i >= starsAssigned && i < starsAssigned + pulseSlotCount;
                if (slots[i].color == StarOn)
                {
                    continue;
                }
                if (wouldFill)
                {
                    float p = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f);
                    slots[i].color = Color.Lerp(StarOff, StarOn, 0.35f + 0.5f * p);
                    slots[i].rectTransform.localScale = Vector3.one * (1f + 0.25f * p);
                }
                else
                {
                    slots[i].color = StarOff;
                    slots[i].rectTransform.localScale = Vector3.one;
                }
            }

            // 連鎖があと1で☆のとき、☆アイコンを脈動させて予兆を出す
            if (comboStep == ScoreRules.ComboStarStep - 1 && comboStar.color == StarOff)
            {
                float s = 1f + 0.18f * Mathf.Sin(Time.unscaledTime * 16f);
                comboStar.rectTransform.localScale = Vector3.one * s;
            }
        }

        private void UpdateGaugeLabel()
        {
            if (spec == null)
            {
                return;
            }
            gaugeLabel.text = gaugeMaxed ? "MAX!  " + Mathf.RoundToInt(shownScore) : Mathf.RoundToInt(shownScore) + " / " + spec.TargetScore;
        }

        private void OnGaugeMax()
        {
            gaugeMaxed = true;
            StartCoroutine(Pop(barRoot, 1.14f, 0.28f));
            StartCoroutine(FlashRoutine());
            ScoreFeedbackUI.Instance?.ShowBanner("MAX!", FillMax);
            JuiceManager.Instance?.PlayStarNote(2);
        }

        private IEnumerator FlashRoutine()
        {
            float elapsed = 0f;
            const float duration = 0.4f;
            while (elapsed < duration && flash != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                flash.color = new Color(1f, 1f, 1f, 0.8f * (1f - t));
                yield return null;
            }
            if (flash != null)
            {
                flash.color = new Color(1f, 1f, 1f, 0f);
            }
        }

        // ---------------- Combo ----------------

        private void OnComboChanged(int step)
        {
            comboStep = step;
            comboText.text = step > 0 ? "COMBO " + step : "COMBO";
            if (step > 0)
            {
                StartCoroutine(Pop(comboText.rectTransform, 1.25f, 0.14f));
            }
            else if (comboStar.color == StarOff)
            {
                comboStar.rectTransform.localScale = Vector3.one;
            }
        }

        // ---------------- Stars ----------------

        private void OnStarAwarded(StarReason reason, Vector3? worldPosition)
        {
            Vector3 start;
            switch (reason)
            {
                case StarReason.BonusFloor:
                    start = Camera.main != null && worldPosition.HasValue
                        ? Camera.main.WorldToScreenPoint(worldPosition.Value)
                        : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
                    break;
                case StarReason.Combo:
                    comboStar.color = StarOn;
                    comboStar.rectTransform.localScale = Vector3.one;
                    start = comboStar.rectTransform.position;
                    break;
                default:
                    starMark.color = StarOn;
                    start = starMark.rectTransform.position;
                    break;
            }

            int slotIndex = Mathf.Min(starsAssigned, slots.Length - 1);
            starsAssigned++;
            StartCoroutine(StarFlight(start, slotIndex, stageToken));
        }

        /// <summary>
        /// ☆が条件を満たした場所(ボーナス階/コンボ表示/ゲージの☆マーク)から弾け出て、☆カウンターへ飛ぶ。
        /// </summary>
        private IEnumerator StarFlight(Vector3 start, int slotIndex, int token)
        {
            RectTransform rt = UiKit.NewRect("FlyingStar", canvasRect);
            rt.sizeDelta = new Vector2(100f, 100f);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = UiSprites.Star;
            image.color = StarOn;
            image.raycastTarget = false;
            rt.position = start;

            // 1) 弾け出る
            const float popDuration = 0.24f;
            float elapsed = 0f;
            while (elapsed < popDuration && rt != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / popDuration);
                rt.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.7f, EaseOutBack(t));
                rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-40f, 0f, t));
                yield return null;
            }

            yield return new WaitForSecondsRealtime(0.16f);
            if (rt == null)
            {
                yield break;
            }

            // 2) ☆カウンターへ飛ぶ
            Vector3 target = slots[slotIndex].rectTransform.position;
            Vector3 from = rt.position;
            const float flyDuration = 0.55f;
            elapsed = 0f;
            while (elapsed < flyDuration && rt != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / flyDuration);
                float e = t * t * (3f - 2f * t);
                rt.position = Vector3.Lerp(from, target, e);
                rt.localScale = Vector3.one * Mathf.Lerp(1.7f, 0.65f, e);
                yield return null;
            }

            if (rt != null)
            {
                Destroy(rt.gameObject);
            }

            // 3) 着いた: カウンターが点く
            if (token == stageToken)
            {
                slots[slotIndex].color = StarOn;
                StartCoroutine(Pop(slots[slotIndex].rectTransform, 1.6f, 0.3f));
                JuiceManager.Instance?.PlayStarNote(slotIndex);
            }
        }

        // ---------------- Aim ----------------

        private void OnAimChanged(bool aiming, Vector2 screenPosition, int tapIndex, int predicted)
        {
            GameManager gm = GameManager.Instance;
            if (!aiming || tapIndex < 0 || spec == null || gm == null || gm.CurrentTower == null)
            {
                SetGuideVisible(false);
                aimText.gameObject.SetActive(false);
                ghostNormalized = 0f;
                pulseSlotCount = 0;
                return;
            }

            bool reachesMax = displayedScore + predicted >= spec.TargetScore;
            int alive = gm.CurrentTower.AliveCount;
            UpdateAimGuide(gm.CurrentTower.Floors[tapIndex]);

            // この一撃で取れる☆(まだ取っていない条件だけ)
            bool bonus = !gm.HasStar(StarReason.BonusFloor) && ScoreRules.ChainIncludesBonus(spec.Floors, tapIndex, alive);
            bool combo = !gm.HasStar(StarReason.Combo) && alive - tapIndex >= ScoreRules.ComboStarStep;
            bool overshoot = !gm.HasStar(StarReason.Overshoot) && displayedScore + predicted >= spec.StarScore;
            int starCount = (bonus ? 1 : 0) + (combo ? 1 : 0) + (overshoot ? 1 : 0);
            pulseSlotCount = starCount;

            aimText.gameObject.SetActive(true);
            aimText.text = "+" + predicted + (reachesMax ? "  MAX!" : "");
            aimText.color = reachesMax ? FillMax : GoodColor;
            for (int i = 0; i < aimStars.Length; i++)
            {
                aimStars[i].gameObject.SetActive(i < starCount);
                // 表示する☆をラベルの真下で左右対称に並べる
                aimStars[i].rectTransform.anchoredPosition = new Vector2((i - (starCount - 1) * 0.5f) * 84f, -4f);
            }
            ghostNormalized = Mathf.Clamp01((displayedScore + predicted) / (float)spec.StarScore);

            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition + new Vector2(0f, Screen.height * 0.12f), null, out local);
            aimRect.anchorMin = new Vector2(0.5f, 0.5f);
            aimRect.anchorMax = new Vector2(0.5f, 0.5f);
            aimRect.anchoredPosition = local;
        }

        // ---------------- Helpers ----------------

        private static IEnumerator Pop(RectTransform rt, float peak, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && rt != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                rt.localScale = Vector3.one * (1f + (peak - 1f) * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            if (rt != null)
            {
                rt.localScale = Vector3.one;
            }
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }
}
