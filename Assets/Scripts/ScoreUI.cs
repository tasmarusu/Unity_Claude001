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
        private float shownScore;
        private int targetScore;

        public RectTransform ScoreTextRect => scoreText != null ? scoreText.rectTransform : null;

        private void Start()
        {
            // 上端の大きなスコア数字は、ゲージの「現在値/最大値」と重複するので出さない(コンポーネントは残す)
            if (scoreText != null)
            {
                scoreText.enabled = false;
            }
            if (bestScoreText != null)
            {
                bestScoreText.enabled = false;
            }
            if (scoreText != null)
            {
                // スコア数字の下地(暗い枠)も一緒に隠す
                Transform backdrop = scoreText.transform.parent != null ? scoreText.transform.parent.Find("ScoreBackdrop") : null;
                if (backdrop != null)
                {
                    backdrop.gameObject.SetActive(false);
                }
            }

            if (ScoreManager.Instance == null)
            {
                return;
            }

            ScoreManager.Instance.DisplayedScoreChanged += OnScoreChanged;
            ScoreManager.Instance.BestScoreUpdated += OnBestScoreUpdated;

            targetScore = ScoreManager.Instance.DisplayedScore;
            shownScore = targetScore;
            UpdateScoreText(targetScore);
            UpdateBestScoreText(ScoreManager.Instance.BestScore);
        }

        private void OnDestroy()
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.DisplayedScoreChanged -= OnScoreChanged;
                ScoreManager.Instance.BestScoreUpdated -= OnBestScoreUpdated;
            }
        }

        private void Update()
        {
            if (Mathf.Approximately(shownScore, targetScore))
            {
                return;
            }

            // 表示スコアが着いた分だけ数字が転がるように増える(一瞬で書き換わらない)
            shownScore = Mathf.Lerp(shownScore, targetScore, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            if (Mathf.Abs(targetScore - shownScore) < 1f)
            {
                shownScore = targetScore;
            }
            UpdateScoreText(Mathf.RoundToInt(shownScore));
        }

        private void OnScoreChanged(int addedAmount, int currentTotal)
        {
            targetScore = currentTotal;
            if (addedAmount == 0)
            {
                shownScore = currentTotal;
                UpdateScoreText(currentTotal);
            }

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
