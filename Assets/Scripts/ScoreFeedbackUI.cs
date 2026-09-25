using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace OneTapDemolition
{
    /// <summary>
    /// JuiceManagerの演出フックを受けて、崩落した階から「+N」を浮かせるポップアップと、
    /// 連鎖数が2以上のときに「N CHAIN!」を中央に出すコンボ表示、
    /// リワード広告視聴時の「POWER UP!」バナー、建築史アンロック時のトーストを行う。
    /// シーンに手動配置せず、実行時に自分でCanvas/Text等を生成する
    /// (他セッションによるシーン上書きでUIごと消えるのを避けるため)。
    /// </summary>
    public class ScoreFeedbackUI : MonoBehaviour
    {
        public static ScoreFeedbackUI Instance { get; private set; }

        [SerializeField] private float popupDuration = 0.7f;
        [SerializeField] private float popupRiseDistance = 80f;
        [SerializeField] private float bannerHoldDuration = 0.6f;
        [SerializeField] private float flashDuration = 0.18f;
        [SerializeField] private float historyToastHoldDuration = 2.0f;

        private Canvas targetCanvas;
        private Camera mainCamera;
        private Text bannerText;
        private Image flashImage;
        private Coroutine bannerRoutine;
        private Coroutine flashRoutine;

        private GameObject historyToastRoot;
        private CanvasGroup historyCanvasGroup;
        private Image historyIconImage;
        private Text historyText;
        private Coroutine historyRoutine;

        private static readonly Color ComboColor = new Color(1f, 0.85f, 0.2f, 1f);
        private static readonly Color GoodGateColor = new Color(0.6f, 1f, 0.55f, 1f);
        private static readonly Color BadGateColor = new Color(1f, 0.4f, 0.4f, 1f);
        private static readonly Color PowerUpColor = new Color(0.45f, 0.85f, 1f, 1f);
        private static readonly Color HistoryColor = new Color(1f, 0.95f, 0.78f, 1f);

        /// <summary>
        /// シーンへの手動配置不要で自動的に生き始める。他セッションによるシーン上書きの影響を受けない。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<ScoreFeedbackUI>() != null)
            {
                return;
            }

            GameObject go = new GameObject(nameof(ScoreFeedbackUI));
            go.AddComponent<ScoreFeedbackUI>();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            mainCamera = Camera.main;
            targetCanvas = FindOrCreateCanvas();
            CreateFlashImage();
            CreateBannerText();
            CreateHistoryToast();

            if (JuiceManager.Instance != null)
            {
                JuiceManager.Instance.OnScorePopupRequested.AddListener(OnScorePopup);
                JuiceManager.Instance.OnChainImpact += OnChainImpact;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (JuiceManager.Instance != null)
            {
                JuiceManager.Instance.OnScorePopupRequested.RemoveListener(OnScorePopup);
                JuiceManager.Instance.OnChainImpact -= OnChainImpact;
            }
        }

        private Canvas FindOrCreateCanvas()
        {
            GameObject existing = GameObject.Find("ScoreCanvas");
            if (existing != null)
            {
                Canvas existingCanvas = existing.GetComponent<Canvas>();
                if (existingCanvas != null)
                {
                    return existingCanvas;
                }
            }

            GameObject go = new GameObject(
                "ScoreFeedbackCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        private void CreateFlashImage()
        {
            GameObject go = new GameObject("ImpactFlash", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(targetCanvas.transform, false);
            go.transform.SetAsFirstSibling();

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            flashImage = go.GetComponent<Image>();
            flashImage.color = new Color(1f, 1f, 1f, 0f);
            flashImage.raycastTarget = false;
        }

        private void CreateBannerText()
        {
            GameObject go = new GameObject("FeedbackBannerText", typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(targetCanvas.transform, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.62f);
            rect.anchorMax = new Vector2(0.5f, 0.62f);
            rect.sizeDelta = new Vector2(700, 140);
            rect.anchoredPosition = Vector2.zero;

            bannerText = go.GetComponent<Text>();
            bannerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            bannerText.alignment = TextAnchor.MiddleCenter;
            bannerText.fontSize = 64;
            bannerText.fontStyle = FontStyle.Bold;
            bannerText.color = ComboColor;

            Outline outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(3, -3);

            go.SetActive(false);
        }

        private void CreateHistoryToast()
        {
            GameObject root = new GameObject("HistoryUnlockToast", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(targetCanvas.transform, false);

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.83f);
            rootRect.anchorMax = new Vector2(0.5f, 0.83f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(680, 128);
            rootRect.anchoredPosition = Vector2.zero;

            historyCanvasGroup = root.GetComponent<CanvasGroup>();

            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(root.transform, false);
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            Image bgImage = bg.GetComponent<Image>();
            bgImage.color = new Color(0.05f, 0.05f, 0.09f, 0.8f);

            GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(root.transform, false);
            RectTransform iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.sizeDelta = new Vector2(100, 100);
            iconRect.anchoredPosition = new Vector2(14, 0);
            historyIconImage = iconGO.GetComponent<Image>();
            historyIconImage.preserveAspect = true;

            GameObject textGO = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Outline));
            textGO.transform.SetParent(root.transform, false);
            RectTransform textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(130, 10);
            textRect.offsetMax = new Vector2(-16, -10);

            historyText = textGO.GetComponent<Text>();
            historyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            historyText.alignment = TextAnchor.MiddleLeft;
            historyText.fontSize = 26;
            historyText.fontStyle = FontStyle.Bold;
            historyText.color = HistoryColor;
            historyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            historyText.verticalOverflow = VerticalWrapMode.Truncate;

            Outline outline = textGO.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2, -2);

            historyToastRoot = root;
            historyCanvasGroup.alpha = 0f;
            historyToastRoot.SetActive(false);
        }

        private void OnScorePopup(Vector3 worldPosition, int amount)
        {
            if (targetCanvas == null || mainCamera == null)
            {
                return;
            }

            GameObject go = new GameObject("ScorePopup", typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(targetCanvas.transform, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(260, 90);
            rect.anchoredPosition = WorldToCanvasPosition(worldPosition);

            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.alignment = TextAnchor.MiddleCenter;
            text.fontStyle = FontStyle.Bold;
            text.text = "+" + amount;

            // 加算量が多いほど大きく、より熱い色にして「効いてる感」を出す
            float t = Mathf.InverseLerp(10f, 60f, amount);
            text.fontSize = Mathf.RoundToInt(Mathf.Lerp(36f, 64f, t));
            text.color = Color.Lerp(Color.white, new Color(1f, 0.5f, 0.1f, 1f), t);

            Outline outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2, -2);

            StartCoroutine(AnimatePopup(rect, text));
        }

        private Vector2 WorldToCanvasPosition(Vector3 worldPosition)
        {
            Vector3 screenPoint = mainCamera.WorldToScreenPoint(worldPosition);
            RectTransform canvasRect = targetCanvas.transform as RectTransform;
            Camera eventCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera;

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, eventCamera, out localPoint);
            return localPoint;
        }

        private IEnumerator AnimatePopup(RectTransform rect, Text text)
        {
            float elapsed = 0f;
            Vector2 startPos = rect.anchoredPosition;
            Vector2 endPos = startPos + new Vector2(0f, popupRiseDistance);
            Color startColor = text.color;

            while (elapsed < popupDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / popupDuration);
                rect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                text.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(1f, 0f, t));
                yield return null;
            }

            if (rect != null)
            {
                Destroy(rect.gameObject);
            }
        }

        private void OnChainImpact(int totalChainCount)
        {
            PlayFlash(totalChainCount);

            if (totalChainCount < 2 || bannerText == null)
            {
                return;
            }

            PlayBanner(totalChainCount + " CHAIN!", ComboColor);
        }

        /// <summary>
        /// タップ衝撃のたびに画面を一瞬白く光らせる。連鎖数が多いほど強く光る。
        /// </summary>
        private void PlayFlash(int totalChainCount)
        {
            if (flashImage == null)
            {
                return;
            }

            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            float peakAlpha = Mathf.Clamp01(0.25f + 0.05f * Mathf.Max(0, totalChainCount - 1));
            flashRoutine = StartCoroutine(FlashRoutine(peakAlpha));
        }

        private IEnumerator FlashRoutine(float peakAlpha)
        {
            flashImage.color = new Color(1f, 1f, 1f, peakAlpha);

            float elapsed = 0f;
            while (elapsed < flashDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / flashDuration);
                flashImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(peakAlpha, 0f, t));
                yield return null;
            }

            flashImage.color = new Color(1f, 1f, 1f, 0f);
            flashRoutine = null;
        }

        /// <summary>
        /// リワード広告視聴後、次のタワーが強化されたことを知らせるバナー。
        /// GameManager.SpawnNewTowerから、ブースト適用時に呼ばれる。
        /// </summary>
        public void ShowPowerUpBanner()
        {
            if (bannerText == null)
            {
                return;
            }

            PlayBanner("POWER UP!", PowerUpColor);
        }

        /// <summary>
        /// 建築史トリビアが1項目解放されたことを知らせるトースト。
        /// GameManager.OnTowerClearedから、クリアのたびに呼ばれる。
        /// </summary>
        public void ShowHistoryUnlock(BuildingHistoryManager.Entry entry)
        {
            if (historyToastRoot == null)
            {
                return;
            }

            if (historyRoutine != null)
            {
                StopCoroutine(historyRoutine);
            }

            if (historyIconImage != null)
            {
                Sprite icon = BuildingHistoryManager.LoadIcon(entry.IconId);
                historyIconImage.sprite = icon;
                historyIconImage.enabled = icon != null;
            }

            if (historyText != null)
            {
                historyText.text = "建築史解放 " + entry.Era + "\n" + entry.Name;
            }

            historyRoutine = StartCoroutine(HistoryToastRoutine());
        }

        private IEnumerator HistoryToastRoutine()
        {
            historyToastRoot.SetActive(true);
            historyToastRoot.transform.localScale = Vector3.one * 0.7f;
            historyCanvasGroup.alpha = 1f;

            yield return PunchScale(historyToastRoot.transform, 0.7f, 1f, 0.2f);

            yield return new WaitForSecondsRealtime(historyToastHoldDuration);

            float elapsed = 0f;
            const float fadeDuration = 0.3f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                historyCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
                yield return null;
            }

            historyToastRoot.SetActive(false);
            historyRoutine = null;
        }

        /// <summary>
        /// ゲート階を通過した瞬間の「×3」バナー。通過後の合計倍率も出して、得点が伸びた理由を見せる。
        /// </summary>
        public void ShowGateBanner(float gateValue, float gateProduct)
        {
            if (bannerText == null)
            {
                return;
            }

            bool bad = gateValue < 1f;
            string label = bad ? "÷2" : "×" + Mathf.RoundToInt(gateValue);
            if (Mathf.Abs(gateProduct - gateValue) > 0.01f)
            {
                label += "  (合計 ×" + gateProduct.ToString("0.#") + ")";
            }
            PlayBanner(label, bad ? BadGateColor : GoodGateColor);
        }

        public void ShowFailBanner()
        {
            if (bannerText == null)
            {
                return;
            }

            PlayBanner("NG!", BadGateColor);
        }

        private void PlayBanner(string text, Color color)
        {
            if (bannerRoutine != null)
            {
                StopCoroutine(bannerRoutine);
            }
            bannerRoutine = StartCoroutine(BannerRoutine(text, color));
        }

        private IEnumerator BannerRoutine(string text, Color color)
        {
            bannerText.text = text;
            bannerText.color = new Color(color.r, color.g, color.b, 1f);
            bannerText.gameObject.SetActive(true);

            yield return PunchScale(bannerText.transform, 0.6f, 1.15f, 0.15f);
            yield return PunchScale(bannerText.transform, 1.15f, 1f, 0.1f);

            yield return new WaitForSecondsRealtime(bannerHoldDuration);

            float elapsed = 0f;
            const float fadeDuration = 0.25f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                bannerText.color = new Color(color.r, color.g, color.b, Mathf.Lerp(1f, 0f, t));
                yield return null;
            }

            bannerText.gameObject.SetActive(false);
            bannerRoutine = null;
        }

        private IEnumerator PunchScale(Transform target, float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                target.localScale = Vector3.one * Mathf.Lerp(from, to, EaseOutBack(t));
                yield return null;
            }
            target.localScale = Vector3.one * to;
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
