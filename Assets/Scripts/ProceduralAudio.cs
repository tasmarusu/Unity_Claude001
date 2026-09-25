using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// 外部音源ファイルなしで、崩落用の効果音(衝撃・破壊音)をその場で合成するユーティリティ。
    /// AIによる音声生成にはAPIキーが必要で今は未設定のため、代わりにノイズ+エンベロープで
    /// 「ドン」「ガシャッ」に近い質感を波形合成する。JuiceManagerが音源未設定のときに使う。
    /// </summary>
    public static class ProceduralAudio
    {
        /// <summary>
        /// タップした瞬間の低い「ドン」という衝撃音。
        /// </summary>
        public static AudioClip CreateImpactThud(int seed = 12345, float duration = 0.18f, int sampleRate = 44100)
        {
            int samples = Mathf.CeilToInt(duration * sampleRate);
            float[] data = new float[samples];
            System.Random rng = new System.Random(seed);

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Exp(-t * 26f);
                float thump = Mathf.Sin(2f * Mathf.PI * 85f * t) * 0.65f;
                float sub = Mathf.Sin(2f * Mathf.PI * 45f * t) * 0.25f;
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.35f;
                data[i] = Mathf.Clamp((thump + sub + noise) * envelope, -1f, 1f);
            }

            return BuildClip("ProceduralImpactThud", data, sampleRate);
        }

        /// <summary>
        /// 階が崩れる瞬間の「ガシャッ」というノイズ主体の破壊音。
        /// </summary>
        public static AudioClip CreateDestroyCrunch(int seed = 6789, float duration = 0.22f, int sampleRate = 44100)
        {
            int samples = Mathf.CeilToInt(duration * sampleRate);
            float[] data = new float[samples];
            System.Random rng = new System.Random(seed);
            float prevNoise = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Exp(-t * 16f);
                float rawNoise = (float)rng.NextDouble() * 2f - 1f;
                float noise = Mathf.Lerp(prevNoise, rawNoise, 0.55f);
                prevNoise = noise;
                float crack = Mathf.Sin(2f * Mathf.PI * (260f - 180f * t) * t) * 0.3f;
                data[i] = Mathf.Clamp((noise * 0.7f + crack) * envelope, -1f, 1f);
            }

            return BuildClip("ProceduralDestroyCrunch", data, sampleRate);
        }

        /// <summary>
        /// ゲート階を通過した「ピロン」という上昇チャイム。倍率が高いほど高い音で呼ぶ。
        /// </summary>
        public static AudioClip CreateGateChime(float baseFrequency, int sampleRate = 44100)
        {
            float duration = 0.26f;
            int samples = Mathf.CeilToInt(duration * sampleRate);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Exp(-t * 9f) * Mathf.Clamp01(t * 200f);
                float f = t < 0.08f ? baseFrequency : baseFrequency * 1.5f;
                float tone = Mathf.Sin(2f * Mathf.PI * f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.2f;
                data[i] = Mathf.Clamp(tone * envelope * 0.7f, -1f, 1f);
            }
            return BuildClip("ProceduralGateChime", data, sampleRate);
        }

        /// <summary>
        /// 保護階を壊した/失敗したときの、下降する濁ったブザー。
        /// </summary>
        public static AudioClip CreateFail(int sampleRate = 44100)
        {
            float duration = 0.55f;
            int samples = Mathf.CeilToInt(duration * sampleRate);
            float[] data = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float freq = Mathf.Lerp(220f, 70f, t / duration);
                phase += 2f * Mathf.PI * freq / sampleRate;
                float saw = Mathf.Repeat(phase / (2f * Mathf.PI), 1f) * 2f - 1f;
                float envelope = Mathf.Exp(-t * 3.5f);
                data[i] = Mathf.Clamp(saw * envelope * 0.55f, -1f, 1f);
            }
            return BuildClip("ProceduralFail", data, sampleRate);
        }

        /// <summary>
        /// 星が1つ点くたびの「キラン」。indexが上がるほど高くなる。
        /// </summary>
        public static AudioClip CreateStarNote(int index, int sampleRate = 44100)
        {
            float[] notes = { 659.25f, 783.99f, 987.77f };
            float f0 = notes[Mathf.Clamp(index, 0, notes.Length - 1)];
            float duration = 0.4f;
            int samples = Mathf.CeilToInt(duration * sampleRate);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Exp(-t * 6f) * Mathf.Clamp01(t * 300f);
                float tone = Mathf.Sin(2f * Mathf.PI * f0 * t) * 0.55f
                    + Mathf.Sin(2f * Mathf.PI * f0 * 2f * t) * 0.25f
                    + Mathf.Sin(2f * Mathf.PI * f0 * 3f * t) * 0.1f;
                data[i] = Mathf.Clamp(tone * envelope, -1f, 1f);
            }
            return BuildClip("ProceduralStarNote" + index, data, sampleRate);
        }

        /// <summary>
        /// ステージクリア(特に星3つ)の短いアルペジオのファンファーレ。
        /// </summary>
        public static AudioClip CreateFanfare(int sampleRate = 44100)
        {
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            float noteLen = 0.13f;
            float duration = noteLen * notes.Length + 0.45f;
            int samples = Mathf.CeilToInt(duration * sampleRate);
            float[] data = new float[samples];
            for (int n = 0; n < notes.Length; n++)
            {
                int start = Mathf.RoundToInt(n * noteLen * sampleRate);
                for (int i = start; i < samples; i++)
                {
                    float t = (i - start) / (float)sampleRate;
                    if (t > 0.6f)
                    {
                        break;
                    }
                    float envelope = Mathf.Exp(-t * 5f) * Mathf.Clamp01(t * 250f);
                    float tone = Mathf.Sin(2f * Mathf.PI * notes[n] * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * notes[n] * 2f * t) * 0.18f;
                    data[i] = Mathf.Clamp(data[i] + tone * envelope * 0.6f, -1f, 1f);
                }
            }
            return BuildClip("ProceduralFanfare", data, sampleRate);
        }

        /// <summary>
        /// ポイントがスコアに着いた瞬間の「ピッ」。stepが上がるごとに音階が上がり、連続で着くほど高くなる。
        /// </summary>
        public static AudioClip CreateScoreTick(int step, int sampleRate = 44100)
        {
            float[] scale = { 523.25f, 587.33f, 659.25f, 783.99f, 880f, 1046.5f, 1174.7f, 1318.5f };
            float f = scale[Mathf.Clamp(step, 0, scale.Length - 1)];
            float duration = 0.1f;
            int samples = Mathf.CeilToInt(duration * sampleRate);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Exp(-t * 32f) * Mathf.Clamp01(t * 400f);
                data[i] = Mathf.Sin(2f * Mathf.PI * f * t) * envelope * 0.5f;
            }
            return BuildClip("ProceduralScoreTick" + step, data, sampleRate);
        }

        /// <summary>
        /// ボタンを押した瞬間の短い「カチッ」。
        /// </summary>
        public static AudioClip CreateUiClick(int sampleRate = 44100)
        {
            float duration = 0.07f;
            int samples = Mathf.CeilToInt(duration * sampleRate);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Exp(-t * 60f);
                data[i] = Mathf.Sin(2f * Mathf.PI * 1200f * t) * envelope * 0.5f;
            }
            return BuildClip("ProceduralUiClick", data, sampleRate);
        }

        private static AudioClip BuildClip(string name, float[] data, int sampleRate)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
