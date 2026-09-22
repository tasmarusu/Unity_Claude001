using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// Floorプレハブを指定階数分積み上げて生成し、タップされた階より上の階を連鎖的に崩落させる。
    /// </summary>
    public class BuildingTower : MonoBehaviour
    {
        [Header("Build Settings")]
        [SerializeField] private Floor floorPrefab;
        [SerializeField] private int floorCount = 10;
        [SerializeField] private float floorHeight = 1f;

        [Header("Chain Settings")]
        [SerializeField] private float chainDelay = 0.05f;

        private readonly List<Floor> floors = new List<Floor>();
        private int aliveFloorCount;

        public bool IsCleared => aliveFloorCount <= 0;
        public int FloorCount => floors.Count;

        /// <summary>
        /// 指定階数分のFloorを下から積み上げて生成する。
        /// </summary>
        public void BuildTower()
        {
            ClearExisting();

            for (int i = 0; i < floorCount; i++)
            {
                Floor floor = Instantiate(floorPrefab, transform);
                floor.transform.localPosition = new Vector3(0f, i * floorHeight, 0f);
                floor.Setup(i, this);
                floors.Add(floor);
            }

            aliveFloorCount = floors.Count;
        }

        private void ClearExisting()
        {
            foreach (Floor floor in floors)
            {
                if (floor != null)
                {
                    Destroy(floor.gameObject);
                }
            }

            floors.Clear();
            aliveFloorCount = 0;
        }

        /// <summary>
        /// TapDemolishControllerからのタップ判定を受けて、その階と上階の連鎖崩落を開始する。
        /// </summary>
        public void RequestDemolish(Floor tappedFloor, Vector3 hitPoint)
        {
            if (tappedFloor == null || tappedFloor.IsDemolished)
            {
                return;
            }

            int totalChainCount = CountRemainingFromIndex(tappedFloor.FloorIndex);
            JuiceManager.Instance?.TriggerImpact(totalChainCount);

            StartCoroutine(CollapseFromFloor(tappedFloor.FloorIndex, hitPoint));
        }

        private int CountRemainingFromIndex(int startIndex)
        {
            int count = 0;
            for (int i = startIndex; i < floors.Count; i++)
            {
                if (floors[i] != null && !floors[i].IsDemolished)
                {
                    count++;
                }
            }
            return count;
        }

        private IEnumerator CollapseFromFloor(int startIndex, Vector3 hitPoint)
        {
            int chainStep = 0;

            for (int i = startIndex; i < floors.Count; i++)
            {
                Floor floor = floors[i];
                if (floor == null || floor.IsDemolished)
                {
                    continue;
                }

                chainStep++;
                FreeFromNeighborCollisions(floor);
                floor.Demolish(hitPoint, chainStep);
                aliveFloorCount--;

                if (ScoreManager.Instance != null)
                {
                    int scoreAdded = ScoreManager.Instance.AddChainScore(chainStep);
                    JuiceManager.Instance?.ShowScorePopup(floor.transform.position, scoreAdded);
                }

                if (i > startIndex)
                {
                    yield return new WaitForSeconds(chainDelay);
                }
            }

            if (IsCleared)
            {
                GameManager.Instance?.OnTowerCleared();
            }
        }

        /// <summary>
        /// 崩落した階が他の階(残存階・既に崩落した階)と衝突して突っかからないよう、衝突判定を無効化する。
        /// </summary>
        private void FreeFromNeighborCollisions(Floor floor)
        {
            Collider floorCollider = floor.GetComponent<Collider>();
            if (floorCollider == null)
            {
                return;
            }

            foreach (Floor other in floors)
            {
                if (other == null || other == floor)
                {
                    continue;
                }

                Collider otherCollider = other.GetComponent<Collider>();
                if (otherCollider != null)
                {
                    Physics.IgnoreCollision(floorCollider, otherCollider, true);
                }
            }
        }
    }
}
