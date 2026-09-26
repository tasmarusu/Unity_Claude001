#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// QA用の仕様適合チェック(エディタ/開発ビルドのみ)。
    /// 各ステージを実際のゲーム(GameManager/BuildingTower)で、ランダムな撃ち方と「最高得点の撃ち方」で遊び、
    /// スコア・☆・失敗判定・ステージ終了のタイミングが、ルール(ScoreRules)から計算した期待値と一致するかを確かめる。
    /// 使い方: Play中に StageAudit.Begin(1, 100, 6f, 3) → 終わったら StageAudit.Report を読む。
    /// </summary>
    public class StageAudit : MonoBehaviour
    {
        private const string StageIndexKey = "OneTapDemolition_StageIndex";
        private const string BestScoreKey = "OneTapDemolition_BestScore";
        private const string HistoryKey = "OneTapDemolition_HistoryUnlocked";

        public static string Report { get; private set; } = "";
        public static bool Running { get; private set; }

        private float speed = 6f;
        private StageResult lastResult;

        public static void Begin(int fromStage, int toStage, float timeScale, int randomPlans)
        {
            if (Running)
            {
                return;
            }
            StageAudit audit = new GameObject("StageAudit").AddComponent<StageAudit>();
            audit.speed = timeScale;
            audit.StartCoroutine(audit.Run(fromStage, toStage, randomPlans));
        }

        private void LateUpdate()
        {
            if (Running)
            {
                Time.timeScale = speed;
            }
        }

        private void OnEnded(StageResult r)
        {
            lastResult = r;
        }

        private class Expect
        {
            public int Score;
            public bool Bonus;
            public bool Ended;
        }

        /// <summary>ルール上の期待値: 撃った列(先頭から count 発)の合計スコア・ボーナス階を壊したか・そこで終わるか。</summary>
        private static Expect Model(StageSpec spec, float hb, List<int> taps, int count)
        {
            int limit = spec.Floors.Length;
            int shots = spec.Shots;
            Expect e = new Expect();
            for (int i = 0; i < count; i++)
            {
                int j = taps[i];
                e.Score += ScoreRules.SimulateTap(spec.Floors, j, limit, hb);
                e.Bonus |= ScoreRules.ChainIncludesBonus(spec.Floors, j, limit);
                limit = j;
                shots--;
            }
            e.Ended = limit <= 0 || shots <= 0;
            return e;
        }

        /// <summary>最高得点になる撃ち方(BestScoreと同じ探索で手順を復元)。</summary>
        private static int BestPlan(StageSpec spec, float hb, int limit, int shots, List<int> plan)
        {
            if (shots <= 0 || limit <= 0)
            {
                return 0;
            }
            int best = 0;
            int bestTap = -1;
            List<int> bestRest = null;
            for (int j = 0; j < limit; j++)
            {
                List<int> rest = new List<int>();
                int total = ScoreRules.SimulateTap(spec.Floors, j, limit, hb) + BestPlan(spec, hb, j, shots - 1, rest);
                if (total > best)
                {
                    best = total;
                    bestTap = j;
                    bestRest = rest;
                }
            }
            if (bestTap >= 0)
            {
                plan.Add(bestTap);
                plan.AddRange(bestRest);
            }
            return best;
        }

        private IEnumerator Run(int from, int to, int randomPlans)
        {
            Running = true;
            Report = "";
            GameManager gm = GameManager.Instance;
            int savedStage = PlayerPrefs.GetInt(StageIndexKey, 1);
            int savedBest = PlayerPrefs.GetInt(BestScoreKey, 0);
            int savedHistory = PlayerPrefs.GetInt(HistoryKey, 0);
            gm.StageEnded += OnEnded;

            int runs = 0;
            int problems = 0;
            int overMax = 0;
            int optimalNotThree = 0;
            int perfectBelowMax = 0;
            int failedPlays = 0;
            int clearedPlays = 0;
            int optimalPlays = 0;
            int optimalScoreMismatch = 0;
            StringBuilder issues = new StringBuilder();
            StringBuilder notes = new StringBuilder();

            for (int stage = from; stage <= to; stage++)
            {
                System.Random rng = new System.Random(stage * 31 + 5);
                for (int variant = 0; variant <= randomPlans; variant++)
                {
                    bool optimal = variant == 0;
                    lastResult = null;
                    gm.PrepareStage(stage, 0);
                    yield return null;
                    yield return null;
                    gm.BeginPlay();
                    StageSpec spec = gm.CurrentSpec;
                    float hb = ScoreManager.Instance != null ? ScoreManager.Instance.HistoryBonus : 0f;

                    List<int> taps = new List<int>();
                    if (optimal)
                    {
                        BestPlan(spec, hb, spec.Floors.Length, spec.Shots, taps);
                    }

                    string tag = "stage " + stage + (optimal ? " [optimal]" : " [random#" + variant + "]");
                    int limit = spec.Floors.Length;
                    int done = 0;
                    bool planFinished = false;
                    while (!planFinished)
                    {
                        yield return WaitPlayable(gm);
                        if (lastResult != null || gm.State != GameState.Playing)
                        {
                            break;
                        }

                        int j;
                        if (optimal)
                        {
                            if (done >= taps.Count)
                            {
                                break;
                            }
                            j = taps[done];
                        }
                        else
                        {
                            j = rng.Next(0, limit);
                            taps.Add(j);
                        }

                        BuildingTower tower = gm.CurrentTower;
                        Floor floor = tower.Floors[j];
                        if (!tower.RequestDemolish(floor, floor.transform.position + Vector3.back * 0.5f))
                        {
                            issues.AppendLine(tag + ": shot at " + j + " REJECTED");
                            problems++;
                            break;
                        }
                        limit = j;
                        done++;
                        yield return WaitWhile(gm, GameState.Resolving);
                        yield return WaitFrames(3);

                        Expect ex = Model(spec, hb, taps, done);
                        int actual = ScoreManager.Instance.CurrentScore;
                        if (actual != ex.Score)
                        {
                            issues.AppendLine(tag + ": after shot " + done + " (tap " + j + ") score engine=" + actual + " model=" + ex.Score);
                            problems++;
                        }
                        bool engineEnded = gm.State == GameState.Result;
                        if (engineEnded != ex.Ended)
                        {
                            issues.AppendLine(tag + ": after shot " + done + " engineEnded=" + engineEnded + " modelEnded=" + ex.Ended + " score=" + actual + " star=" + spec.StarScore);
                            problems++;
                        }
                        if (ex.Ended)
                        {
                            planFinished = true;
                        }
                    }

                    // 結果待ち
                    float wait = 0f;
                    while (lastResult == null && wait < 12f)
                    {
                        wait += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    runs++;
                    if (lastResult == null)
                    {
                        issues.AppendLine(tag + ": NO RESULT state=" + gm.State + " shots=" + done);
                        problems++;
                        continue;
                    }

                    Expect final = Model(spec, hb, taps, done);
                    bool expectFailed = final.Score < spec.TargetScore;
                    int expectStars = expectFailed ? 0 : 1 + (final.Bonus ? 1 : 0) + (final.Score >= spec.StarScore ? 1 : 0);
                    string summary = tag + " taps=[" + string.Join(",", taps) + "] score=" + lastResult.Score + "/" + spec.OptimalScore
                        + " clear=" + spec.TargetScore + " star=" + spec.StarScore + " stars=" + lastResult.Stars + " failed=" + lastResult.Failed;

                    if (lastResult.Failed != expectFailed || lastResult.Stars != expectStars || lastResult.Score != final.Score)
                    {
                        issues.AppendLine(summary + "  <-- expected failed=" + expectFailed + " stars=" + expectStars + " score=" + final.Score);
                        problems++;
                    }
                    if (lastResult.Score > spec.OptimalScore)
                    {
                        overMax++;
                        issues.AppendLine(summary + "  <-- SCORE ABOVE MAX");
                    }
                    if (ScoreManager.Instance.DisplayedScore != ScoreManager.Instance.CurrentScore)
                    {
                        issues.AppendLine(summary + "  <-- displayed score != real score at result");
                        problems++;
                    }

                    if (lastResult.Failed)
                    {
                        failedPlays++;
                    }
                    else
                    {
                        clearedPlays++;
                    }
                    if (optimal)
                    {
                        optimalPlays++;
                        if (lastResult.Score != spec.OptimalScore)
                        {
                            optimalScoreMismatch++;
                            issues.AppendLine(summary + "  <-- optimal play did not reach OptimalScore");
                        }
                        if (lastResult.Stars < 3)
                        {
                            optimalNotThree++;
                            if (notes.Length < 1500)
                            {
                                notes.AppendLine("  " + summary);
                            }
                        }
                    }
                    if (!lastResult.Failed && lastResult.Stars >= 3 && lastResult.Score < spec.OptimalScore)
                    {
                        perfectBelowMax++;
                    }
                }
            }

            gm.StageEnded -= OnEnded;
            PlayerPrefs.SetInt(StageIndexKey, savedStage);
            PlayerPrefs.SetInt(BestScoreKey, savedBest);
            PlayerPrefs.SetInt(HistoryKey, savedHistory);
            PlayerPrefs.Save();

            Report = "stages " + from + "-" + to + " runs=" + runs + " (cleared=" + clearedPlays + " failed=" + failedPlays + ")\n"
                + "rule mismatches=" + problems + "  score-above-max=" + overMax + "\n"
                + "optimal plays=" + optimalPlays + " optimal-score-mismatch=" + optimalScoreMismatch + " optimal-but-not-3-stars=" + optimalNotThree + "\n"
                + "PERFECT(3 stars) with score below max=" + perfectBelowMax + "\n"
                + "--- issues ---\n" + issues + "--- optimal plays that were not 3 stars (sample) ---\n" + notes;
            Time.timeScale = 1f;
            Running = false;
        }

        private IEnumerator WaitPlayable(GameManager gm)
        {
            float t = 0f;
            while (gm.State != GameState.Playing && lastResult == null && t < 30f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator WaitWhile(GameManager gm, GameState state)
        {
            float t = 0f;
            while (gm.State == state && t < 30f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator WaitFrames(int n)
        {
            for (int i = 0; i < n; i++)
            {
                yield return null;
            }
        }
    }
}
#endif
