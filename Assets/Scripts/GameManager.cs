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

        [Header("Reward Boost")]
        [Tooltip("リワード広告視聴後、次のタワーの崩落吹っ飛び力に掛かる倍率。")]
        [SerializeField] private float boostForceMultiplier = 2.5f;

        private BuildingTower currentTower;
        private bool nextTowerBoosted;

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

            float multiplier = nextTowerBoosted ? boostForceMultiplier : 1f;
            currentTower.BuildTower(multiplier);
            CameraFraming.Instance?.FrameTower(currentTower.TotalHeight);

            if (nextTowerBoosted)
            {
                nextTowerBoosted = false;
                ScoreFeedbackUI.Instance?.ShowPowerUpBanner();
            }
        }

        /// <summary>
        /// BuildingTowerが全階崩壊したときに呼ばれる。少し間を置いて次のタワーを生成する。
        /// </summary>
        public void OnTowerCleared()
        {
            ScoreManager.Instance?.ResetScore();

            AdsManager.Instance?.NotifyTowerCleared();
            RewardBonusUI.Instance?.ShowIfAvailable();

            Invoke(nameof(SpawnNewTower), nextTowerDelay);
        }

        /// <summary>
        /// リワード広告を最後まで見た後に呼ばれる。次に生成されるタワーの崩落を強化する。
        /// </summary>
        public void ApplyNextTowerBoost()
        {
            nextTowerBoosted = true;
        }
    }
}
