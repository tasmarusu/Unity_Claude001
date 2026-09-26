using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// タワーの高さに合わせてカメラの距離と向きを決め、タワーが画面の狙いの高さ(既定で約半分)に
    /// 大きく収まるようにする。FOVを広げて引くのではなく、カメラ自体を寄せる/離すので、
    /// 背の高いタワーでも1階が小さくなりすぎず、タッチしやすい大きさを保つ。
    /// タワーはHUD(上)とボタン(下)の間に収まるよう、視点を少し上げて画面のやや下寄りに置く。
    /// 画面揺れ(JuiceManager)はSetShakeで受け取り、位置に足す(基準位置を上書きしない)。
    /// シーンへの手動配置不要で、Main Cameraに自動アタッチする。
    /// </summary>
    public class CameraFraming : MonoBehaviour
    {
        public static CameraFraming Instance { get; private set; }

        [SerializeField] private float fieldOfView = 50f;
        [Tooltip("タワーの高さが画面の高さに占めてほしい割合。大きいほどタワーが大きく写る。")]
        [SerializeField] private float towerScreenFraction = 0.6f;
        [SerializeField] private float minDistance = 16f;
        [SerializeField] private float maxDistance = 26f;
        [Tooltip("カメラの仰角(度)。0で真横、大きいほど見下ろす。")]
        [SerializeField] private float elevationDegrees = 8f;
        [Tooltip("タワー中心を画面の中心より下へずらす量(画面高さ比)。上のHUD、下のボタンを避けるため。")]
        [SerializeField] private float towerDownShift = 0.03f;
        [SerializeField] private float blendSpeed = 5f;

        private Camera cam;
        private Vector3 horizontalDirection;
        private Vector3 basePosition;
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private Vector2 shake;
        private bool hasTarget;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<CameraFraming>() != null)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            mainCamera.gameObject.AddComponent<CameraFraming>();
        }

        private void Awake()
        {
            Instance = this;
            cam = GetComponent<Camera>();

            // シーンに置かれたカメラの水平方向(タワーから見たカメラの向き)を、そのまま使う
            Vector3 flat = transform.position;
            flat.y = 0f;
            horizontalDirection = flat.sqrMagnitude > 0.01f ? flat.normalized : new Vector3(0.4f, 0f, -0.9f).normalized;

            if (cam != null)
            {
                cam.fieldOfView = fieldOfView;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void LateUpdate()
        {
            if (!hasTarget)
            {
                return;
            }

            float k = 1f - Mathf.Exp(-blendSpeed * Time.unscaledDeltaTime);
            basePosition = Vector3.Lerp(basePosition, targetPosition, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, k);
            transform.position = basePosition + transform.right * shake.x + transform.up * shake.y;
        }

        /// <summary>
        /// 画面揺れの現在のずれ(カメラの右/上方向、ワールド単位)。基準位置に足して表示する。
        /// </summary>
        public void SetShake(Vector2 offset)
        {
            shake = offset;
        }

        /// <summary>
        /// 新しいタワーの高さに合わせて、カメラの目標位置と向きを決める。
        /// </summary>
        public void FrameTower(float towerHeight)
        {
            if (cam == null)
            {
                return;
            }

            float halfFov = fieldOfView * 0.5f * Mathf.Deg2Rad;
            float visibleHeightPerDistance = 2f * Mathf.Tan(halfFov);
            float distance = Mathf.Clamp(towerHeight / (towerScreenFraction * visibleHeightPerDistance), minDistance, maxDistance);

            Vector3 center = new Vector3(0f, towerHeight * 0.5f, 0f);
            float elevation = elevationDegrees * Mathf.Deg2Rad;
            Vector3 offset = horizontalDirection * (distance * Mathf.Cos(elevation)) + Vector3.up * (distance * Mathf.Sin(elevation));
            targetPosition = center + offset;

            // 視点を少し上へ向けると、タワーが画面の下寄りに写る
            float visibleHeight = distance * visibleHeightPerDistance;
            Vector3 lookAt = center + Vector3.up * (visibleHeight * towerDownShift);
            targetRotation = Quaternion.LookRotation(lookAt - targetPosition, Vector3.up);

            if (!hasTarget)
            {
                basePosition = targetPosition;
                transform.position = targetPosition;
                transform.rotation = targetRotation;
            }
            hasTarget = true;
        }
    }
}
