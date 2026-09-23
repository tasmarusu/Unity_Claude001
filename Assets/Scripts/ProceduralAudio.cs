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

        private static AudioClip BuildClip(string name, float[] data, int sampleRate)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
