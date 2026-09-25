using System;
using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// スコア加算とベストスコアのPlayerPrefs保存を担当する。計算式そのものはScoreRulesに集約している
    /// (タップ前の予測表示・ステージ生成の最適解探索と同じ式を使うため)。
    /// 1階分の得点 = 10 × コンボ倍率 × ゲート倍率(その階までに通過したゲートの積) × (1+歴史解放ボーナス)。
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        private const string BestScoreKey = "OneTapDemolition_BestScore";

        public static ScoreManager Instance { get; private set; }

        public event Action<int, int> ScoreChanged; // (addedAmount, currentTotal)
        public event Action<int> BestScoreUpdated;

        public int CurrentScore { get; private set; }
        public int BestScore { get; private set; }

        public float HistoryBonus =>
            BuildingHistoryManager.Instance != null ? BuildingHistoryManager.Instance.HistoryBonusMultiplier : 0f;

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
        /// 連鎖の何段目か・通過済みゲートの倍率積からスコアを加算する。加算量を返す。
        /// </summary>
        public int AddChainScore(int chainStep, float gateProduct)
        {
            int amount = ScoreRules.FloorPoints(chainStep, gateProduct, HistoryBonus);
            CurrentScore += amount;
            ScoreChanged?.Invoke(amount, CurrentScore);

            return amount;
        }

        public void ResetScore()
        {
            CurrentScore = 0;
            ScoreChanged?.Invoke(0, CurrentScore);
        }

        /// <summary>
        /// ステージ成功時に今回のスコアをベスト判定にかける(失敗した連鎖のスコアは記録しない)。
        /// </summary>
        public void CommitBestScore()
        {
            TryUpdateBestScore(CurrentScore);
        }

        /// <summary>
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
