using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OneTapDemolition
{
    /// <summary>
    /// メインカメラにアタッチする。押している間は狙った階をハイライトして予測スコアを出し(AimChanged)、
    /// 指を離した瞬間にその階を崩す。タップ(素早く離す)でもそのまま撃てる。
    /// 階の上で押して階の外で離した場合はキャンセルされ、ショットは消費されない。
    /// 触りやすさのため、ビルの見た目より広い範囲(左右・上下)を「ビルを押した」とみなし、最も近い階に吸着させる。
    /// </summary>
    public class TapDemolishController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private LayerMask floorLayerMask = ~0;
        [SerializeField] private float maxRayDistance = 200f;

        [Header("Touch Forgiveness (画面に対する割合)")]
        [Tooltip("ビルの左右にどれだけ余裕を持たせるか(画面幅比)。")]
        [SerializeField] private float sideForgiveness = 0.14f;
        [Tooltip("階と階の間や階の上下の隙間を拾う余裕(画面高比)。")]
        [SerializeField] private float gapForgiveness = 0.02f;
        [Tooltip("ビルの頭上・足元をどれだけ拾うか(画面高比)。上端より上は最上階、下端より下は最下階を狙う。")]
        [SerializeField] private float edgeForgiveness = 0.09f;
        [Tooltip("タッチの指先は接触の中心より少し上に感じられるので、判定位置を上へずらす量(画面高比。タッチのみ)。")]
        [SerializeField] private float touchVerticalOffset = 0.012f;
        [Tooltip("狙いが隣の階へ切り替わるのを少し遅らせて、指の細かいブレで狙いがチラつかないようにする量(階の高さ比)。")]
        [SerializeField] private float stickiness = 0.3f;
        [Tooltip("階が画面上でこのピクセルより小さいとき(背の高いビル)は、押した階を起点に「指をこのピクセル動かすと1階分」動く精密モードにする。")]
        [SerializeField] private float minEffectiveFloorPixels = 130f;
        [Tooltip("精密モードで、ビルの中心から横にこれだけ(画面幅比)離れるとキャンセル扱いにする。")]
        [SerializeField] private float cancelSideDistance = 0.42f;

        private static readonly RaycastHit[] HitBuffer = new RaycastHit[24];
        private static readonly Vector3[] CornerBuffer = new Vector3[8];

        private bool pressing;
        private Floor aimFloor;
        private Vector3 aimHitPoint;
        private int aimFingerId = -1;
        private bool hasAnchor;
        private bool relativeMode;
        private int anchorIndex;
        private float anchorY;

        /// <summary>
        /// (狙い中か, 指の画面座標, 狙っている階の番号(無ければ-1), 予測スコア)
        /// </summary>
        public event Action<bool, Vector2, int, int> AimChanged;

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
                Fire();
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
                position = touch.position + new Vector2(0f, Screen.height * touchVerticalOffset);
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
            BuildingTower tower = gm.CurrentTower;
            if (tower == null)
            {
                return;
            }

            if (hasAnchor && relativeMode)
            {
                aimFloor = PickRelative(screenPosition, tower, out aimHitPoint);
            }
            else
            {
                aimFloor = PickFloor(screenPosition, tower, out aimHitPoint);
                if (aimFloor != null && !hasAnchor)
                {
                    BeginAnchor(aimFloor, screenPosition, tower);
                }
            }

            if (aimFloor == null)
            {
                tower.SetAimPreview(-1);
                AimChanged?.Invoke(true, screenPosition, -1, 0);
                return;
            }

            int score;
            tower.PredictTap(aimFloor.FloorIndex, out score);
            tower.SetAimPreview(aimFloor.FloorIndex);
            AimChanged?.Invoke(true, screenPosition, aimFloor.FloorIndex, score);
        }

        /// <summary>
        /// 最初に階を捉えた位置を記録する。階が小さい(背の高いビル)なら、以後は相対移動の精密モードにする。
        /// </summary>
        private void BeginAnchor(Floor floor, Vector2 screenPosition, BuildingTower tower)
        {
            hasAnchor = true;
            anchorIndex = floor.FloorIndex;
            anchorY = screenPosition.y;
            relativeMode = AverageFloorPixels(tower) < minEffectiveFloorPixels;
        }

        private float AverageFloorPixels(BuildingTower tower)
        {
            float sum = 0f;
            int n = 0;
            for (int i = 0; i < tower.AliveCount && i < tower.Floors.Count; i++)
            {
                Rect rect;
                if (tower.Floors[i] != null && !tower.Floors[i].IsDemolished && ScreenRectOf(tower.Floors[i], out rect))
                {
                    sum += rect.height;
                    n++;
                }
            }
            return n > 0 ? sum / n : float.MaxValue;
        }

        /// <summary>
        /// 精密モード: 押した階を起点に、指の上下移動量(minEffectiveFloorPixelsで1階)だけ狙いを動かす。
        /// 実際の階より大きい移動量で動くので、小さい階でも狙いを合わせやすい。
        /// </summary>
        private Floor PickRelative(Vector2 screenPosition, BuildingTower tower, out Vector3 hitPoint)
        {
            hitPoint = default;
            int count = Mathf.Min(tower.AliveCount, tower.Floors.Count);
            if (count <= 0)
            {
                return null;
            }

            Floor anchor = tower.Floors[Mathf.Clamp(anchorIndex, 0, count - 1)];
            Rect anchorRect;
            if (anchor != null && ScreenRectOf(anchor, out anchorRect))
            {
                if (Mathf.Abs(screenPosition.x - anchorRect.center.x) > Screen.width * cancelSideDistance)
                {
                    return null;
                }
            }

            int index = Mathf.Clamp(anchorIndex + Mathf.RoundToInt((screenPosition.y - anchorY) / minEffectiveFloorPixels), 0, count - 1);
            Floor floor = tower.Floors[index];
            if (floor == null || floor.IsDemolished)
            {
                return null;
            }
            hitPoint = floor.transform.position + (targetCamera.transform.position - floor.transform.position).normalized * 0.5f;
            return floor;
        }

        /// <summary>
        /// まず正確なレイキャストで階を探し、外れたら画面上でいちばん近い階に吸着する。
        /// </summary>
        private Floor PickFloor(Vector2 screenPosition, BuildingTower tower, out Vector3 hitPoint)
        {
            hitPoint = default;
            if (targetCamera == null)
            {
                return null;
            }

            Ray ray = targetCamera.ScreenPointToRay(screenPosition);
            int count = Physics.RaycastNonAlloc(ray, HitBuffer, maxRayDistance, floorLayerMask);
            Floor exact = null;
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Floor f = HitBuffer[i].collider.GetComponentInParent<Floor>();
                if (f != null && !f.IsDemolished && f.OwnerTower == tower && HitBuffer[i].distance < nearest)
                {
                    nearest = HitBuffer[i].distance;
                    exact = f;
                    hitPoint = HitBuffer[i].point;
                }
            }
            Floor picked = exact != null ? exact : SnapToNearestFloor(screenPosition, tower, out hitPoint);
            return KeepPreviousIfClose(picked, screenPosition);
        }

        /// <summary>
        /// 直前に狙っていた階の近くにまだ指があるなら、そのまま狙い続ける(切り替わりのチラつき防止)。
        /// </summary>
        private Floor KeepPreviousIfClose(Floor picked, Vector2 screenPosition)
        {
            Floor previous = aimFloor;
            if (previous == null || previous == picked || previous.IsDemolished || picked == null)
            {
                return picked;
            }

            Rect rect;
            if (!ScreenRectOf(previous, out rect))
            {
                return picked;
            }

            float pad = rect.height * stickiness;
            Rect grown = new Rect(rect.xMin - Screen.width * sideForgiveness, rect.yMin - pad,
                rect.width + Screen.width * sideForgiveness * 2f, rect.height + pad * 2f);
            if (!grown.Contains(screenPosition))
            {
                return picked;
            }

            aimHitPoint = previous.transform.position + (targetCamera.transform.position - previous.transform.position).normalized * 0.5f;
            return previous;
        }

        private Floor SnapToNearestFloor(Vector2 screenPosition, BuildingTower tower, out Vector3 hitPoint)
        {
            hitPoint = default;
            float tolSide = Screen.width * sideForgiveness;
            float tolGap = Screen.height * gapForgiveness;
            float tolEdge = Screen.height * edgeForgiveness;

            Floor best = null;
            float bestDistance = float.MaxValue;
            Floor bottom = null;
            Floor top = null;
            float bottomY = float.MaxValue;
            float topY = float.MinValue;
            float minX = float.MaxValue;
            float maxX = float.MinValue;

            var floors = tower.Floors;
            for (int i = 0; i < floors.Count && i < tower.AliveCount; i++)
            {
                Floor f = floors[i];
                if (f == null || f.IsDemolished)
                {
                    continue;
                }

                Rect rect;
                if (!ScreenRectOf(f, out rect))
                {
                    continue;
                }

                minX = Mathf.Min(minX, rect.xMin);
                maxX = Mathf.Max(maxX, rect.xMax);
                if (rect.yMin < bottomY)
                {
                    bottomY = rect.yMin;
                    bottom = f;
                }
                if (rect.yMax > topY)
                {
                    topY = rect.yMax;
                    top = f;
                }

                Rect grown = new Rect(rect.xMin - tolSide, rect.yMin - tolGap, rect.width + tolSide * 2f, rect.height + tolGap * 2f);
                if (grown.Contains(screenPosition))
                {
                    float d = Mathf.Abs(screenPosition.y - rect.center.y);
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = f;
                    }
                }
            }

            if (best == null && bottom != null)
            {
                bool insideSide = screenPosition.x >= minX - tolSide && screenPosition.x <= maxX + tolSide;
                if (insideSide && screenPosition.y < bottomY && screenPosition.y >= bottomY - tolEdge)
                {
                    best = bottom;
                }
                else if (insideSide && screenPosition.y > topY && screenPosition.y <= topY + tolEdge)
                {
                    best = top;
                }
            }

            if (best != null)
            {
                Vector3 toCamera = (targetCamera.transform.position - best.transform.position).normalized;
                hitPoint = best.transform.position + toCamera * 0.5f;
            }
            return best;
        }

        private bool ScreenRectOf(Floor floor, out Rect rect)
        {
            rect = default;
            Renderer r = floor.GetComponent<Renderer>();
            if (r == null)
            {
                return false;
            }

            Bounds b = r.bounds;
            Vector3 c = b.center;
            Vector3 e = b.extents;
            int n = 0;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        CornerBuffer[n++] = c + new Vector3(e.x * x, e.y * y, e.z * z);
                    }
                }
            }

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            foreach (Vector3 corner in CornerBuffer)
            {
                Vector3 p = targetCamera.WorldToScreenPoint(corner);
                if (p.z <= 0f)
                {
                    return false;
                }
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }
            rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        private void Fire()
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
            hasAnchor = false;
            relativeMode = false;
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.CurrentTower != null)
            {
                gm.CurrentTower.SetAimPreview(-1);
            }
            AimChanged?.Invoke(false, Vector2.zero, -1, 0);
        }
    }
}
