using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// 外部画像なしで使える基本UIスプライトをコードで生成(キャッシュ)する。
    /// モデル制作セッションの正式アセットがある場合は、呼び出し側がResourcesを先に探してこちらを代替にする。
    /// </summary>
    public static class UiSprites
    {
        private static Sprite roundedRect;
        private static Sprite circle;
        private static Sprite star;

        public static Sprite RoundedRect
        {
            get
            {
                if (roundedRect == null)
                {
                    const int size = 64;
                    const float radius = 22f;
                    Texture2D tex = NewTexture(size);
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float dx = Mathf.Max(radius - (x + 0.5f), (x + 0.5f) - (size - radius), 0f);
                            float dy = Mathf.Max(radius - (y + 0.5f), (y + 0.5f) - (size - radius), 0f);
                            float dist = Mathf.Sqrt(dx * dx + dy * dy);
                            float a = Mathf.Clamp01(radius - dist + 0.5f);
                            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                        }
                    }
                    tex.Apply();
                    roundedRect = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                        SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
                }
                return roundedRect;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (circle == null)
                {
                    const int size = 64;
                    Texture2D tex = NewTexture(size);
                    float r = size * 0.5f - 1f;
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                            tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - dist + 0.5f)));
                        }
                    }
                    tex.Apply();
                    circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                }
                return circle;
            }
        }

        public static Sprite Star
        {
            get
            {
                if (star == null)
                {
                    // モデル制作セッションの正式アセット(Assets/Resources/StageUI/star.png)があれば優先して使う
                    star = Resources.Load<Sprite>("StageUI/star");
                }
                if (star == null)
                {
                    const int size = 96;
                    Texture2D tex = NewTexture(size);
                    Vector2[] poly = new Vector2[10];
                    Vector2 c = new Vector2(size * 0.5f, size * 0.5f - 2f);
                    for (int i = 0; i < 10; i++)
                    {
                        float ang = Mathf.PI / 2f + i * Mathf.PI / 5f;
                        float rad = (i % 2 == 0 ? 0.48f : 0.21f) * size;
                        poly[i] = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
                    }
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float a = InsidePolygon(poly, new Vector2(x + 0.5f, y + 0.5f)) ? 1f : 0f;
                            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                        }
                    }
                    tex.Apply();
                    tex.filterMode = FilterMode.Bilinear;
                    star = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                }
                return star;
            }
        }

        private static Texture2D NewTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static bool InsidePolygon(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                    (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                {
                    inside = !inside;
                }
            }
            return inside;
        }
    }
}
