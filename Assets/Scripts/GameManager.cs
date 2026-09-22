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
        [SerializeField] private BuildingTower towerPrefab;
        [SerializeField] private Transform towerSpawnPoint;
        [SerializeField] private float nextTowerDelay = 1.5f;

        private BuildingTower currentTower;

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
            currentTower = Instantiate(towerPrefab, spawnPosition, Quaternion.identity);
            currentTower.BuildTower();
        }

        /// <summary>
        /// BuildingTowerが全階崩壊したときに呼ばれる。少し間を置いて次のタワーを生成する。
        /// </summary>
        public void OnTowerCleared()
        {
            ScoreManager.Instance?.ResetScore();
            Invoke(nameof(SpawnNewTower), nextTowerDelay);
        }
    }
}
