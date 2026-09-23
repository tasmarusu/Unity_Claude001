using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace OneTapDemolition
{
    /// <summary>
    /// JuiceManagerの演出フックを受けて、崩落した階から「+N」を浮かせるポップアップと、
    /// 連鎖数が2以上のときに「N CHAIN!」を中央に出すコンボ表示を行う。
    /// シーンに手動配置せず、実行時に自分でCanvas/Text等を生成する
    /// (他セッションによるシーン上書きでUIごと消えるのを避けるため)。
    /// </summary>
    public class ScoreFeedbackUI : MonoBehaviour
    {
        [SerializeField] private float popupDuration = 0.7f;
        [SerializeField] private float popupRiseDistance = 80f;
        [SerializeField] private float comboHoldDuration = 0.6f;

        private Canvas targetCanvas;
        private Camera mainCamera;
        private Text comboText;
        private Coroutine comboRoutine;

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

        private void Start()
        {
            mainCamera = Camera.main;
            targetCanvas = FindOrCreateCanvas();
            CreateComboText();

            if (JuiceManager.Instance != null)
            {
                JuiceManager.Instance.OnScorePopupRequested.AddListener(OnScorePopup);
                JuiceManager.Instance.OnChainImpact += OnChainImpact;
            }
        }

        private void OnDestroy()
        {
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

        private void CreateComboText()
        {
            GameObject go = new GameObject("ChainComboText", typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(targetCanvas.transform, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.62f);
            rect.anchorMax = new Vector2(0.5f, 0.62f);
            rect.sizeDelta = new Vector2(700, 140);
            rect.anchoredPosition = Vector2.zero;

            comboText = go.GetComponent<Text>();
            comboText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            comboText.alignment = TextAnchor.MiddleCenter;
            comboText.fontSize = 64;
            comboText.fontStyle = FontStyle.Bold;
            comboText.color = new Color(1f, 0.85f, 0.2f, 1f);

            Outline outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(3, -3);

            go.SetActive(false);
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
            if (totalChainCount < 2 || comboText == null)
            {
                return;
            }

            if (comboRoutine != null)
            {
                StopCoroutine(comboRoutine);
            }
            comboRoutine = StartCoroutine(ComboRoutine(totalChainCount));
        }

        private IEnumerator ComboRoutine(int totalChainCount)
        {
            comboText.text = totalChainCount + " CHAIN!";
            comboText.gameObject.SetActive(true);
            Color baseColor = comboText.color;
            comboText.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);

            yield return PunchScale(comboText.transform, 0.6f, 1.15f, 0.15f);
            yield return PunchScale(comboText.transform, 1.15f, 1f, 0.1f);

            yield return new WaitForSecondsRealtime(comboHoldDuration);

            float elapsed = 0f;
            const float fadeDuration = 0.25f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                comboText.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(1f, 0f, t));
                yield return null;
            }

            comboText.gameObject.SetActive(false);
            comboRoutine = null;
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
