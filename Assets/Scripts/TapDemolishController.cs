using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// メインカメラにアタッチする。タップ(モバイル)/クリック(エディタ)両対応のレイキャストで階を判定する。
    /// </summary>
    public class TapDemolishController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private LayerMask floorLayerMask = ~0;
        [SerializeField] private float maxRayDistance = 200f;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (TryGetTapScreenPosition(out Vector2 screenPosition))
            {
                TryDemolishAt(screenPosition);
            }
        }

        private bool TryGetTapScreenPosition(out Vector2 screenPosition)
        {
            // モバイル実機でのタップ
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    screenPosition = touch.position;
                    return true;
                }
            }

            // エディタ / PC実行時のマウスクリック
            if (Input.GetMouseButtonDown(0))
            {
                screenPosition = Input.mousePosition;
                return true;
            }

            screenPosition = default;
            return false;
        }

        private void TryDemolishAt(Vector2 screenPosition)
        {
            if (targetCamera == null)
            {
                return;
            }

            Ray ray = targetCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, floorLayerMask))
            {
                Floor floor = hit.collider.GetComponentInParent<Floor>();
                if (floor != null && floor.OwnerTower != null)
                {
                    floor.OwnerTower.RequestDemolish(floor, hit.point);
                }
            }
        }
    }
}
