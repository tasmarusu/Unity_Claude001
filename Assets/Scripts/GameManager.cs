using System;
using UnityEngine;

namespace OneTapDemolition
{
    public enum GameState
    {
        Ready,
        Playing,
        Resolving,
        Result
    }

    public class StageResult
    {
        public StageSpec Spec;
        public bool Failed;
        public int Score;
        public int Stars;
        public int ShotsUsed;
        public bool NewBest;
        public BuildingHistoryManager.Entry? Unlocked;
    }

    /// <summary>
    /// ステージ進行(準備→プレイ→崩落中→リザルト)と、ショット数・星評価・進行度の保存を制御する。
    /// UIはここのイベントを購読するだけで、進行ロジックは持たない。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private const string StageIndexKey = "OneTapDemolition_StageIndex";

        public static GameManager Instance { get; private set; }

        [Header("Tower Spawning")]
        [SerializeField] private BuildingTower[] towerPrefabs;
        [SerializeField] private Transform towerSpawnPoint;

        [Header("Flow")]
        [Tooltip("最後の崩落が終わってからリザルトを出すまでの待ち(破片が落ち着くのを見せる)。")]
        [SerializeField] private float resultDelay = 1.0f;

        private BuildingTower currentTower;
        private int shotsUsed;
        private int totalShots;

        public event Action<StageSpec, int> StageStarted;
        public event Action<int, int> ShotsChanged;
        public event Action<StageResult> StageEnded;
        public event Action<GameState> StateChanged;

        public GameState State { get; private set; } = GameState.Ready;
        public StageSpec CurrentSpec { get; private set; }
        public int CurrentStageIndex { get; private set; } = 1;
        public int ShotsLeft { get; private set; }
        public BuildingTower CurrentTower => currentTower;

        public bool CanShoot => State == GameState.Playing && ShotsLeft > 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            CurrentStageIndex = Mathf.Max(1, PlayerPrefs.GetInt(StageIndexKey, 1));
            PrepareStage(CurrentStageIndex, 0);
        }

        /// <summary>
        /// ステージのタワーを組み立てて待機状態(Ready)にする。メニューの「PLAY」でBeginPlayを呼ぶ。
        /// </summary>
        public void PrepareStage(int stageIndex, int extraShots)
        {
            CancelInvoke(nameof(ShowResult));
            if (currentTower != null)
            {
                Destroy(currentTower.gameObject);
            }

            float historyBonus = BuildingHistoryManager.Instance != null ? BuildingHistoryManager.Instance.HistoryBonusMultiplier : 0f;
            CurrentSpec = StageGenerator.Generate(stageIndex, historyBonus);
            CurrentStageIndex = stageIndex;

            Vector3 spawnPosition = towerSpawnPoint != null ? towerSpawnPoint.position : Vector3.zero;
            BuildingTower prefab = towerPrefabs[(stageIndex - 1) % towerPrefabs.Length];
            currentTower = Instantiate(prefab, spawnPosition, Quaternion.identity);
            currentTower.BuildTower(CurrentSpec);
            CameraFraming.Instance?.FrameTower(currentTower.TotalHeight);

            ScoreManager.Instance?.ResetScore();

            totalShots = CurrentSpec.Shots + extraShots;
            ShotsLeft = totalShots;
            shotsUsed = 0;
            SetState(GameState.Ready);
            StageStarted?.Invoke(CurrentSpec, totalShots);
            ShotsChanged?.Invoke(ShotsLeft, totalShots);
        }

        public void BeginPlay()
        {
            if (State == GameState.Ready)
            {
                SetState(GameState.Playing);
            }
        }

        /// <summary>
        /// リザルトから次へ。成功した場合は進んだ次のステージ、失敗した場合は同じステージ。
        /// </summary>
        public void StartNextStage()
        {
            AdsManager.Instance?.NotifyTowerCleared();
            PrepareStage(CurrentStageIndex, 0);
            BeginPlay();
        }

        /// <summary>
        /// 同じステージをもう一度。extraShotsはリワード広告で得た追加ショット。
        /// </summary>
        public void RetryStage(int extraShots)
        {
            AdsManager.Instance?.NotifyTowerCleared();
            int index = CurrentSpec != null ? CurrentSpec.StageIndex : CurrentStageIndex;
            PrepareStage(index, extraShots);
            BeginPlay();
        }

        public bool TryConsumeShot()
        {
            if (!CanShoot)
            {
                return false;
            }

            ShotsLeft--;
            shotsUsed++;
            SetState(GameState.Resolving);
            ShotsChanged?.Invoke(ShotsLeft, totalShots);
            return true;
        }

        /// <summary>
        /// BuildingTowerの連鎖崩落が終わったときに呼ばれる。次のショットへ進むか、ステージを終えるかを決める。
        /// </summary>
        public void OnChainFinished(bool protectedHit)
        {
            bool towerDone = currentTower == null
                || currentTower.AliveCount <= 0
                || !ScoreRules.HasSafeTap(CurrentSpec.Floors, currentTower.AliveCount);

            if (protectedHit || ShotsLeft <= 0 || towerDone)
            {
                EndStage(protectedHit);
                return;
            }

            SetState(GameState.Playing);
        }

        private void EndStage(bool failed)
        {
            SetState(GameState.Result);
            pendingFailed = failed;
            Invoke(nameof(ShowResult), resultDelay);
        }

        private bool pendingFailed;

        private void ShowResult()
        {
            int score = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0;
            StageResult result = new StageResult
            {
                Spec = CurrentSpec,
                Failed = pendingFailed,
                Score = score,
                Stars = CurrentSpec.StarsFor(score, pendingFailed),
                ShotsUsed = shotsUsed
            };

            if (!pendingFailed)
            {
                ScoreManager.Instance?.CommitBestScore();
                result.Unlocked = BuildingHistoryManager.Instance?.TryUnlockNext();
                CurrentStageIndex = CurrentSpec.StageIndex + 1;
                PlayerPrefs.SetInt(StageIndexKey, CurrentStageIndex);
                PlayerPrefs.Save();
            }

            StageEnded?.Invoke(result);
        }

        private void SetState(GameState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
