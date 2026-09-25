using UnityEngine;

namespace OneTapDemolition
{
    public enum AimState
    {
        None,
        InChain,
        Target
    }

    /// <summary>
    /// 1階分のロジック。タップされると物理演算を有効化して弾け飛ぶ。
    /// 種類(通常/ゲート/保護)に応じた色分けと看板、狙っている間のハイライトも担当する。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class Floor : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color FallbackDebrisTint = new Color(0.6f, 0.57f, 0.53f, 1f);
        private static readonly Color GateX2Color = new Color(0.55f, 1f, 0.6f, 1f);
        private static readonly Color GateX3Color = new Color(1f, 0.85f, 0.25f, 1f);
        private static readonly Color GateBadColor = new Color(1f, 0.45f, 0.45f, 1f);
        private static readonly Color ProtectedColor = new Color(0.5f, 0.78f, 1f, 1f);

        [Header("Physics")]
        [SerializeField] private float launchForce = 3.5f;
        [SerializeField] private float launchTorque = 2.5f;
        [Tooltip("hitPointから見た方向にどれだけ従うか(0=完全ランダム散らばり、1=常に同じ方向)。")]
        [SerializeField] private float hitDirectionWeight = 0.4f;
        [SerializeField] private float scatterWeight = 0.8f;
        [SerializeField] private float upwardLift = 0.6f;
        [Tooltip("連鎖が進むほど吹っ飛びが強くなる割合(段ごとの加算率)。")]
        [SerializeField] private float chainKickPerStep = 0.06f;
        [Tooltip("ゲート階・保護階を壊したときの追加の吹っ飛び倍率(壊した実感を強める)。")]
        [SerializeField] private float specialKick = 1.5f;

        [Header("Visuals")]
        [SerializeField] private GameObject accentBand;
        [SerializeField] private int accentInterval = 4;
        [SerializeField] private float badgeDistance = 1.6f;

        private Rigidbody rb;
        private MeshRenderer bodyRenderer;
        private MaterialPropertyBlock block;
        private Color bodyBaseColor = Color.white;
        private BuildingTower ownerTower;
        private FloorBadge badge;
        private int floorIndex;
        private bool isDemolished;
        private Color debrisTint = FallbackDebrisTint;
        private Color kindTint = Color.white;

        public FloorSpec Spec { get; private set; } = FloorSpec.Normal;
        public int FloorIndex => floorIndex;
        public bool IsDemolished => isDemolished;
        public BuildingTower OwnerTower => ownerTower;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            bodyRenderer = GetComponent<MeshRenderer>();
            block = new MaterialPropertyBlock();
            if (bodyRenderer != null && bodyRenderer.sharedMaterial != null && bodyRenderer.sharedMaterial.HasProperty(ColorId))
            {
                bodyBaseColor = bodyRenderer.sharedMaterial.GetColor(ColorId);
            }
            CacheDebrisTint();
        }

        /// <summary>
        /// 建物バリアントのAccentマテリアルの色を1回だけ拾い、通常階の崩落デブリの色にする。
        /// </summary>
        private void CacheDebrisTint()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                Material material = renderer.sharedMaterial;
                if (material == null || material.name.IndexOf("Accent", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                Color baseColor = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor")
                    : material.HasProperty("_Color") ? material.color
                    : FallbackDebrisTint;
                debrisTint = Color.Lerp(baseColor, FallbackDebrisTint, 0.35f);
                return;
            }
        }

        /// <summary>
        /// タワー生成時にBuildingTowerから呼ばれる初期化。
        /// </summary>
        public void Setup(int index, BuildingTower tower, FloorSpec spec)
        {
            floorIndex = index;
            ownerTower = tower;
            isDemolished = false;
            rb.isKinematic = true;

            if (accentBand != null)
            {
                accentBand.SetActive(accentInterval > 0 && index % accentInterval == 0);
            }

            ApplyKind(spec);
        }

        private void ApplyKind(FloorSpec spec)
        {
            Spec = spec;
            string label = null;
            switch (spec.Kind)
            {
                case FloorKind.Gate:
                    if (spec.GateValue >= 3f)
                    {
                        kindTint = GateX3Color;
                        label = "×3";
                    }
                    else if (spec.GateValue >= 2f)
                    {
                        kindTint = GateX2Color;
                        label = "×2";
                    }
                    else
                    {
                        kindTint = GateBadColor;
                        label = "÷2";
                    }
                    break;
                case FloorKind.Protected:
                    kindTint = ProtectedColor;
                    label = "KEEP";
                    break;
                default:
                    kindTint = Color.white;
                    break;
            }

            if (spec.Kind != FloorKind.Normal)
            {
                debrisTint = Color.Lerp(kindTint, FallbackDebrisTint, 0.2f);
                badge = FloorBadge.Create(transform, label, kindTint, badgeDistance);
            }

            SetAim(AimState.None, false);
        }

        /// <summary>
        /// 狙っている間のハイライト。Targetはタップ位置、InChainは巻き込まれる階(保護階を含む場合は赤み)。
        /// </summary>
        public void SetAim(AimState state, bool danger)
        {
            if (bodyRenderer == null)
            {
                return;
            }

            Color tint = kindTint;
            switch (state)
            {
                case AimState.Target:
                    tint = Color.Lerp(kindTint, new Color(1f, 0.95f, 0.5f, 1f), 0.6f);
                    break;
                case AimState.InChain:
                    tint = danger
                        ? Color.Lerp(kindTint, new Color(1f, 0.3f, 0.3f, 1f), 0.4f)
                        : Color.Lerp(kindTint, Color.white, 0.3f);
                    break;
            }

            block.SetColor(ColorId, new Color(bodyBaseColor.r * tint.r, bodyBaseColor.g * tint.g, bodyBaseColor.b * tint.b, bodyBaseColor.a));
            bodyRenderer.SetPropertyBlock(block);
        }

        /// <summary>
        /// この階を破壊する。物理演算をオンにして弾け飛ばし、演出フックを呼ぶ。
        /// chainStep: この階が連鎖の何番目に崩れたか(1始まり)。
        /// </summary>
        public void Demolish(Vector3 hitPoint, int chainStep)
        {
            if (isDemolished)
            {
                return;
            }

            isDemolished = true;
            rb.isKinematic = false;
            SetAim(AimState.None, false);
            if (badge != null)
            {
                badge.Detach();
                badge = null;
            }

            Vector3 hitDirection = transform.position - hitPoint;
            hitDirection = hitDirection.sqrMagnitude < 0.01f ? Random.onUnitSphere : hitDirection.normalized;

            Vector3 pushDirection = (hitDirection * hitDirectionWeight
                + Random.insideUnitSphere * scatterWeight
                + Vector3.up * upwardLift).normalized;

            float chainKick = 1f + chainKickPerStep * Mathf.Max(0, chainStep - 1);
            float special = Spec.Kind == FloorKind.Normal ? 1f : specialKick;
            float totalForce = launchForce * chainKick * special;

            rb.AddForce(pushDirection * totalForce, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * (launchTorque * chainKick * special), ForceMode.Impulse);

            JuiceManager.Instance?.PlayFloorDestroyEffect(transform.position, chainStep, debrisTint);
        }

        private void OnDestroy()
        {
            if (badge != null)
            {
                badge.Detach();
            }
        }
    }
}
