using System;
using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// スコア加算とベストスコアのPlayerPrefs保存を担当する。
    /// 合計倍率 = コンボ倍率 ×(1+ワンタップボーナス)×(1+スピードボーナス)×(1+歴史解放ボーナス) の乗算式。
    /// docs/building-history-unlocks.md の設計を、時間制限/手数制限のない本作のワンタップ仕様に合わせて適用したもの。
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        private const string BestScoreKey = "OneTapDemolition_BestScore";

        public static ScoreManager Instance { get; private set; }

        [Header("Scoring")]
        [SerializeField] private int scorePerFloor = 10;

        [Header("Combo Multiplier")]
        [Tooltip("連鎖1段ごとに加算される倍率。上限comboMultiplierCapまで。")]
        [SerializeField] private float comboBonusPerStep = 0.1f;
        [SerializeField] private float comboMultiplierCap = 3.0f;

        [Header("One-Tap Bonus")]
        [Tooltip("そのタワーへの通算タップ回数が1回(最初の一撃)なら+100%。タップ回数が増えるほど線形に減衰する。")]
        [SerializeField] private float oneTapBonusMax = 1.0f;
        [SerializeField] private int oneTapBonusDecayTaps = 5;

        [Header("Speed Bonus")]
        [Tooltip("タワー出現からの経過時間が短いほど+50%に近づき、speedBonusWindow秒でゼロまで減衰する。")]
        [SerializeField] private float speedBonusMax = 0.5f;
        [SerializeField] private float speedBonusWindow = 6f;

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
        /// 連鎖段数・そのタワーへの通算タップ回数・出現からの経過時間から倍率を計算してスコアを加算する。加算量を返す。
        /// </summary>
        public int AddChainScore(int chainStep, int tapCountThisTower, float elapsedSinceTowerSpawn)
        {
            float comboMultiplier = Mathf.Min(1f + comboBonusPerStep * Mathf.Max(0, chainStep - 1), comboMultiplierCap);

            float tapDecayT = oneTapBonusDecayTaps > 1
                ? Mathf.Clamp01((tapCountThisTower - 1) / (float)(oneTapBonusDecayTaps - 1))
                : (tapCountThisTower > 1 ? 1f : 0f);
            float oneTapBonus = Mathf.Lerp(oneTapBonusMax, 0f, tapDecayT);

            float speedT = speedBonusWindow > 0f ? Mathf.Clamp01(elapsedSinceTowerSpawn / speedBonusWindow) : 1f;
            float speedBonus = Mathf.Lerp(speedBonusMax, 0f, speedT);

            float historyBonus = BuildingHistoryManager.Instance != null ? BuildingHistoryManager.Instance.HistoryBonusMultiplier : 0f;

            float totalMultiplier = comboMultiplier * (1f + oneTapBonus) * (1f + speedBonus) * (1f + historyBonus);
            int amount = Mathf.RoundToInt(scorePerFloor * totalMultiplier);

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
