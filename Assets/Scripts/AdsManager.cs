using System;
using GoogleMobileAds.Api;
using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// AdMobのバナー・インタースティシャル・リワード広告をまとめて管理するシングルトン。
    /// 現時点ではGoogle公式のテスト広告ユニットIDを使用している(本番配信前に差し替えが必要)。
    /// </summary>
    public class AdsManager : MonoBehaviour
    {
        public static AdsManager Instance { get; private set; }

#if UNITY_ANDROID
        private const string BannerAdUnitId = "ca-app-pub-3940256099942544/6300978111";
        private const string InterstitialAdUnitId = "ca-app-pub-3940256099942544/1033173712";
        private const string RewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917";
#elif UNITY_IOS
        private const string BannerAdUnitId = "ca-app-pub-3940256099942544/2934735716";
        private const string InterstitialAdUnitId = "ca-app-pub-3940256099942544/4411468910";
        private const string RewardedAdUnitId = "ca-app-pub-3940256099942544/1712485313";
#else
        private const string BannerAdUnitId = "unused";
        private const string InterstitialAdUnitId = "unused";
        private const string RewardedAdUnitId = "unused";
#endif

        [Header("Interstitial Pacing")]
        [Tooltip("この回数タワーをクリアするたびにインタースティシャルを挟む。")]
        [SerializeField] private int towerClearsPerInterstitial = 3;

        private BannerView bannerView;
        private InterstitialAd interstitialAd;
        private RewardedAd rewardedAd;
        private int towerClearCount;

        public bool IsRewardedReady => rewardedAd != null && rewardedAd.CanShowAd();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            MobileAds.Initialize(_ =>
            {
                ShowBanner();
                LoadInterstitial();
                LoadRewarded();
            });
        }

        // ---------------- Banner ----------------

        public void ShowBanner()
        {
            bannerView?.Destroy();

            bannerView = new BannerView(BannerAdUnitId, AdSize.Banner, AdPosition.Bottom);
            bannerView.LoadAd(new AdRequest());
        }

        public void HideBanner()
        {
            bannerView?.Hide();
        }

        // ---------------- Interstitial ----------------

        private void LoadInterstitial()
        {
            InterstitialAd.Load(InterstitialAdUnitId, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogWarning("[AdsManager] Interstitial failed to load: " + error);
                    return;
                }

                interstitialAd = ad;
                interstitialAd.OnAdFullScreenContentClosed += OnInterstitialClosed;
                interstitialAd.OnAdFullScreenContentFailed += _ => OnInterstitialClosed();
            });
        }

        private void OnInterstitialClosed()
        {
            interstitialAd = null;
            LoadInterstitial();
        }

        /// <summary>
        /// タワークリアのたびにGameManagerから呼ばれる。設定回数に達したらインタースティシャルを表示する。
        /// </summary>
        public void NotifyTowerCleared()
        {
            towerClearCount++;
            if (towerClearCount < towerClearsPerInterstitial)
            {
                return;
            }

            towerClearCount = 0;
            if (interstitialAd != null && interstitialAd.CanShowAd())
            {
                interstitialAd.Show();
            }
        }

        // ---------------- Rewarded ----------------

        private void LoadRewarded()
        {
            RewardedAd.Load(RewardedAdUnitId, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogWarning("[AdsManager] Rewarded ad failed to load: " + error);
                    return;
                }

                rewardedAd = ad;
                rewardedAd.OnAdFullScreenContentClosed += OnRewardedClosed;
                rewardedAd.OnAdFullScreenContentFailed += _ => OnRewardedClosed();
            });
        }

        private void OnRewardedClosed()
        {
            rewardedAd = null;
            LoadRewarded();
        }

        /// <summary>
        /// リワード広告を表示する。最後まで視聴されたときのみonRewardedが呼ばれる。
        /// </summary>
        public void ShowRewarded(Action onRewarded)
        {
            if (rewardedAd == null || !rewardedAd.CanShowAd())
            {
                return;
            }

            rewardedAd.Show(_ => onRewarded?.Invoke());
        }
    }
}
