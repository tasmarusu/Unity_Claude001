using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// 1階分のロジック。タップされると物理演算を有効化して弾け飛ぶ。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class Floor : MonoBehaviour
    {
        [Header("Physics")]
        [SerializeField] private float launchForce = 3.5f;
        [SerializeField] private float launchTorque = 2.5f;
        [Tooltip("hitPointから見た方向にどれだけ従うか(0=完全ランダム散らばり、1=常に同じ方向)。連鎖中の全階が同じ方向に滑り落ちるのを防ぐ。")]
        [SerializeField] private float hitDirectionWeight = 0.4f;
        [SerializeField] private float scatterWeight = 0.8f;
        [SerializeField] private float upwardLift = 0.6f;
        [Tooltip("連鎖が進むほど吹っ飛びが強くなる割合(段ごとの加算率)。")]
        [SerializeField] private float chainKickPerStep = 0.06f;

        [Header("Visuals")]
        [SerializeField] private GameObject accentBand;
        [SerializeField] private int accentInterval = 4;

        private Rigidbody rb;
        private BuildingTower ownerTower;
        private int floorIndex;
        private bool isDemolished;
        private float forceMultiplier = 1f;

        public int FloorIndex => floorIndex;
        public bool IsDemolished => isDemolished;
        public BuildingTower OwnerTower => ownerTower;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
        }

        /// <summary>
        /// タワー生成時にBuildingTowerから呼ばれる初期化。
        /// </summary>
        public void Setup(int index, BuildingTower tower)
        {
            floorIndex = index;
            ownerTower = tower;
            isDemolished = false;
            forceMultiplier = 1f;

            rb.isKinematic = true;

            if (accentBand != null)
            {
                accentBand.SetActive(accentInterval > 0 && index % accentInterval == 0);
            }
        }

        /// <summary>
        /// リワード広告の「デモリションブースト」等、崩落時の吹っ飛び方を強化する倍率を設定する。
        /// </summary>
        public void SetForceMultiplier(float multiplier)
        {
            forceMultiplier = Mathf.Max(0.1f, multiplier);
        }

        /// <summary>
        /// この階を破壊する。物理演算をオンにして弾け飛ばし、演出フックを呼ぶ。
        /// chainStep: この階が連鎖の何番目に崩れたか(1始まり)。
        /// 連鎖中は全階に同じhitPointが渡ってくるため、方向をそのまま使うと全階が同じ向きに
        /// 滑り落ちるだけになる。ランダムな散らばりと上向きのリフトを混ぜて、爆発的にバラける見た目にする。
        /// また連鎖が進むほど勢いを増して、崩落が加速していく感覚を出す。
        /// </summary>
        public void Demolish(Vector3 hitPoint, int chainStep)
        {
            if (isDemolished)
            {
                return;
            }

            isDemolished = true;
            rb.isKinematic = false;

            Vector3 hitDirection = transform.position - hitPoint;
            hitDirection = hitDirection.sqrMagnitude < 0.01f ? Random.onUnitSphere : hitDirection.normalized;

            Vector3 pushDirection = (hitDirection * hitDirectionWeight
                + Random.insideUnitSphere * scatterWeight
                + Vector3.up * upwardLift).normalized;

            float chainKick = 1f + chainKickPerStep * Mathf.Max(0, chainStep - 1);
            float totalForce = launchForce * forceMultiplier * chainKick;

            rb.AddForce(pushDirection * totalForce, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * (launchTorque * forceMultiplier * chainKick), ForceMode.Impulse);

            JuiceManager.Instance?.PlayFloorDestroyEffect(transform.position, chainStep);
        }
    }
}
