using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace OneTapDemolition
{
    /// <summary>
    /// プレイ中のHUD。ステージ・残りショット・ポイントゲージ(現在/MAX)・☆カウンター・コンボ表示と、
    /// 狙っている間の予測スコア(指の上+ゲージ上のゴースト+この一撃で取れる☆)を出す。
    /// ☆の3条件は、すべて画面上に印がある: ゲージ上のMAXの☆、ゲージ右端の☆、建物のボーナス階の☆。
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
        private RectTransform zoneRect;
        private RectTransform maxTick;
        private Image maxStarMark;
        private Text gaugeLabel;
        private Image endStarMark;
        private Image flash;
        private readonly Image[] slots = new Image[3];
        private Text comboText;
        private Text aimText;
        private RectTransform aimRect;
        private readonly Image[] aimStars = new Image[3];
        private RectTransform guideLine;
        private RectTransform guideCapLeft;
        private RectTransform guideCapRight;
        private Button finishButton;
        private Button hintButton;
        private RectTransform hintLine;
        private RectTransform hintCapLeft;
        private RectTransform hintCapRight;
        private Text hintText;
        private int hintIndex = -1;
        private int hintStage;
        private int pulseSlotCount;

        public static StageHudUI Instance { get; private set; }

        private StageSpec spec;
        private float shownScore;
        private int displayedScore;
        private float ghostNormalized;
        private bool gaugeMaxed;
        private bool fullScoreAnnounced;
        private int starsAssigned;
        private int stageToken;
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

        /// <summary>
        /// 飛んできた「+N」が着く位置(ゲージの塗りの先頭)。上部のスコア数字を廃止したので、ここへ吸い込まれる。
        /// </summary>
        public Vector3 ScoreLandingPosition()
        {
            if (fill == null || !hudPanel.gameObject.activeInHierarchy)
            {
                return new Vector3(Screen.width * 0.5f, Screen.height * 0.9f, 0f);
            }
            Vector3 p = fill.position;
            p.x += Mathf.Max(30f, fill.sizeDelta.x) * fill.lossyScale.x;
            return p;
        }

        private void Start()
        {
            Instance = this;
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
                gm.StateChanged += OnStateChanged;
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
            if (Instance == this)
            {
                Instance = null;
            }
            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                gm.StageStarted -= OnStageStarted;
                gm.StageEnded -= OnStageEnded;
                gm.ShotsChanged -= OnShotsChanged;
                gm.StarAwarded -= OnStarAwarded;
                gm.GaugeMaxReached -= OnGaugeMax;
                gm.ComboChanged -= OnComboChanged;
                gm.StateChanged -= OnStateChanged;
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
            // 上端に置く(以前は上端中央の大きなスコア数字の直下だったが、ゲージに現在値/最大値があるので数字は廃止)
            RectTransform panel = UiKit.NewRect("HudPanel", safeRoot);
            hudPanel = panel;
            UiKit.SetAnchored(panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(BarWidth + 60f, 250f));

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

            // MAXに届いたあと、残りのショットを使わずに終えるボタン
            finishButton = UiKit.MakeButton(safeRoot, "FinishButton", "FINISH", new Color(0.25f, 0.55f, 0.95f, 1f), new Vector2(360f, 110f), OnFinishClicked);
            UiKit.SetAnchored(finishButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(360f, 110f));
            finishButton.gameObject.SetActive(false);

            // リワード広告を見ると、最大得点を出すための一手を教えてくれる
            hintButton = UiKit.MakeButton(safeRoot, "HintButton", "▶ ヒント", new Color(0.55f, 0.35f, 0.9f, 1f), new Vector2(300f, 100f), OnHintClicked);
            UiKit.SetAnchored(hintButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(300f, 100f));
            hintButton.gameObject.SetActive(false);

            Color hintColor = new Color(0.55f, 1f, 0.6f, 0.95f);
            Image hl = UiKit.Panel(canvasRect, "HintLine", hintColor, false);
            hintLine = hl.rectTransform;
            hintLine.sizeDelta = new Vector2(100f, 10f);
            Image hcl = UiKit.Panel(canvasRect, "HintCapL", hintColor, false);
            hcl.sprite = UiSprites.Circle;
            hintCapLeft = hcl.rectTransform;
            hintCapLeft.sizeDelta = new Vector2(34f, 34f);
            Image hcr = UiKit.Panel(canvasRect, "HintCapR", hintColor, false);
            hcr.sprite = UiSprites.Circle;
            hintCapRight = hcr.rectTransform;
            hintCapRight.sizeDelta = new Vector2(34f, 34f);
            hintText = UiKit.Label(canvasRect, "HintText", "", 44, new Color(0.7f, 1f, 0.7f, 1f), TextAnchor.MiddleLeft);
            hintText.rectTransform.sizeDelta = new Vector2(360f, 130f);
            SetHintVisible(false);
        }

        private void SetHintVisible(bool visible)
        {
            hintLine.gameObject.SetActive(visible);
            hintCapLeft.gameObject.SetActive(visible);
            hintCapRight.gameObject.SetActive(visible);
            hintText.gameObject.SetActive(visible);
        }

        private void ClearHint()
        {
            hintIndex = -1;
            SetHintVisible(false);
        }

        private void RefreshHintButton()
        {
            GameManager gm = GameManager.Instance;
            bool can = gm != null && gm.State == GameState.Playing && gm.ShotsLeft > 0 && gm.CurrentTower != null
                && gm.CurrentTower.AliveCount > 0 && hintIndex < 0 && AdsManager.Instance != null && AdsManager.Instance.IsRewardedReady;
            hintButton.gameObject.SetActive(can);
        }

        private void OnHintClicked()
        {
            JuiceManager.Instance?.PlayUiClick();
            AdsManager.Instance?.ShowRewarded(GrantHint);
        }

        /// <summary>
        /// 今の状態から最大得点になる一手目(今の残りショット・残りの階で総当たり)を印で示す。次のショットを撃つまで出ている。
        /// </summary>
        private void GrantHint()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing || gm.CurrentTower == null || spec == null || gm.ShotsLeft <= 0)
            {
                return;
            }

            float historyBonus = ScoreManager.Instance != null ? ScoreManager.Instance.HistoryBonus : 0f;
            int first;
            int bestFromHere = ScoreRules.BestScore(spec.Floors, gm.CurrentTower.AliveCount, gm.ShotsLeft, historyBonus, out first);
            if (first < 0)
            {
                return;
            }

            hintIndex = first;
            hintStage = stageToken;
            int current = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0;
            int reachable = current + bestFromHere;
            hintText.text = reachable >= spec.OptimalScore ? "ここ!\n満点コース" : "ここ!\n最大 " + reachable;
            hintButton.gameObject.SetActive(false);
            JuiceManager.Instance?.PlayStarNote(1);
        }

        private void UpdateHint()
        {
            GameManager gm = GameManager.Instance;
            Camera cam = Camera.main;
            if (hintIndex < 0 || gm == null || gm.CurrentTower == null || cam == null || hintStage != stageToken || hintIndex >= gm.CurrentTower.Floors.Count)
            {
                if (hintIndex >= 0)
                {
                    ClearHint();
                }
                return;
            }

            Floor floor = gm.CurrentTower.Floors[hintIndex];
            if (floor == null)
            {
                ClearHint();
                return;
            }

            // 狙うときの切断ラインと同じく、階の下端に線を引く(ここから上が崩れる)
            Bounds b = floor.GetComponent<Renderer>().bounds;
            Vector3 mid = new Vector3(b.center.x, b.min.y, b.center.z);
            Vector3 right = cam.transform.right;
            Vector3 a = cam.WorldToScreenPoint(mid - right * (b.extents.x + 1.4f));
            Vector3 c = cam.WorldToScreenPoint(mid + right * (b.extents.x + 1.4f));
            if (a.z <= 0f || c.z <= 0f)
            {
                SetHintVisible(false);
                return;
            }

            SetHintVisible(true);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
            Vector2 delta = c - a;
            hintLine.position = (a + c) * 0.5f;
            hintLine.sizeDelta = new Vector2(delta.magnitude, 8f + 6f * pulse);
            hintLine.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            hintCapLeft.position = a;
            hintCapRight.position = c;
            hintCapLeft.localScale = Vector3.one * (1f + 0.35f * pulse);
            hintCapRight.localScale = Vector3.one * (1f + 0.35f * pulse);

            // ラベルは線の右端の外側(建物に重ならない)。画面からはみ出さないよう収める
            float scale = canvasRect.lossyScale.x;
            float width = hintText.rectTransform.sizeDelta.x * scale;
            float left = Mathf.Min(c.x + 30f * scale, Screen.width - width);
            hintText.rectTransform.pivot = new Vector2(0f, 0.5f);
            hintText.rectTransform.position = new Vector3(left, c.y + 60f * scale, 0f);
            hintText.color = Color.Lerp(new Color(0.7f, 1f, 0.7f, 1f), Color.white, pulse);
        }

        private void OnFinishClicked()
        {
            GameManager.Instance?.FinishStage();
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

            // クリアの印から右側の☆までは「ここまで伸ばすと☆」の帯として薄い金色にする
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

            // ゲージ上のクリアの位置に☆を置く(届くとクリア確定+☆がもらえる)
            Image tick = UiKit.Panel(barRoot, "MaxTick", Color.white, false);
            maxTick = tick.rectTransform;
            UiKit.SetAnchored(maxTick, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, BarHeight + 16f));

            gaugeLabel = UiKit.Label(barRoot, "GaugeLabel", "0 / 0", 36, Color.white, TextAnchor.MiddleLeft);
            gaugeLabel.rectTransform.anchorMin = Vector2.zero;
            gaugeLabel.rectTransform.anchorMax = Vector2.one;
            gaugeLabel.rectTransform.offsetMin = new Vector2(24f, 0f);
            gaugeLabel.rectTransform.offsetMax = Vector2.zero;

            maxStarMark = UiKit.Panel(barRoot, "MaxStarMark", StarOff, false);
            maxStarMark.sprite = UiSprites.Star;
            UiKit.SetAnchored(maxStarMark.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 70f));

            endStarMark = UiKit.Panel(barRoot, "EndStarMark", StarOff, false);
            endStarMark.sprite = UiSprites.Star;
            UiKit.SetAnchored(endStarMark.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 70f));

            flash = UiKit.Panel(barRoot, "Flash", new Color(1f, 1f, 1f, 0f));
            flash.rectTransform.anchorMin = Vector2.zero;
            flash.rectTransform.anchorMax = Vector2.one;
            flash.rectTransform.sizeDelta = Vector2.zero;
        }

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

            comboText = UiKit.Label(panel, "ComboText", "", 42, Color.white, TextAnchor.MiddleRight);
            UiKit.SetAnchored(comboText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -178f), new Vector2(360f, 56f));
        }

        // ---------------- Stage / shots ----------------

        private void OnStageStarted(StageSpec newSpec, int shots)
        {
            hudPanel.gameObject.SetActive(true);
            stageToken++;
            ClearHint();
            spec = newSpec;
            displayedScore = 0;
            shownScore = 0f;
            ghostNormalized = 0f;
            gaugeMaxed = false;
            fullScoreAnnounced = false;
            starsAssigned = 0;
            pulseSlotCount = 0;

            stageText.text = "STAGE " + newSpec.StageIndex;
            endStarMark.color = StarOff;
            maxStarMark.color = StarOff;
            foreach (Image slot in slots)
            {
                slot.color = StarOff;
                slot.rectTransform.localScale = Vector3.one;
            }
            fillImage.color = FillNormal;
            barRoot.localScale = Vector3.one;
            comboText.text = "";

            float targetNorm = TargetNormalized();
            float maxWidth = BarWidth - BarInset * 2f;
            maxTick.anchoredPosition = new Vector2(BarInset + targetNorm * maxWidth, 0f);
            maxStarMark.rectTransform.anchoredPosition = new Vector2(BarInset + targetNorm * maxWidth, 0f);
            zoneRect.anchoredPosition = new Vector2(BarInset + targetNorm * maxWidth, 0f);
            float starNorm = StarNormalized();
            zoneRect.sizeDelta = new Vector2(Mathf.Max(0f, starNorm - targetNorm) * maxWidth, -BarInset * 2f);
            endStarMark.rectTransform.anchoredPosition = new Vector2(BarInset + starNorm * maxWidth, 0f);
            ghost.sizeDelta = new Vector2(0f, -BarInset * 2f);
            fill.sizeDelta = new Vector2(0f, -BarInset * 2f);
            aimText.gameObject.SetActive(false);
            finishButton.gameObject.SetActive(false);
            SetGuideVisible(false);
            UpdateGaugeLabel();
            RebuildPips(shots);
        }

        private void OnStageEnded(StageResult result)
        {
            // リザルトが前面に出るので、ゲージ/コンボ等のHUDは畳んで重なりを避ける
            hudPanel.gameObject.SetActive(false);
            aimText.gameObject.SetActive(false);
            finishButton.gameObject.SetActive(false);
            hintButton.gameObject.SetActive(false);
            ClearHint();
            SetGuideVisible(false);
            pulseSlotCount = 0;
        }

        private void OnStateChanged(GameState state)
        {
            RefreshHintButton();
            // MAXに届いたあと、次のショットを待っている間だけFINISHを出す
            finishButton.gameObject.SetActive(gaugeMaxed && state == GameState.Playing);
        }

        /// <summary>
        /// ゲージの右端(=母数)は、そのステージで出せる最大得点。クリアの印と右側の☆はその途中に置く。
        /// </summary>
        private float GaugeTotal()
        {
            if (spec == null)
            {
                return 1f;
            }
            return Mathf.Max(1, spec.OptimalScore, spec.StarScore);
        }

        private float TargetNormalized()
        {
            return spec != null ? Mathf.Clamp01(spec.TargetScore / GaugeTotal()) : 0.6f;
        }

        private float StarNormalized()
        {
            return spec != null ? Mathf.Clamp01(spec.StarScore / GaugeTotal()) : 0.75f;
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
            // ショットを撃ったら、その一手のヒントは終わり(次の一手はまたヒントを見られる)
            ClearHint();
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
            if (!fullScoreAnnounced && spec != null && displayed > 0 && displayed >= GaugeTotal() - 0.5f)
            {
                // ゲージが満タン(そのステージの最大得点)に届いた
                fullScoreAnnounced = true;
                ScoreFeedbackUI.Instance?.ShowBanner("FULL SCORE!", FillMax);
            }
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

            RefreshHintButton();
            UpdateHint();

            float k = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
            shownScore = Mathf.Lerp(shownScore, displayedScore, k);
            if (Mathf.Abs(displayedScore - shownScore) < 1f)
            {
                shownScore = displayedScore;
            }

            float fillNormalized = Mathf.Clamp01(shownScore / GaugeTotal());
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
                if (slots[i].color == StarOn)
                {
                    continue;
                }

                bool wouldFill = i >= starsAssigned && i < starsAssigned + pulseSlotCount;
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
        }

        private void UpdateGaugeLabel()
        {
            if (spec == null)
            {
                return;
            }
            gaugeLabel.text = Mathf.RoundToInt(shownScore) + " / " + Mathf.RoundToInt(GaugeTotal());
        }

        private void OnGaugeMax()
        {
            gaugeMaxed = true;
            StartCoroutine(Pop(barRoot, 1.14f, 0.28f));
            StartCoroutine(FlashRoutine());
            ScoreFeedbackUI.Instance?.ShowBanner("CLEAR!", FillMax);
            finishButton.gameObject.SetActive(GameManager.Instance != null && GameManager.Instance.State == GameState.Playing);
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
            comboText.text = step > 0 ? "COMBO " + step : "";
            if (step > 0)
            {
                StartCoroutine(Pop(comboText.rectTransform, 1.25f, 0.14f));
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
                case StarReason.Max:
                    maxStarMark.color = StarOn;
                    start = maxStarMark.rectTransform.position;
                    break;
                default:
                    endStarMark.color = StarOn;
                    start = endStarMark.rectTransform.position;
                    break;
            }

            int slotIndex = Mathf.Min(starsAssigned, slots.Length - 1);
            starsAssigned++;
            StartCoroutine(StarFlight(start, slotIndex, stageToken));
        }

        /// <summary>
        /// ☆が条件を満たした場所(ボーナス階/ゲージのMAX/ゲージ右端)から弾け出て、☆カウンターへ飛ぶ。
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
            bool max = !gm.HasStar(StarReason.Max) && reachesMax;
            bool overshoot = !gm.HasStar(StarReason.Overshoot) && displayedScore + predicted >= spec.StarScore;
            int starCount = (bonus ? 1 : 0) + (max ? 1 : 0) + (overshoot ? 1 : 0);
            pulseSlotCount = starCount;

            aimText.gameObject.SetActive(true);
            aimText.text = "+" + predicted + (reachesMax ? "  CLEAR!" : "");
            aimText.color = reachesMax ? FillMax : GoodColor;
            for (int i = 0; i < aimStars.Length; i++)
            {
                aimStars[i].gameObject.SetActive(i < starCount);
                // 表示する☆をラベルの真下で左右対称に並べる
                aimStars[i].rectTransform.anchoredPosition = new Vector2((i - (starCount - 1) * 0.5f) * 84f, -4f);
            }
            ghostNormalized = Mathf.Clamp01((displayedScore + predicted) / GaugeTotal());

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
