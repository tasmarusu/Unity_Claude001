using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace OneTapDemolition
{
    /// <summary>
    /// 破壊時の「気持ちよさ」を演出する演出系ロジックをまとめたシングルトン。
    /// カメラシェイク・パーティクル・ヒットストップ・SE・スコアポップアップのフックを提供する。
    /// BuildingTower.csやFloor.csから呼び出される想定。
    /// 効果音・崩落パーティクルは、Inspectorで未設定の場合はその場で自動生成する
    /// (アセット未設定で無音・無演出になるのを避けるため)。
    /// </summary>
    public class JuiceManager : MonoBehaviour
    {
        public static JuiceManager Instance { get; private set; }

        [Header("Camera Shake")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float baseShakeMagnitude = 0.12f;
        [SerializeField] private float shakeMagnitudePerChain = 0.035f;
        [SerializeField] private float maxShakeMagnitude = 0.7f;
        [SerializeField] private float shakeDuration = 0.18f;

        [Header("Hit Stop")]
        [SerializeField] private float hitStopDuration = 0.05f;
        [SerializeField] private float hitStopDurationPerChain = 0.012f;
        [SerializeField] private float maxHitStopDuration = 0.22f;
        [SerializeField] private float hitStopTimeScale = 0.03f;

        [Header("Debris Particle")]
        [Tooltip("未設定ならプロシージャルな粉塵バーストを自動生成する。")]
        [SerializeField] private ParticleSystem debrisParticlePrefab;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [Tooltip("未設定ならProceduralAudioで自動合成する。")]
        [SerializeField] private AudioClip destroyClip;
        [SerializeField] private AudioClip impactClip;
        [SerializeField] private float basePitch = 1f;
        [SerializeField] private float pitchStepPerChain = 0.04f;
        [SerializeField] private float maxPitch = 2f;

        [Header("Score Popup Hook")]
        [Tooltip("連鎖でスコアが加算されるたびに呼ばれる。UI側の数値ポップ演出などをここに接続する。")]
        public UnityEvent<Vector3, int> OnScorePopupRequested;

        /// <summary>
        /// タップの衝撃が起きるたびに呼ばれる。totalChainCountはこの一撃で崩れる階数。
        /// コンボ演出(「3 CHAIN!」等)や画面フラッシュをUI側で接続する。
        /// </summary>
        public event Action<int> OnChainImpact;

        private Vector3 cameraOriginalLocalPosition;
        private Coroutine shakeRoutine;
        private Coroutine hitStopRoutine;
        private static Mesh cachedDebrisMesh;
        private static Material cachedDebrisMaterial;

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

            if (impactClip == null)
            {
                impactClip = ProceduralAudio.CreateImpactThud();
            }
            if (destroyClip == null)
            {
                destroyClip = ProceduralAudio.CreateDestroyCrunch();
            }
        }

        /// <summary>
        /// タップした瞬間の「衝撃」演出。連鎖数が多いほどシェイク・ヒットストップが強くなる。
        /// BuildingTower.RequestDemolishから、崩落する全階数が分かった時点で呼ばれる。
        /// </summary>
        public void TriggerImpact(int totalChainCount)
        {
            Shake(totalChainCount);
            DoHitStop(totalChainCount);
            PlayImpactSound();
            OnChainImpact?.Invoke(totalChainCount);
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
            if (debrisParticlePrefab != null)
            {
                ParticleSystem instance = Instantiate(debrisParticlePrefab, worldPosition, Quaternion.identity);
                float lifetime = instance.main.duration + instance.main.startLifetime.constantMax;
                Destroy(instance.gameObject, lifetime);
                return;
            }

            SpawnProceduralDebris(worldPosition);
        }

        /// <summary>
        /// パーティクルプレハブ未設定時に使う、コード生成の粉塵・破片バースト。
        /// 外部アセット不要で、シーン上書きの影響も受けない。
        /// </summary>
        private void SpawnProceduralDebris(Vector3 worldPosition)
        {
            GameObject go = new GameObject("DebrisBurst");
            go.transform.position = worldPosition;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.8f, 0.77f, 0.72f, 1f), new Color(0.55f, 0.52f, 0.48f, 1f));
            main.gravityModifier = 1.3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 10, 16) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            var rotationOverLifetime = ps.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-220f, 220f);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = GetDebrisMesh();
            renderer.material = GetDebrisMaterial();
            renderer.alignment = ParticleSystemRenderSpace.World;

            ps.Play();

            float totalLifetime = main.duration + main.startLifetime.constantMax + 0.3f;
            Destroy(go, totalLifetime);
        }

        private static Mesh GetDebrisMesh()
        {
            if (cachedDebrisMesh == null)
            {
                GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cachedDebrisMesh = temp.GetComponent<MeshFilter>().sharedMesh;
                Destroy(temp);
            }
            return cachedDebrisMesh;
        }

        private static Material GetDebrisMaterial()
        {
            if (cachedDebrisMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }
                if (shader == null)
                {
                    shader = Shader.Find("Sprites/Default");
                }
                cachedDebrisMaterial = new Material(shader);
                if (cachedDebrisMaterial.HasProperty("_BaseColor"))
                {
                    cachedDebrisMaterial.SetColor("_BaseColor", new Color(0.6f, 0.57f, 0.53f, 1f));
                }
                else if (cachedDebrisMaterial.HasProperty("_Color"))
                {
                    cachedDebrisMaterial.SetColor("_Color", new Color(0.6f, 0.57f, 0.53f, 1f));
                }
            }
            return cachedDebrisMaterial;
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
                Vector2 offset = UnityEngine.Random.insideUnitCircle * magnitude * damper;
                cameraTransform.localPosition = cameraOriginalLocalPosition + new Vector3(offset.x, offset.y, 0f);
                yield return null;
            }

            cameraTransform.localPosition = cameraOriginalLocalPosition;
            shakeRoutine = null;
        }

        private void DoHitStop(int chainCount)
        {
            if (hitStopRoutine != null)
            {
                StopCoroutine(hitStopRoutine);
                Time.timeScale = 1f;
            }

            float duration = Mathf.Min(
                maxHitStopDuration,
                hitStopDuration + hitStopDurationPerChain * Mathf.Max(0, chainCount - 1));

            hitStopRoutine = StartCoroutine(HitStopRoutine(duration));
        }

        private IEnumerator HitStopRoutine(float duration)
        {
            Time.timeScale = hitStopTimeScale;
            yield return new WaitForSecondsRealtime(duration);
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
