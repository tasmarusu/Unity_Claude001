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
        private static Material cachedDustMaterial;
        private static Texture2D cachedDustTexture;
        private AudioSource sfxSource;
        private AudioClip failClip;
        private AudioClip fanfareClip;
        private AudioClip clickClip;
        private readonly System.Collections.Generic.Dictionary<int, AudioClip> gateClips = new System.Collections.Generic.Dictionary<int, AudioClip>();
        private readonly AudioClip[] starClips = new AudioClip[3];

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

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;

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
        /// debrisTintはその階(建物バリアント)の色。連鎖が進むほど量・サイズが増していく。
        /// </summary>
        public void PlayFloorDestroyEffect(Vector3 worldPosition, int chainStep, Color debrisTint)
        {
            SpawnDebris(worldPosition, chainStep, debrisTint);
            PlayDestroySound(chainStep);
        }

        private void SpawnDebris(Vector3 worldPosition, int chainStep, Color debrisTint)
        {
            if (debrisParticlePrefab != null)
            {
                ParticleSystem instance = Instantiate(debrisParticlePrefab, worldPosition, Quaternion.identity);
                float lifetime = instance.main.duration + instance.main.startLifetime.constantMax;
                Destroy(instance.gameObject, lifetime);
                return;
            }

            SpawnProceduralDebris(worldPosition, chainStep, debrisTint);
            SpawnDustPuff(worldPosition, chainStep);
        }

        /// <summary>
        /// パーティクルプレハブ未設定時に使う、コード生成の破片チャンクバースト。
        /// 建物の色(debrisTint)で染め、連鎖段数(chainStep)が進むほど量・サイズ・勢いが増す。
        /// 外部アセット不要で、シーン上書きの影響も受けない。
        /// </summary>
        private void SpawnProceduralDebris(Vector3 worldPosition, int chainStep, Color debrisTint)
        {
            GameObject go = new GameObject("DebrisBurst");
            go.transform.position = worldPosition;

            float escalation = Mathf.Clamp01((chainStep - 1) / 9f);
            int burstCount = Mathf.RoundToInt(Mathf.Lerp(12, 26, escalation));

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, Mathf.Lerp(5f, 7.5f, escalation));
            main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Lerp(0.07f, 0.09f, escalation), Mathf.Lerp(0.2f, 0.3f, escalation));
            main.startColor = new ParticleSystem.MinMaxGradient(
                Color.Lerp(debrisTint, Color.white, 0.15f), Color.Lerp(debrisTint, Color.black, 0.25f));
            main.gravityModifier = 1.3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)(burstCount - 4), (short)burstCount) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            var rotationOverLifetime = ps.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.x = new ParticleSystem.MinMaxCurve(-260f, 260f);
            rotationOverLifetime.y = new ParticleSystem.MinMaxCurve(-260f, 260f);
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-260f, 260f);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
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

        /// <summary>
        /// 破片チャンクに重ねる、ふわっと広がる粉塵の煙。質量感を補い、「壊した」実感を強める。
        /// </summary>
        private void SpawnDustPuff(Vector3 worldPosition, int chainStep)
        {
            GameObject go = new GameObject("DustPuff");
            go.transform.position = worldPosition;

            float escalation = Mathf.Clamp01((chainStep - 1) / 9f);
            int puffCount = Mathf.RoundToInt(Mathf.Lerp(4, 9, escalation));

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.9f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, Mathf.Lerp(0.9f, 1.4f, escalation));
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.72f, 0.7f, 0.68f, 0.5f), new Color(0.6f, 0.58f, 0.55f, 0.35f));
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)puffCount) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0.3f, 0.4f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = GetDustMaterial();
            renderer.alignment = ParticleSystemRenderSpace.View;

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

        /// <summary>
        /// パーティクルの頂点カラー(main.startColor/colorOverLifetime)を確実に反映するため、
        /// Particles系のUnlitシェーダーを使う(Standard/URP Litはパーティクルカラーを無視することがある)。
        /// </summary>
        private static Material GetDebrisMaterial()
        {
            if (cachedDebrisMaterial == null)
            {
                cachedDebrisMaterial = new Material(FindParticleShader());
            }
            return cachedDebrisMaterial;
        }

        private static Material GetDustMaterial()
        {
            if (cachedDustMaterial == null)
            {
                cachedDustMaterial = new Material(FindParticleShader());
                if (cachedDustMaterial.HasProperty("_MainTex"))
                {
                    cachedDustMaterial.SetTexture("_MainTex", GetDustTexture());
                }
                else if (cachedDustMaterial.HasProperty("_BaseMap"))
                {
                    cachedDustMaterial.SetTexture("_BaseMap", GetDustTexture());
                }
            }
            return cachedDustMaterial;
        }

        private static Shader FindParticleShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            return shader;
        }

        /// <summary>
        /// 粉塵パフ用の、中心が白く外側に向けてフェードする円形テクスチャをコードで生成する。
        /// </summary>
        private static Texture2D GetDustTexture()
        {
            if (cachedDustTexture != null)
            {
                return cachedDustTexture;
            }

            const int size = 32;
            cachedDustTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxDist = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float alpha = Mathf.Clamp01(1f - dist / maxDist);
                    alpha = alpha * alpha;
                    cachedDustTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            cachedDustTexture.Apply();
            return cachedDustTexture;
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

        /// <summary>
        /// ゲート通過音。通過後の倍率積が高いほど高い音になる(倍率が下がる場合は低い音)。
        /// </summary>
        public void PlayGate(float gateProduct)
        {
            int semitone = Mathf.Clamp(Mathf.RoundToInt(Mathf.Log(Mathf.Max(0.25f, gateProduct), 2f) * 5f), -6, 14);
            AudioClip clip;
            if (!gateClips.TryGetValue(semitone, out clip))
            {
                clip = ProceduralAudio.CreateGateChime(660f * Mathf.Pow(2f, semitone / 12f));
                gateClips[semitone] = clip;
            }
            sfxSource.PlayOneShot(clip, 0.8f);
        }

        public void PlayFail()
        {
            if (failClip == null)
            {
                failClip = ProceduralAudio.CreateFail();
            }
            sfxSource.PlayOneShot(failClip, 0.9f);
        }

        public void PlayStarNote(int index)
        {
            index = Mathf.Clamp(index, 0, starClips.Length - 1);
            if (starClips[index] == null)
            {
                starClips[index] = ProceduralAudio.CreateStarNote(index);
            }
            sfxSource.PlayOneShot(starClips[index], 0.9f);
        }

        public void PlayFanfare()
        {
            if (fanfareClip == null)
            {
                fanfareClip = ProceduralAudio.CreateFanfare();
            }
            sfxSource.PlayOneShot(fanfareClip, 0.9f);
        }

        public void PlayUiClick()
        {
            if (clickClip == null)
            {
                clickClip = ProceduralAudio.CreateUiClick();
            }
            sfxSource.PlayOneShot(clickClip, 0.7f);
        }

        /// <summary>
        /// UI側(ScoreFeedbackUI)からスコア加算演出を呼び出すためのフック。
        /// </summary>
        public void ShowScorePopup(Vector3 worldPosition, int amount)
        {
            OnScorePopupRequested?.Invoke(worldPosition, amount);
        }
    }
}
