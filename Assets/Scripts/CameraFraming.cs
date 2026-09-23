using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// タワーの高さ(階数×階高)に応じてメインカメラのFOVを自動調整し、
    /// 短いタワーが画面の中でスカスカに小さく写る/高いタワーが窮屈に写るのを防ぐ。
    /// GameManager.SpawnNewTowerから、新しいタワーが積み上がった直後に呼ばれる。
    /// シーンへの手動配置不要で、Main Cameraに自動アタッチする。
    /// </summary>
    public class CameraFraming : MonoBehaviour
    {
        public static CameraFraming Instance { get; private set; }

        [SerializeField] private float baseFov = 60f;
        [SerializeField] private float referenceTowerHeight = 9.5f;
        [SerializeField] private float fovPerHeightUnit = 6f;
        [SerializeField] private float minFov = 48f;
        [SerializeField] private float maxFov = 72f;
        [SerializeField] private float blendSpeed = 4f;

        private Camera cam;
        private float targetFov;

        /// <summary>
        /// シーンへの手動配置不要で自動的に生き始める。他セッションによるシーン上書きの影響を受けない。
        /// </summary>
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
            targetFov = cam != null ? cam.fieldOfView : baseFov;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (cam == null)
            {
                return;
            }

            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, Time.unscaledDeltaTime * blendSpeed);
        }

        /// <summary>
        /// 新しいタワーの高さに合わせて、画面いっぱいに収まるようFOVを再計算する。
        /// </summary>
        public void FrameTower(float towerHeight)
        {
            float delta = towerHeight - referenceTowerHeight;
            targetFov = Mathf.Clamp(baseFov + delta * fovPerHeightUnit, minFov, maxFov);
        }
    }
}
