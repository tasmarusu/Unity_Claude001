#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// QA用の自動プレイヤー(エディタ/開発ビルドのみ)。ステージ生成時に「☆3つを取れる撃ち方」を探し、
    /// その手順を実際のゲーム(GameManager/BuildingTower/スコア飛翔/☆判定)でそのまま実行して、
    /// 本当に☆3つになるかを確かめる。ソルバーの理屈だけでなく、実エンジンの挙動(タイミング・表示スコア・ステージ終了条件)まで含めた検証。
    /// 使い方: Play中に StageAutoPlayer.Begin(1, 30) → 終わったら StageAutoPlayer.Report を読む。
    /// oneTap=true なら「ボーナス階を巻き込む1発の最高スコア」を1回だけ撃つ(素直に高得点を狙った撃ち方)で☆3つになるかを確かめる。
    /// 進行度(PlayerPrefs)は実行後に元へ戻す。
    /// </summary>
    public class StageAutoPlayer : MonoBehaviour
    {
        private const string StageIndexKey = "OneTapDemolition_StageIndex";
        private const string BestScoreKey = "OneTapDemolition_BestScore";
        private const string HistoryKey = "OneTapDemolition_HistoryUnlocked";

        public static string Report { get; private set; } = "";
        public static bool Running { get; private set; }

        private float speed = 4f;
        private bool oneTap;
        private StageResult lastResult;

        public static void Begin(int fromStage, int toStage, float timeScale = 4f, bool oneTap = false)
        {
            if (Running)
            {
                return;
            }

            GameObject go = new GameObject("StageAutoPlayer");
            StageAutoPlayer player = go.AddComponent<StageAutoPlayer>();
            player.speed = timeScale;
            player.oneTap = oneTap;
            player.StartCoroutine(player.Run(fromStage, toStage));
        }

        private void LateUpdate()
        {
            // ヒットストップ等で戻される時間スケールを、検証中は一定に保つ
            if (Running)
            {
                Time.timeScale = speed;
            }
        }

        private void OnEnded(StageResult result)
        {
            lastResult = result;
        }

        private IEnumerator Run(int fromStage, int toStage)
        {
            Running = true;
            Report = "";
            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                Report = "GameManager not found";
                Running = false;
                yield break;
            }

            int savedStage = PlayerPrefs.GetInt(StageIndexKey, 1);
            int savedBest = PlayerPrefs.GetInt(BestScoreKey, 0);
            int savedHistory = PlayerPrefs.GetInt(HistoryKey, 0);
            gm.StageEnded += OnEnded;

            int threeStars = 0;
            int notThree = 0;
            int noPlan = 0;
            StringBuilder lines = new StringBuilder();

            for (int stage = fromStage; stage <= toStage; stage++)
            {
                lastResult = null;
                gm.PrepareStage(stage, 0);
                yield return null;
                yield return null;
                gm.BeginPlay();

                StageSpec spec = gm.CurrentSpec;
                float historyBonus = ScoreManager.Instance != null ? ScoreManager.Instance.HistoryBonus : 0f;
                List<int> plan = new List<int>();
                if (oneTap)
                {
                    int bestTap = -1;
                    int bestScore = -1;
                    for (int j = 0; j < spec.Floors.Length; j++)
                    {
                        if (!ScoreRules.ChainIncludesBonus(spec.Floors, j, spec.Floors.Length))
                        {
                            continue;
                        }
                        int sc = ScoreRules.SimulateTap(spec.Floors, j, spec.Floors.Length, historyBonus);
                        if (sc > bestScore)
                        {
                            bestScore = sc;
                            bestTap = j;
                        }
                    }
                    plan.Add(bestTap);
                }
                else if (!ScoreRules.FindThreeStarPlan(spec, historyBonus, plan))
                {
                    noPlan++;
                    lines.AppendLine("stage " + stage + ": NO PLAN (solver says 3 stars impossible)");
                    continue;
                }

                foreach (int index in plan)
                {
                    yield return WaitUntilPlayableOrResult(gm);
                    if (lastResult != null)
                    {
                        break;
                    }

                    BuildingTower tower = gm.CurrentTower;
                    Floor floor = tower.Floors[index];
                    if (!tower.RequestDemolish(floor, floor.transform.position + Vector3.back * 0.5f))
                    {
                        lines.AppendLine("stage " + stage + ": shot at floor " + index + " was REJECTED");
                    }
                    yield return WaitWhile(gm, GameState.Resolving);
                }

                // 計画を撃ち終えたのに終わっていなければ(残りショットがある等)、FINISHで終える
                float wait = 0f;
                while (lastResult == null && wait < 15f)
                {
                    wait += Time.unscaledDeltaTime;
                    if (gm.State == GameState.Playing)
                    {
                        gm.FinishStage();
                    }
                    yield return null;
                }

                if (lastResult == null)
                {
                    notThree++;
                    lines.AppendLine("stage " + stage + ": NO RESULT (state=" + gm.State + ")");
                    continue;
                }

                bool ok = !lastResult.Failed && lastResult.Stars == 3;
                if (ok)
                {
                    threeStars++;
                }
                else
                {
                    notThree++;
                }
                lines.AppendLine("stage " + stage + ": " + (ok ? "OK " : "NG ") + "stars=" + lastResult.Stars
                    + " failed=" + lastResult.Failed + " score=" + lastResult.Score + " MAX=" + spec.TargetScore + " starMark=" + spec.StarScore
                    + " floors=" + spec.Floors.Length + " shots=" + spec.Shots + " plan=[" + string.Join(",", plan) + "]");
            }

            gm.StageEnded -= OnEnded;
            PlayerPrefs.SetInt(StageIndexKey, savedStage);
            PlayerPrefs.SetInt(BestScoreKey, savedBest);
            PlayerPrefs.SetInt(HistoryKey, savedHistory);
            BuildingHistoryManager.Instance?.RestoreUnlockedCount(savedHistory);
            PlayerPrefs.Save();

            Report = "stages " + fromStage + "-" + toStage + ": 3-star OK=" + threeStars + " NG=" + notThree + " noPlan=" + noPlan + "\n" + lines;
            Time.timeScale = 1f;
            Running = false;
        }

        private IEnumerator WaitUntilPlayableOrResult(GameManager gm)
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
    }
}
#endif
