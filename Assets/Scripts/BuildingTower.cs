using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// StageSpecに従ってFloorを下から積み上げ、タップされた階より上の階を連鎖的に崩落させる。
    /// 崩落中に通過したゲート階の倍率でスコアを増幅し、保護階を壊したらステージ失敗をGameManagerへ伝える。
    /// </summary>
    public class BuildingTower : MonoBehaviour
    {
        [Header("Build Settings")]
        [SerializeField] private Floor floorPrefab;
        [SerializeField] private float floorHeight = 1f;

        [Header("Chain Settings")]
        [Tooltip("階が揺れてヒビが入ってから崩れるまでの時間(秒)。何が壊れるかを目で追えるようにする。")]
        [SerializeField] private float crackDuration = 0.09f;
        [Tooltip("崩れてから次の階が揺れ始めるまでの最初の間隔(秒)。段が進むごとにchainGapAccelerationずつ縮まる。")]
        [SerializeField] private float chainGap = 0.08f;
        [SerializeField] private float chainGapAcceleration = 0.005f;
        [SerializeField] private float minChainGap = 0.025f;

        private readonly List<Floor> floors = new List<Floor>();
        private StageSpec spec;
        private int limit;
        private bool resolving;

        public StageSpec Spec => spec;
        public int FloorCount => floors.Count;
        public int AliveCount => limit;
        public IReadOnlyList<Floor> Floors => floors;
        public bool IsResolving => resolving;

        public float TotalHeight => floors.Count * floorHeight;

        public void BuildTower(StageSpec stageSpec)
        {
            ClearExisting();
            spec = stageSpec;

            for (int i = 0; i < spec.Floors.Length; i++)
            {
                Floor floor = Instantiate(floorPrefab, transform);
                floor.transform.localPosition = new Vector3(0f, i * floorHeight, 0f);
                floor.Setup(i, this, spec.Floors[i]);
                floors.Add(floor);
            }

            limit = floors.Count;
            resolving = false;
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
            limit = 0;
        }

        /// <summary>
        /// 指定階をタップした場合の予測スコア。
        /// </summary>
        public bool PredictTap(int index, out int score)
        {
            score = 0;
            if (spec == null || index < 0 || index >= limit)
            {
                return false;
            }

            float historyBonus = ScoreManager.Instance != null ? ScoreManager.Instance.HistoryBonus : 0f;
            score = ScoreRules.SimulateTap(spec.Floors, index, limit, historyBonus);
            return true;
        }

        /// <summary>
        /// 狙っている階(index)と、その上で巻き込まれる階をハイライトする。-1で解除。
        /// </summary>
        public void SetAimPreview(int index)
        {
            for (int i = 0; i < floors.Count; i++)
            {
                Floor floor = floors[i];
                if (floor == null || floor.IsDemolished)
                {
                    continue;
                }

                AimState state = AimState.None;
                if (index >= 0 && i == index)
                {
                    state = AimState.Target;
                }
                else if (index >= 0 && i > index && i < limit)
                {
                    state = AimState.InChain;
                }
                floor.SetAim(state);
            }
        }

        /// <summary>
        /// TapDemolishControllerからのタップ確定を受けて、その階と上階の連鎖崩落を開始する。
        /// ショットを消費できなかった場合(ステージ外・崩落中・弾切れ)は何もしない。
        /// </summary>
        public bool RequestDemolish(Floor tappedFloor, Vector3 hitPoint)
        {
            if (resolving || tappedFloor == null || tappedFloor.IsDemolished || tappedFloor.FloorIndex >= limit)
            {
                return false;
            }
            if (GameManager.Instance == null || !GameManager.Instance.TryConsumeShot())
            {
                return false;
            }

            resolving = true;
            SetAimPreview(-1);

            int startIndex = tappedFloor.FloorIndex;
            JuiceManager.Instance?.TriggerImpact(limit - startIndex);
            StartCoroutine(CollapseFromFloor(startIndex, hitPoint));
            return true;
        }

        private IEnumerator CollapseFromFloor(int startIndex, Vector3 hitPoint)
        {
            int chainStep = 0;
            float gateProduct = 1f;

            for (int i = startIndex; i < limit; i++)
            {
                Floor floor = floors[i];
                if (floor == null || floor.IsDemolished)
                {
                    continue;
                }

                // 揺れとヒビ → 崩壊 → ポイントが飛び出す、の順に見せる
                floor.StartCrack(crackDuration);
                yield return new WaitForSeconds(crackDuration);

                chainStep++;
                FloorSpec spec = floor.Spec;

                if (spec.Kind == FloorKind.Gate)
                {
                    gateProduct *= spec.GateValue;
                    ScoreFeedbackUI.Instance?.ShowGateBanner(spec.GateValue, gateProduct);
                    JuiceManager.Instance?.PlayGate(gateProduct);
                }

                Vector3 floorPosition = floor.transform.position;
                DemolishOne(floor, hitPoint, chainStep, gateProduct);
                GameManager.Instance?.NotifyChainStep(chainStep);

                if (spec.Kind == FloorKind.Bonus)
                {
                    GameManager.Instance?.TryAwardStar(StarReason.BonusFloor, floorPosition);
                }

                yield return new WaitForSeconds(GapForStep(chainStep));
            }

            limit = startIndex;
            resolving = false;
            GameManager.Instance?.OnChainFinished();
        }

        /// <summary>
        /// 連鎖が進むほど間隔を詰めて、崩落が加速していく感覚を出す。
        /// </summary>
        private float GapForStep(int chainStep)
        {
            return Mathf.Max(minChainGap, chainGap - chainGapAcceleration * Mathf.Max(0, chainStep - 1));
        }

        private void DemolishOne(Floor floor, Vector3 hitPoint, int chainStep, float gateProduct)
        {
            FreeFromNeighborCollisions(floor);
            Vector3 popupPosition = floor.transform.position;
            FloorKind kind = floor.Spec.Kind;
            floor.Demolish(hitPoint, chainStep);

            if (ScoreManager.Instance != null)
            {
                int scoreAdded = ScoreManager.Instance.AddChainScore(chainStep, gateProduct, kind);
                JuiceManager.Instance?.ShowScorePopup(popupPosition, scoreAdded);
            }
        }
        /// <summary>
        /// 崩落した階が他の階と衝突して突っかからないよう、衝突判定を無効化する。
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
