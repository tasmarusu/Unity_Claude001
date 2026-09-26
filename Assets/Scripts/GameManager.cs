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
        public bool[] StarFlags;
        public int ShotsUsed;
        public BuildingHistoryManager.Entry? Unlocked;
    }

    /// <summary>
    /// ステージ進行(準備→プレイ→崩落中→リザルト)と、ショット数・ゲージMAX(クリア)・☆・進行度の保存を制御する。
    /// UIはここのイベントを購読するだけで、進行ロジックは持たない。
    /// ☆は3種類(すべて画面上に印がある): ボーナス階の破壊 / ゲージのMAXに到達 / ゲージ右端の☆マークに到達。
    /// MAXに届いてもショットと階が残っていれば続けられる(☆マークを狙う)。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private const string StageIndexKey = "OneTapDemolition_StageIndex";

        public static GameManager Instance { get; private set; }

        [Header("Tower Spawning")]
        [SerializeField] private BuildingTower[] towerPrefabs;
        [SerializeField] private Transform towerSpawnPoint;

        [Header("Flow")]
        [Tooltip("最後の崩落が終わってからリザルトを出すまでの待ち。飛んでいるポイントとMAX演出が完了するのを待つ。")]
        [SerializeField] private float resultDelay = 1.6f;

        private BuildingTower currentTower;
        private int shotsUsed;
        private int totalShots;
        private bool gaugeMaxAnnounced;
        private bool pendingFailed;
        private readonly bool[] starFlags = new bool[3];

        public event Action<StageSpec, int> StageStarted;
        public event Action<int, int> ShotsChanged;
        public event Action<StageResult> StageEnded;
        public event Action<GameState> StateChanged;
        public event Action<StarReason, Vector3?> StarAwarded;
        public event Action GaugeMaxReached;
        public event Action<int> ComboChanged;

        public GameState State { get; private set; } = GameState.Ready;
        public StageSpec CurrentSpec { get; private set; }
        public int CurrentStageIndex { get; private set; } = 1;
        public int ShotsLeft { get; private set; }
        public BuildingTower CurrentTower => currentTower;

        public bool CanShoot => State == GameState.Playing && ShotsLeft > 0;

        public bool HasStar(StarReason reason)
        {
            return starFlags[(int)reason];
        }

        public int StarCount
        {
            get
            {
                int n = 0;
                foreach (bool f in starFlags)
                {
                    n += f ? 1 : 0;
                }
                return n;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.DisplayedScoreChanged += OnDisplayedScoreChanged;
            }
        }

        private void OnDisable()
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.DisplayedScoreChanged -= OnDisplayedScoreChanged;
            }
        }

        private void Start()
        {
            // ScoreManagerのAwakeがこのOnEnableより後の場合に備えて、ここでも購読を保証する
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.DisplayedScoreChanged -= OnDisplayedScoreChanged;
                ScoreManager.Instance.DisplayedScoreChanged += OnDisplayedScoreChanged;
            }

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

            Array.Clear(starFlags, 0, starFlags.Length);
            gaugeMaxAnnounced = false;
            pendingFailed = false;
            totalShots = CurrentSpec.Shots + extraShots;
            ShotsLeft = totalShots;
            shotsUsed = 0;
            SetState(GameState.Ready);
            StageStarted?.Invoke(CurrentSpec, totalShots);
            ShotsChanged?.Invoke(ShotsLeft, totalShots);
            ComboChanged?.Invoke(0);
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
        /// 連鎖の何段目まで崩れたか(1発ごと)。HUDのコンボ表示に使う。
        /// </summary>
        public void NotifyChainStep(int chainStep)
        {
            ComboChanged?.Invoke(chainStep);
        }

        /// <summary>
        /// ☆を1つ与える(理由ごとに1ステージ1回)。worldPositionがあれば、その階から☆が飛び出す。
        /// </summary>
        public void TryAwardStar(StarReason reason, Vector3? worldPosition)
        {
            int slot = (int)reason;
            if (starFlags[slot] || (State != GameState.Playing && State != GameState.Resolving && State != GameState.Result))
            {
                return;
            }

            starFlags[slot] = true;
            StarAwarded?.Invoke(reason, worldPosition);
        }

        private void OnDisplayedScoreChanged(int added, int displayed)
        {
            if (CurrentSpec == null)
            {
                return;
            }

            if (!gaugeMaxAnnounced && displayed >= CurrentSpec.TargetScore && displayed > 0)
            {
                gaugeMaxAnnounced = true;
                TryAwardStar(StarReason.Max, null);
                GaugeMaxReached?.Invoke();
            }
            if (displayed >= CurrentSpec.StarScore && displayed > 0)
            {
                TryAwardStar(StarReason.Overshoot, null);
            }
        }

        /// <summary>
        /// BuildingTowerの連鎖崩落が終わったときに呼ばれる。クリア(ゲージMAX)・失敗・次のショットのどれかを決める。
        /// </summary>
        public void OnChainFinished()
        {
            ComboChanged?.Invoke(0);

            int score = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0;
            bool towerDone = currentTower == null || currentTower.AliveCount <= 0;

            // ゲージの右端は「そのステージの最大得点」。☆を3つ取り切っても、ショットと階が残る限り続けられる
            // (満点を狙える)。撃ち切る/壊し切る/FINISHを押すと終わる。
            if (towerDone || ShotsLeft <= 0)
            {
                EndStage(score < CurrentSpec.TargetScore);
            }
            else
            {
                SetState(GameState.Playing);
            }
        }

        /// <summary>
        /// MAXに届いたあと、残りのショットを使わずにここで終える(FINISHボタン)。MAX未達では終えられない。
        /// </summary>
        public void FinishStage()
        {
            int score = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0;
            if (State == GameState.Playing && CurrentSpec != null && score >= CurrentSpec.TargetScore)
            {
                EndStage(false);
            }
        }

        private void EndStage(bool failed)
        {
            SetState(GameState.Result);
            pendingFailed = failed;
            Invoke(nameof(ShowResult), resultDelay);
        }

        private void ShowResult()
        {
            // 飛行中のポイントが残っていても、ここで全て表示に反映して☆判定を確定させる
            ScoreManager.Instance?.FlushDisplay();

            bool failed = pendingFailed;
            int score = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0;
            StageResult result = new StageResult
            {
                Spec = CurrentSpec,
                Failed = failed,
                Score = score,
                Stars = failed ? 0 : StarCount,
                StarFlags = (bool[])starFlags.Clone(),
                ShotsUsed = shotsUsed
            };

            if (!failed)
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
