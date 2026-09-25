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

        public static FloorBadge Create(Transform floor, string label, Color color, float towardCameraDistance)
        {
            GameObject root = new GameObject("FloorBadge", typeof(RectTransform), typeof(Canvas));
            FloorBadge badge = root.AddComponent<FloorBadge>();
            badge.target = floor;
            badge.towardCamera = towardCameraDistance;

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(260f, 120f);
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
            text.fontSize = label.Length > 3 ? 64 : 92;
            text.color = Color.Lerp(color, Color.white, 0.55f);
            text.raycastTarget = false;
            Outline outline = textGO.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);

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

            Vector3 toCam = (cam.transform.position - target.position);
            toCam.y = 0f;
            toCam = toCam.sqrMagnitude > 0.0001f ? toCam.normalized : Vector3.back;
            transform.position = target.position + toCam * towardCamera;
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
        }
    }
}
