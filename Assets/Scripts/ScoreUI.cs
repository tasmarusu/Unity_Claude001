using UnityEngine;
using UnityEngine.UI;

namespace OneTapDemolition
{
    /// <summary>
    /// ScoreManagerのイベントを購読し、現在スコア・ベストスコアをUGUI Textへ反映する。
    /// </summary>
    public class ScoreUI : MonoBehaviour
    {
        [SerializeField] private Text scoreText;
        [SerializeField] private Text bestScoreText;

        private void Start()
        {
            if (ScoreManager.Instance == null)
            {
                return;
            }

            ScoreManager.Instance.ScoreChanged += OnScoreChanged;
            ScoreManager.Instance.BestScoreUpdated += OnBestScoreUpdated;

            UpdateScoreText(ScoreManager.Instance.CurrentScore);
            UpdateBestScoreText(ScoreManager.Instance.BestScore);
        }

        private void OnDestroy()
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.ScoreChanged -= OnScoreChanged;
                ScoreManager.Instance.BestScoreUpdated -= OnBestScoreUpdated;
            }
        }

        private void OnScoreChanged(int addedAmount, int currentTotal)
        {
            UpdateScoreText(currentTotal);
        }

        private void OnBestScoreUpdated(int bestScore)
        {
            UpdateBestScoreText(bestScore);
        }

        private void UpdateScoreText(int currentTotal)
        {
            if (scoreText != null)
            {
                scoreText.text = currentTotal.ToString();
            }
        }

        private void UpdateBestScoreText(int bestScore)
        {
            if (bestScoreText != null)
            {
                bestScoreText.text = "BEST " + bestScore;
            }
        }
    }
}
