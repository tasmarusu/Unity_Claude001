using UnityEngine;
using UnityEngine.UI;

namespace OneTapDemolition
{
    /// <summary>
    /// タワークリア直後に、リワード広告視聴で「クリアスコア2倍」を狙えるボタンを一瞬表示する。
    /// </summary>
    public class RewardBonusUI : MonoBehaviour
    {
        public static RewardBonusUI Instance { get; private set; }

        [SerializeField] private GameObject buttonRoot;
        [SerializeField] private Button watchAdButton;
        [SerializeField] private float visibleDuration = 1.3f;

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
            if (buttonRoot != null)
            {
                buttonRoot.SetActive(false);
            }

            if (watchAdButton != null)
            {
                watchAdButton.onClick.AddListener(OnWatchAdClicked);
            }
        }

        /// <summary>
        /// GameManagerからタワークリア時に呼ばれる。リワード広告の準備ができていればボタンを表示する。
        /// </summary>
        public void ShowIfAvailable()
        {
            if (buttonRoot == null || AdsManager.Instance == null || !AdsManager.Instance.IsRewardedReady)
            {
                return;
            }

            buttonRoot.SetActive(true);
            CancelInvoke(nameof(Hide));
            Invoke(nameof(Hide), visibleDuration);
        }

        private void Hide()
        {
            if (buttonRoot != null)
            {
                buttonRoot.SetActive(false);
            }
        }

        private void OnWatchAdClicked()
        {
            Hide();
            AdsManager.Instance?.ShowRewarded(() => GameManager.Instance?.ApplyDoubleClearBonus());
        }
    }
}
