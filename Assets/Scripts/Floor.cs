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

        private Rigidbody rb;
        private BuildingTower ownerTower;
        private int floorIndex;
        private bool isDemolished;

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

            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
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

            Vector3 pushDirection = transform.position - hitPoint;
            if (pushDirection.sqrMagnitude < 0.01f)
            {
                pushDirection = Random.onUnitSphere;
            }
            pushDirection = (pushDirection.normalized + Vector3.up * 0.5f).normalized;

            rb.AddForce(pushDirection * launchForce, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * launchTorque, ForceMode.Impulse);

            JuiceManager.Instance?.PlayFloorDestroyEffect(transform.position, chainStep);
        }
    }
}
