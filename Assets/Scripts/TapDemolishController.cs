using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OneTapDemolition
{
    /// <summary>
    /// メインカメラにアタッチする。押している間は狙った階をハイライトして予測スコアを出し(AimChanged)、
    /// 指を離した瞬間にその階を崩す。タップ(素早く離す)でもそのまま撃てる。
    /// 階の上で押して階の外で離した場合はキャンセルされ、ショットは消費されない。
    /// </summary>
    public class TapDemolishController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private LayerMask floorLayerMask = ~0;
        [SerializeField] private float maxRayDistance = 200f;

        private static readonly RaycastHit[] HitBuffer = new RaycastHit[24];

        private bool pressing;
        private Floor aimFloor;
        private Vector3 aimHitPoint;
        private int aimFingerId = -1;

        /// <summary>
        /// (狙い中か, 指の画面座標, 予測スコア, 保護階を巻き込むか, 狙える階があるか)
        /// </summary>
        public event Action<bool, Vector2, int, bool, bool> AimChanged;

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
            Vector2 position;
            bool down, held, up;
            ReadPointer(out position, out down, out held, out up);

            GameManager gm = GameManager.Instance;
            if (gm == null || !gm.CanShoot)
            {
                CancelAim();
                return;
            }

            if (down && !pressing)
            {
                if (IsOverUI())
                {
                    return;
                }
                pressing = true;
            }

            if (!pressing)
            {
                return;
            }

            UpdateAim(position, gm);

            if (up)
            {
                Fire(position);
            }
            else if (!held)
            {
                CancelAim();
            }
        }

        private void ReadPointer(out Vector2 position, out bool down, out bool held, out bool up)
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                aimFingerId = touch.fingerId;
                position = touch.position;
                down = touch.phase == TouchPhase.Began;
                up = touch.phase == TouchPhase.Ended;
                held = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
                return;
            }

            aimFingerId = -1;
            position = Input.mousePosition;
            down = Input.GetMouseButtonDown(0);
            up = Input.GetMouseButtonUp(0);
            held = Input.GetMouseButton(0);
        }

        private bool IsOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(aimFingerId);
        }

        private void UpdateAim(Vector2 screenPosition, GameManager gm)
        {
            Floor hitFloor = null;
            Vector3 hitPoint = default;
            if (targetCamera != null)
            {
                Ray ray = targetCamera.ScreenPointToRay(screenPosition);
                int count = Physics.RaycastNonAlloc(ray, HitBuffer, maxRayDistance, floorLayerMask);
                float nearest = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    Floor f = HitBuffer[i].collider.GetComponentInParent<Floor>();
                    if (f != null && !f.IsDemolished && f.OwnerTower == gm.CurrentTower && HitBuffer[i].distance < nearest)
                    {
                        nearest = HitBuffer[i].distance;
                        hitFloor = f;
                        hitPoint = HitBuffer[i].point;
                    }
                }
            }

            aimFloor = hitFloor;
            aimHitPoint = hitPoint;

            BuildingTower tower = gm.CurrentTower;
            if (tower == null)
            {
                return;
            }

            if (aimFloor == null)
            {
                tower.SetAimPreview(-1);
                AimChanged?.Invoke(true, screenPosition, 0, false, false);
                return;
            }

            int score;
            bool danger;
            tower.PredictTap(aimFloor.FloorIndex, out score, out danger);
            tower.SetAimPreview(aimFloor.FloorIndex);
            AimChanged?.Invoke(true, screenPosition, score, danger, true);
        }

        private void Fire(Vector2 screenPosition)
        {
            Floor floor = aimFloor;
            Vector3 hitPoint = aimHitPoint;
            CancelAim();

            if (floor != null && floor.OwnerTower != null)
            {
                floor.OwnerTower.RequestDemolish(floor, hitPoint);
            }
        }

        private void CancelAim()
        {
            if (!pressing && aimFloor == null)
            {
                return;
            }

            pressing = false;
            aimFloor = null;
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.CurrentTower != null)
            {
                gm.CurrentTower.SetAimPreview(-1);
            }
            AimChanged?.Invoke(false, Vector2.zero, 0, false, false);
        }
    }
}
