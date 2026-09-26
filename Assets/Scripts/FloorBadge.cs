using UnityEngine;
using UnityEngine.UI;

namespace OneTapDemolition
{
    /// <summary>
    /// 階の正面に浮かぶ「×3」「÷2」「KEEP」の看板。タップ前に効果が分かるようにするための常時表示ラベル。
    /// 階の非一様スケールの影響を受けないよう、階の子にはせず位置だけ追従する。カメラの方を向き続ける。
    /// </summary>
    public class FloorBadge : MonoBehaviour
    {
        private Transform target;
        private float towardCamera;
        private Camera cam;
        private bool pulse;
        private Renderer targetRenderer;

        public static FloorBadge Create(Transform floor, string label, Color color, float towardCameraDistance)
        {
            GameObject root = new GameObject("FloorBadge", typeof(RectTransform), typeof(Canvas));
            FloorBadge badge = root.AddComponent<FloorBadge>();
            badge.target = floor;
            badge.towardCamera = towardCameraDistance;
            badge.targetRenderer = floor.GetComponent<Renderer>();

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(260f, 82f);
            root.transform.localScale = Vector3.one * 0.0095f;

            GameObject bg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(root.transform, false);
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            Image bgImage = bg.GetComponent<Image>();
            bgImage.sprite = UiSprites.RoundedRect;
            bgImage.type = Image.Type.Sliced;
            bgImage.color = new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, 0.92f);
            bgImage.raycastTarget = false;

            GameObject textGO = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Outline));
            textGO.transform.SetParent(root.transform, false);
            RectTransform textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            Text text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontStyle = FontStyle.Bold;
            text.fontSize = label.Length > 3 ? 48 : 68;
            text.color = Color.Lerp(color, Color.white, 0.55f);
            text.raycastTarget = false;
            Outline outline = textGO.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            return badge;
        }

        /// <summary>
        /// ボーナス階用の脈動する☆バッジ。テキストではなく☆スプライトで表す(フォント依存を避ける)。
        /// </summary>
        public static FloorBadge CreateStar(Transform floor, Color color, float towardCameraDistance)
        {
            FloorBadge badge = Create(floor, "", color, towardCameraDistance);
            badge.pulse = true;

            GameObject iconGO = new GameObject("StarIcon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(badge.transform, false);
            RectTransform rect = iconGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(74f, 74f);
            Image icon = iconGO.GetComponent<Image>();
            icon.sprite = UiSprites.Star;
            icon.color = Color.Lerp(color, Color.white, 0.25f);
            icon.raycastTarget = false;
            return badge;
        }

        public void Detach()
        {
            Destroy(gameObject);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                Destroy(gameObject);
                return;
            }
            if (cam == null)
            {
                cam = Camera.main;
                if (cam == null)
                {
                    return;
                }
            }

            // 階の手前(-Z)の面に貼り付ける。カメラ向きではなく面と同じ向きにして、階から浮いて見えないようにする
            float depth = targetRenderer != null ? targetRenderer.bounds.extents.z : 1f;
            transform.position = target.position + new Vector3(0f, 0f, -(depth + 0.06f));
            transform.rotation = Quaternion.identity;
            if (pulse)
            {
                float s = 0.0095f * (1f + 0.12f * Mathf.Sin(Time.time * 5f));
                transform.localScale = Vector3.one * s;
            }
        }
    }
}
