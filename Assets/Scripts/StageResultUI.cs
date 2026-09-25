using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace OneTapDemolition
{
    /// <summary>
    /// 開始画面(PLAY)とリザルト(星・スコア・次へ/リトライ/動画で+1ショット)。
    /// 進行はGameManagerに任せ、ここはイベントを受けて表示とボタン入力を担当する。
    /// リザルトからは1タップで次のプレイに入れる(NEXT/RETRYがどちらも大きい主ボタン)。
    /// </summary>
    public class StageResultUI : MonoBehaviour
    {
        private static readonly Color Green = new Color(0.25f, 0.75f, 0.35f, 1f);
        private static readonly Color Orange = new Color(0.95f, 0.55f, 0.15f, 1f);
        private static readonly Color Purple = new Color(0.6f, 0.35f, 0.9f, 1f);
        private static readonly Color Blue = new Color(0.25f, 0.55f, 0.95f, 1f);
        private static readonly Color StarOff = new Color(0.3f, 0.3f, 0.35f, 1f);
        private static readonly Color StarOn = new Color(1f, 0.85f, 0.2f, 1f);

        private RectTransform safeRoot;

        private GameObject readyPanel;
        private Text readyInfo;

        private GameObject resultPanel;
        private Text resultTitle;
        private Text scoreText;
        private Text bestText;
        private readonly Image[] stars = new Image[3];
        private Image unlockIcon;
        private Text unlockText;
        private Button primaryButton;
        private Text primaryLabel;
        private Image primaryImage;
        private Button retryButton;
        private Button rewardButton;

        private StageResult lastResult;
        private Coroutine resultRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<StageResultUI>() != null)
            {
                return;
            }
            new GameObject(nameof(StageResultUI)).AddComponent<StageResultUI>();
        }

        private void Start()
        {
            Canvas canvas = UiKit.CreateCanvas("StageResultCanvas", 20);
            safeRoot = UiKit.CreateSafeRoot(canvas);
            BuildReadyPanel();
            BuildResultPanel();

            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                return;
            }

            gm.StageStarted += OnStageStarted;
            gm.StageEnded += OnStageEnded;
            gm.StateChanged += OnStateChanged;

            if (gm.CurrentSpec != null)
            {
                OnStageStarted(gm.CurrentSpec, gm.ShotsLeft);
                OnStateChanged(gm.State);
            }
        }

        private void OnDestroy()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                return;
            }
            gm.StageStarted -= OnStageStarted;
            gm.StageEnded -= OnStageEnded;
            gm.StateChanged -= OnStateChanged;
        }

        // ---------------- Ready ----------------

        private void BuildReadyPanel()
        {
            RectTransform root = UiKit.NewRect("ReadyPanel", safeRoot);
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(1f, 0.2f);
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            readyPanel = root.gameObject;

            readyInfo = UiKit.Label(root, "Info", "", 40, new Color(1f, 1f, 1f, 0.95f));
            UiKit.SetAnchored(readyInfo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(1000f, 60f));

            Button play = UiKit.MakeButton(root, "PlayButton", "PLAY", Green, new Vector2(520f, 140f), OnPlayClicked);
            UiKit.SetAnchored(play.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(520f, 140f));
            play.gameObject.AddComponent<PulseScale>();
        }

        private void OnPlayClicked()
        {
            GameManager.Instance?.BeginPlay();
        }

        private void OnStageStarted(StageSpec spec, int shots)
        {
            readyInfo.text = "押して狙う → 離して崩す    ショット " + shots;
        }

        private void OnStateChanged(GameState state)
        {
            readyPanel.SetActive(state == GameState.Ready);
            if (state != GameState.Result)
            {
                HideResult();
            }
        }

        // ---------------- Result ----------------

        private void BuildResultPanel()
        {
            RectTransform root = UiKit.NewRect("ResultPanel", safeRoot);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            resultPanel = root.gameObject;

            Image dim = UiKit.Panel(root, "Dim", new Color(0f, 0f, 0f, 0.6f), false);
            dim.raycastTarget = true;
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.sizeDelta = Vector2.zero;
            dim.rectTransform.offsetMin = new Vector2(-2000f, -2000f);
            dim.rectTransform.offsetMax = new Vector2(2000f, 2000f);

            resultTitle = UiKit.Label(root, "Title", "", 100, Color.white);
            UiKit.SetAnchored(resultTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 520f), new Vector2(950f, 130f));

            for (int i = 0; i < 3; i++)
            {
                Image star = UiKit.Panel(root, "Star" + i, StarOff, false);
                star.sprite = UiSprites.Star;
                UiKit.SetAnchored(star.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2((i - 1) * 230f, i == 1 ? 340f : 310f), new Vector2(210f, 210f));
                stars[i] = star;
            }

            scoreText = UiKit.Label(root, "Score", "0", 96, Color.white);
            UiKit.SetAnchored(scoreText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 130f), new Vector2(800f, 150f));

            bestText = UiKit.Label(root, "Best", "", 40, new Color(1f, 0.9f, 0.4f, 1f));
            UiKit.SetAnchored(bestText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(800f, 60f));

            unlockIcon = UiKit.Panel(root, "UnlockIcon", Color.white, false);
            UiKit.SetAnchored(unlockIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-330f, -40f), new Vector2(110f, 110f));
            unlockText = UiKit.Label(root, "UnlockText", "", 34, new Color(1f, 0.95f, 0.78f, 1f), TextAnchor.MiddleLeft);
            UiKit.SetAnchored(unlockText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-260f, -40f), new Vector2(620f, 110f));
            unlockText.horizontalOverflow = HorizontalWrapMode.Wrap;

            primaryButton = UiKit.MakeButton(root, "PrimaryButton", "NEXT", Green, new Vector2(640f, 170f), OnPrimaryClicked);
            UiKit.SetAnchored(primaryButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -210f), new Vector2(640f, 170f));
            primaryImage = primaryButton.GetComponent<Image>();
            primaryLabel = primaryButton.GetComponentInChildren<Text>();
            primaryLabel.fontSize = 62;
            primaryButton.gameObject.AddComponent<PulseScale>();

            retryButton = UiKit.MakeButton(root, "RetryButton", "RETRY", Blue, new Vector2(400f, 120f), OnRetryClicked);
            UiKit.SetAnchored(retryButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-230f, -400f), new Vector2(400f, 120f));

            rewardButton = UiKit.MakeButton(root, "RewardButton", "▶ +1ショット", Purple, new Vector2(460f, 120f), OnRewardClicked);
            UiKit.SetAnchored(rewardButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(230f, -400f), new Vector2(460f, 120f));

            resultPanel.SetActive(false);
        }

        private void OnStageEnded(StageResult result)
        {
            lastResult = result;
            if (resultRoutine != null)
            {
                StopCoroutine(resultRoutine);
            }
            resultRoutine = StartCoroutine(ShowResultRoutine(result));
        }

        private IEnumerator ShowResultRoutine(StageResult r)
        {
            resultPanel.SetActive(true);
            resultTitle.text = r.Failed ? "FAILED" : (r.Stars >= 3 ? "PERFECT!" : "CLEAR!");
            resultTitle.color = r.Failed ? new Color(1f, 0.45f, 0.45f, 1f) : Color.white;
            // KEEPを壊した失敗は得点が無効。高い数字が成功に見えないよう灰色にする
            scoreText.color = r.FailReason == FailReason.ProtectedDestroyed ? new Color(0.6f, 0.6f, 0.65f, 1f) : Color.white;
            scoreText.text = "0 / " + r.Spec.TargetScore;
            bestText.text = "";
            foreach (Image s in stars)
            {
                s.color = StarOff;
                s.rectTransform.localScale = Vector3.one;
            }

            bool hasUnlock = r.Unlocked.HasValue;
            unlockIcon.gameObject.SetActive(hasUnlock);
            unlockText.gameObject.SetActive(hasUnlock);
            if (hasUnlock)
            {
                Sprite icon = BuildingHistoryManager.LoadIcon(r.Unlocked.Value.IconId);
                unlockIcon.sprite = icon;
                unlockIcon.enabled = icon != null;
                unlockText.text = "建築史解放  " + r.Unlocked.Value.Era + "\n" + r.Unlocked.Value.Name;
            }

            bool canImprove = r.Stars < 3;
            primaryLabel.text = r.Failed ? "RETRY" : "NEXT";
            primaryImage.color = r.Failed ? Orange : Green;
            retryButton.gameObject.SetActive(!r.Failed);
            rewardButton.gameObject.SetActive(false);
            SetButtonsInteractable(false);

            // スコアのカウントアップ
            float t = 0f;
            const float countDuration = 0.7f;
            while (t < countDuration)
            {
                t += Time.unscaledDeltaTime;
                scoreText.text = Mathf.RoundToInt(Mathf.Lerp(0f, r.Score, Mathf.Clamp01(t / countDuration))) + " / " + r.Spec.TargetScore;
                yield return null;
            }
            scoreText.text = r.Score + " / " + r.Spec.TargetScore;
            if (r.FailReason == FailReason.OutOfShots)
            {
                bestText.text = "あと " + Mathf.Max(0, r.Spec.TargetScore - r.Score) + " ptでMAX";
            }
            else if (r.FailReason == FailReason.ProtectedDestroyed)
            {
                bestText.text = "KEEPの階を壊してしまった";
            }
            else
            {
                bestText.text = ScoreManager.Instance != null && r.Score >= ScoreManager.Instance.BestScore && r.Score > 0 ? "NEW BEST!" : "";
            }

            // 星を1つずつ点灯
            if (r.Failed)
            {
                JuiceManager.Instance?.PlayFail();
            }
            for (int i = 0; i < r.Stars; i++)
            {
                yield return new WaitForSecondsRealtime(0.28f);
                stars[i].color = StarOn;
                JuiceManager.Instance?.PlayStarNote(i);
                StartCoroutine(Pop(stars[i].rectTransform, 1.5f));
            }
            if (r.Stars >= 3)
            {
                yield return new WaitForSecondsRealtime(0.15f);
                JuiceManager.Instance?.PlayFanfare();
            }

            yield return new WaitForSecondsRealtime(0.2f);
            SetButtonsInteractable(true);
            bool rewardReady = AdsManager.Instance != null && AdsManager.Instance.IsRewardedReady;
            rewardButton.gameObject.SetActive(rewardReady && (canImprove || r.Failed));
            LayoutSecondaryButtons();
            resultRoutine = null;
        }

        /// <summary>
        /// 副ボタン(RETRY/動画+1ショット)が1つだけのときは中央に、2つのときは左右に並べる。
        /// </summary>
        private void LayoutSecondaryButtons()
        {
            bool both = retryButton.gameObject.activeSelf && rewardButton.gameObject.activeSelf;
            RectTransform retryRect = retryButton.GetComponent<RectTransform>();
            RectTransform rewardRect = rewardButton.GetComponent<RectTransform>();
            retryRect.anchoredPosition = new Vector2(both ? -230f : 0f, -400f);
            rewardRect.anchoredPosition = new Vector2(both ? 230f : 0f, -400f);
        }

        private void SetButtonsInteractable(bool on)
        {
            primaryButton.interactable = on;
            retryButton.interactable = on;
            rewardButton.interactable = on;
        }

        private void HideResult()
        {
            if (resultRoutine != null)
            {
                StopCoroutine(resultRoutine);
                resultRoutine = null;
            }
            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }
        }

        private void OnPrimaryClicked()
        {
            if (lastResult == null)
            {
                return;
            }

            if (lastResult.Failed)
            {
                GameManager.Instance?.RetryStage(0);
            }
            else
            {
                GameManager.Instance?.StartNextStage();
            }
        }

        private void OnRetryClicked()
        {
            GameManager.Instance?.RetryStage(0);
        }

        private void OnRewardClicked()
        {
            // 広告を最後まで見た場合のみ、同じステージを+1ショットでやり直せる
            AdsManager.Instance?.ShowRewarded(() => GameManager.Instance?.RetryStage(1));
        }

        private static IEnumerator Pop(RectTransform rt, float peak)
        {
            float elapsed = 0f;
            const float duration = 0.35f;
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
    }

    /// <summary>
    /// 主ボタンをゆっくり脈動させて「次はこれを押す」と一目で分かるようにする。
    /// </summary>
    public class PulseScale : MonoBehaviour
    {
        [SerializeField] private float amount = 0.06f;
        [SerializeField] private float speed = 3f;

        private void Update()
        {
            float s = 1f + amount * Mathf.Sin(Time.unscaledTime * speed);
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
