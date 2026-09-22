using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// タワー生成と、クリア後の次タワー生成ループを制御する。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Tower Spawning")]
        [SerializeField] private BuildingTower[] towerPrefabs;
        [SerializeField] private Transform towerSpawnPoint;
        [SerializeField] private float nextTowerDelay = 1.5f;

        private BuildingTower currentTower;
        private int lastClearedScore;

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
            SpawnNewTower();
        }

        private void SpawnNewTower()
        {
            if (currentTower != null)
            {
                Destroy(currentTower.gameObject);
            }

            Vector3 spawnPosition = towerSpawnPoint != null ? towerSpawnPoint.position : Vector3.zero;
            BuildingTower prefab = towerPrefabs[Random.Range(0, towerPrefabs.Length)];
            currentTower = Instantiate(prefab, spawnPosition, Quaternion.identity);
            currentTower.BuildTower();
        }

        /// <summary>
        /// BuildingTowerが全階崩壊したときに呼ばれる。少し間を置いて次のタワーを生成する。
        /// </summary>
        public void OnTowerCleared()
        {
            lastClearedScore = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0;
            ScoreManager.Instance?.ResetScore();

            AdsManager.Instance?.NotifyTowerCleared();
            RewardBonusUI.Instance?.ShowIfAvailable();

            Invoke(nameof(SpawnNewTower), nextTowerDelay);
        }

        /// <summary>
        /// リワード広告を最後まで見た後に呼ばれる。直前にクリアしたスコアを2倍にしてベストスコア判定にかける。
        /// </summary>
        public void ApplyDoubleClearBonus()
        {
            ScoreManager.Instance?.TryUpdateBestScore(lastClearedScore * 2);
        }
    }
}
