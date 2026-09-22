using System;
using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// スコア加算とベストスコアのPlayerPrefs保存を担当する。
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        private const string BestScoreKey = "OneTapDemolition_BestScore";

        public static ScoreManager Instance { get; private set; }

        [Header("Scoring")]
        [SerializeField] private int scorePerFloor = 10;
        [SerializeField] private int chainBonusPerStep = 5;

        public event Action<int, int> ScoreChanged; // (addedAmount, currentTotal)
        public event Action<int> BestScoreUpdated;

        public int CurrentScore { get; private set; }
        public int BestScore { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            BestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
        }

        /// <summary>
        /// 連鎖の何番目かに応じてボーナス込みのスコアを加算する。加算量を返す。
        /// </summary>
        public int AddChainScore(int chainStep)
        {
            int amount = scorePerFloor + chainBonusPerStep * Mathf.Max(0, chainStep - 1);
            CurrentScore += amount;
            ScoreChanged?.Invoke(amount, CurrentScore);

            TryUpdateBestScore(CurrentScore);

            return amount;
        }

        public void ResetScore()
        {
            CurrentScore = 0;
            ScoreChanged?.Invoke(0, CurrentScore);
        }

        /// <summary>
        /// リワード広告視聴のボーナスなど、任意の値を「今回の記録」としてベストスコア判定にかける。
        /// candidateがベストスコアを上回った場合のみ更新される。
        /// </summary>
        public void TryUpdateBestScore(int candidate)
        {
            if (candidate <= BestScore)
            {
                return;
            }

            BestScore = candidate;
            PlayerPrefs.SetInt(BestScoreKey, BestScore);
            BestScoreUpdated?.Invoke(BestScore);
        }
    }
}
