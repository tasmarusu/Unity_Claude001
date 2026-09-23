using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace OneTapDemolition
{
    /// <summary>
    /// ScoreManagerのイベントを購読し、現在スコア・ベストスコアをUGUI Textへ反映する。
    /// 加算時はスコア表示を軽くパンチさせて「増えた感」を出す。
    /// </summary>
    public class ScoreUI : MonoBehaviour
    {
        [SerializeField] private Text scoreText;
        [SerializeField] private Text bestScoreText;
        [SerializeField] private float punchScale = 1.25f;
        [SerializeField] private float punchDuration = 0.18f;

        private Coroutine punchRoutine;

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

            if (addedAmount > 0 && scoreText != null)
            {
                if (punchRoutine != null)
                {
                    StopCoroutine(punchRoutine);
                }
                punchRoutine = StartCoroutine(PunchRoutine());
            }
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

        private IEnumerator PunchRoutine()
        {
            Transform t = scoreText.transform;
            float elapsed = 0f;
            float half = punchDuration * 0.5f;

            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / half);
                t.localScale = Vector3.one * Mathf.Lerp(1f, punchScale, p);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / half);
                t.localScale = Vector3.one * Mathf.Lerp(punchScale, 1f, p);
                yield return null;
            }

            t.localScale = Vector3.one;
            punchRoutine = null;
        }
    }
}
