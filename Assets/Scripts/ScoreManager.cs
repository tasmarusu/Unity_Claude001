using System;
using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// スコア加算とベストスコアのPlayerPrefs保存を担当する。計算式そのものはScoreRulesに集約している。
    /// 「実スコア(CurrentScore)」は破壊した瞬間に増え、ゲーム進行の判定に使う。
    /// 「表示スコア(DisplayedScore)」は、破壊した階から飛んできたポイントがスコア表示に着いた瞬間に増える。
    /// ゲージ・スコア表示・☆判定はすべて表示スコアに連動するので、「壊す→ポイントが飛ぶ→着いて増える」が目で追える。
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        private const string BestScoreKey = "OneTapDemolition_BestScore";

        public static ScoreManager Instance { get; private set; }

        public event Action<int, int> ScoreChanged; // (addedAmount, currentTotal)
        public event Action<int, int> DisplayedScoreChanged; // (addedAmount, displayedTotal)
        public event Action<int> BestScoreUpdated;

        public int CurrentScore { get; private set; }
        public int DisplayedScore { get; private set; }
        public int BestScore { get; private set; }

        /// <summary>
        /// ステージを開始/リセットするたびに増える。飛行中のポイントが前のステージのものかを見分けるために使う。
        /// </summary>
        public int Generation { get; private set; }

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
        /// 連鎖の何段目か・通過済みゲートの倍率積・階の種類からスコアを加算する。加算量を返す。
        /// </summary>
        public int AddChainScore(int chainStep, float gateProduct, FloorKind kind)
        {
            int amount = ScoreRules.FloorPoints(chainStep, gateProduct, HistoryBonus, kind);
            CurrentScore += amount;
            ScoreChanged?.Invoke(amount, CurrentScore);
            return amount;
        }

        /// <summary>
        /// 破壊した階から飛んできたポイントが表示に着いたときに呼ぶ。表示スコアは実スコアを超えない。
        /// </summary>
        public void CommitDisplay(int generation, int amount)
        {
            if (generation != Generation)
            {
                return;
            }

            int next = Mathf.Min(CurrentScore, DisplayedScore + amount);
            int added = next - DisplayedScore;
            if (added <= 0)
            {
                return;
            }
            DisplayedScore = next;
            DisplayedScoreChanged?.Invoke(added, DisplayedScore);
        }

        /// <summary>
        /// 結果表示の直前に、まだ着いていないポイントを全部反映する。
        /// </summary>
        public void FlushDisplay()
        {
            int added = CurrentScore - DisplayedScore;
            if (added <= 0)
            {
                return;
            }
            DisplayedScore = CurrentScore;
            DisplayedScoreChanged?.Invoke(added, DisplayedScore);
        }

        public void ResetScore()
        {
            Generation++;
            CurrentScore = 0;
            DisplayedScore = 0;
            ScoreChanged?.Invoke(0, 0);
            DisplayedScoreChanged?.Invoke(0, 0);
        }

        /// <summary>
        /// ステージ成功時に今回のスコアをベスト判定にかける(失敗した連鎖のスコアは記録しない)。
        /// </summary>
        public void CommitBestScore()
        {
            TryUpdateBestScore(CurrentScore);
        }

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
