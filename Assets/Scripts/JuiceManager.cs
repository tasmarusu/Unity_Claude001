using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace OneTapDemolition
{
    /// <summary>
    /// 破壊時の「気持ちよさ」を演出する演出系ロジックをまとめたシングルトン。
    /// カメラシェイク・パーティクル・ヒットストップ・SE・スコアポップアップのフックを提供する。
    /// BuildingTower.csやFloor.csから呼び出される想定。
    /// </summary>
    public class JuiceManager : MonoBehaviour
    {
        public static JuiceManager Instance { get; private set; }

        [Header("Camera Shake")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float baseShakeMagnitude = 0.1f;
        [SerializeField] private float shakeMagnitudePerChain = 0.03f;
        [SerializeField] private float maxShakeMagnitude = 0.6f;
        [SerializeField] private float shakeDuration = 0.15f;

        [Header("Hit Stop")]
        [SerializeField] private float hitStopDuration = 0.04f;
        [SerializeField] private float hitStopTimeScale = 0.05f;

        [Header("Debris Particle (Placeholder)")]
        [SerializeField] private ParticleSystem debrisParticlePrefab;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip destroyClip;
        [SerializeField] private AudioClip impactClip;
        [SerializeField] private float basePitch = 1f;
        [SerializeField] private float pitchStepPerChain = 0.04f;
        [SerializeField] private float maxPitch = 2f;

        [Header("Score Popup Hook")]
        [Tooltip("連鎖でスコアが加算されるたびに呼ばれる。UI側の数値ポップ演出などをここに接続する。")]
        public UnityEvent<Vector3, int> OnScorePopupRequested;

        private Vector3 cameraOriginalLocalPosition;
        private Coroutine shakeRoutine;
        private Coroutine hitStopRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
            if (cameraTransform != null)
            {
                cameraOriginalLocalPosition = cameraTransform.localPosition;
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
        }

        /// <summary>
        /// タップした瞬間の「衝撃」演出。連鎖数が多いほどシェイクとヒットストップが強くなる。
        /// BuildingTower.RequestDemolishから、崩落する全階数が分かった時点で呼ばれる。
        /// </summary>
        public void TriggerImpact(int totalChainCount)
        {
            Shake(totalChainCount);
            DoHitStop();
            PlayImpactSound();
        }

        /// <summary>
        /// 1階分が壊れた瞬間の演出(パーティクル・SE)。Floor.Demolishから呼ばれる。
        /// </summary>
        public void PlayFloorDestroyEffect(Vector3 worldPosition, int chainStep)
        {
            SpawnDebris(worldPosition);
            PlayDestroySound(chainStep);
        }

        /// <summary>
        /// スコア加算のたびに呼ばれる演出フック。数値ポップなどのUI演出に接続する土台。
        /// </summary>
        public void ShowScorePopup(Vector3 worldPosition, int amount)
        {
            OnScorePopupRequested?.Invoke(worldPosition, amount);
        }

        private void SpawnDebris(Vector3 worldPosition)
        {
            if (debrisParticlePrefab == null)
            {
                return;
            }

            ParticleSystem instance = Instantiate(debrisParticlePrefab, worldPosition, Quaternion.identity);
            float lifetime = instance.main.duration + instance.main.startLifetime.constantMax;
            Destroy(instance.gameObject, lifetime);
        }

        private void Shake(int chainCount)
        {
            if (cameraTransform == null)
            {
                return;
            }

            if (shakeRoutine != null)
            {
                StopCoroutine(shakeRoutine);
                cameraTransform.localPosition = cameraOriginalLocalPosition;
            }

            float magnitude = Mathf.Min(
                maxShakeMagnitude,
                baseShakeMagnitude + shakeMagnitudePerChain * Mathf.Max(0, chainCount - 1));

            shakeRoutine = StartCoroutine(ShakeRoutine(magnitude));
        }

        private IEnumerator ShakeRoutine(float magnitude)
        {
            float elapsed = 0f;
            while (elapsed < shakeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float damper = 1f - Mathf.Clamp01(elapsed / shakeDuration);
                Vector2 offset = Random.insideUnitCircle * magnitude * damper;
                cameraTransform.localPosition = cameraOriginalLocalPosition + new Vector3(offset.x, offset.y, 0f);
                yield return null;
            }

            cameraTransform.localPosition = cameraOriginalLocalPosition;
            shakeRoutine = null;
        }

        private void DoHitStop()
        {
            if (hitStopRoutine != null)
            {
                StopCoroutine(hitStopRoutine);
                Time.timeScale = 1f;
            }

            hitStopRoutine = StartCoroutine(HitStopRoutine());
        }

        private IEnumerator HitStopRoutine()
        {
            Time.timeScale = hitStopTimeScale;
            yield return new WaitForSecondsRealtime(hitStopDuration);
            Time.timeScale = 1f;
            hitStopRoutine = null;
        }

        private void PlayImpactSound()
        {
            if (audioSource == null || impactClip == null)
            {
                return;
            }

            audioSource.pitch = basePitch;
            audioSource.PlayOneShot(impactClip);
        }

        private void PlayDestroySound(int chainStep)
        {
            if (audioSource == null || destroyClip == null)
            {
                return;
            }

            audioSource.pitch = Mathf.Min(maxPitch, basePitch + pitchStepPerChain * Mathf.Max(0, chainStep - 1));
            audioSource.PlayOneShot(destroyClip);
        }
    }
}
