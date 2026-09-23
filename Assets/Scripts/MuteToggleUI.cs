using UnityEngine;
using UnityEngine.UI;

namespace OneTapDemolition
{
    /// <summary>
    /// 画面左上に小さなミュートボタンを出す。音を追加した以上、消す手段が必須なため。
    /// 設定はPlayerPrefsに保存し、次回起動時も維持する。
    /// シーンへの手動配置不要で実行時に自分でUIを生成する。
    /// </summary>
    public class MuteToggleUI : MonoBehaviour
    {
        private const string MutePrefKey = "OneTapDemolition_Muted";

        private Text label;
        private bool isMuted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<MuteToggleUI>() != null)
            {
                return;
            }

            GameObject go = new GameObject(nameof(MuteToggleUI));
            go.AddComponent<MuteToggleUI>();
        }

        private void Start()
        {
            isMuted = PlayerPrefs.GetInt(MutePrefKey, 0) == 1;
            ApplyMuteState();
            BuildUI();
        }

        private void BuildUI()
        {
            Canvas canvas = FindOrCreateCanvas();

            GameObject buttonGO = new GameObject("MuteButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGO.transform.SetParent(canvas.transform, false);

            RectTransform rect = buttonGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(84, 84);
            rect.anchoredPosition = new Vector2(24, -24);

            Image bg = buttonGO.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.45f);

            Button button = buttonGO.GetComponent<Button>();
            button.onClick.AddListener(ToggleMute);

            GameObject labelGO = new GameObject("MuteLabel", typeof(RectTransform), typeof(Text));
            labelGO.transform.SetParent(buttonGO.transform, false);
            RectTransform labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;

            label = labelGO.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 40;
            label.color = Color.white;
            UpdateLabel();
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
                "MuteToggleCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        private void ToggleMute()
        {
            isMuted = !isMuted;
            PlayerPrefs.SetInt(MutePrefKey, isMuted ? 1 : 0);
            ApplyMuteState();
            UpdateLabel();
        }

        private void ApplyMuteState()
        {
            AudioListener.volume = isMuted ? 0f : 1f;
        }

        private void UpdateLabel()
        {
            if (label != null)
            {
                label.text = isMuted ? "MUTE" : "SND";
            }
        }
    }
}
