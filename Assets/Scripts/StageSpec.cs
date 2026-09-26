using UnityEngine;

namespace OneTapDemolition
{
    public enum FloorKind
    {
        Normal,
        Gate,
        Bonus
    }

    /// <summary>
    /// 1階分の種類。Gateは連鎖でその階以降に掛かる倍率(×2/×3/÷2)、
    /// Bonusは得点が2倍で、壊すと☆が1つ出る特別な階(金色に脈動する)。
    /// </summary>
    public struct FloorSpec
    {
        public FloorKind Kind;
        public float GateValue;

        public static FloorSpec Normal => new FloorSpec { Kind = FloorKind.Normal, GateValue = 1f };
        public static FloorSpec Gate(float value) => new FloorSpec { Kind = FloorKind.Gate, GateValue = value };
        public static FloorSpec Bonus => new FloorSpec { Kind = FloorKind.Bonus, GateValue = 1f };
    }

    /// <summary>
    /// ☆の3条件。すべて画面上に印がある: 建物の☆(ボーナス階)、ゲージの目標(MAX)の☆、ゲージ右端の☆。
    /// </summary>
    public enum StarReason
    {
        BonusFloor,
        Max,
        Overshoot
    }

    /// <summary>
    /// 1ステージ分の定義。ステージ番号から決定的に生成されるので、リトライしても同じ配置になる。
    /// TargetScoreに達するとゲージMAX=クリア。StarScoreまで伸ばすと3つ目の☆が出る(ゲージ右端の☆マーク)。
    /// </summary>
    public class StageSpec
    {
        public int StageIndex;
        public FloorSpec[] Floors;
        public int Shots;
        public int OptimalScore;
        public int FirstOptimalTap;
        public int TargetScore;
        public int StarScore;
    }

    /// <summary>
    /// スコア計算の純粋ロジック。実際の加算・タップ前の予測表示・ステージ生成時の最適解探索で同じ式を共有する。
    /// 1階の得点 = 100 × コンボ倍率 × ゲート倍率(その階までに通過したゲートの積) × ボーナス階の倍率 × (1+歴史解放ボーナス)。
    /// </summary>
    public static class ScoreRules
    {
        public const int BasePoints = 100;
        public const float ComboBonusPerStep = 0.1f;
        public const float ComboCap = 3f;
        public const float BonusFloorMultiplier = 2f;

        public static float ComboMultiplier(int chainStep)
        {
            return Mathf.Min(1f + ComboBonusPerStep * Mathf.Max(0, chainStep - 1), ComboCap);
        }

        public static int FloorPoints(int chainStep, float gateProduct, float historyBonus, FloorKind kind)
        {
            float kindMultiplier = kind == FloorKind.Bonus ? BonusFloorMultiplier : 1f;
            return Mathf.RoundToInt(BasePoints * ComboMultiplier(chainStep) * gateProduct * kindMultiplier * (1f + historyBonus));
        }

        /// <summary>
        /// 生きている階が[0, limit)の連続区間のとき、tapIndexをタップした場合の合計スコアを返す。
        /// </summary>
        public static int SimulateTap(FloorSpec[] floors, int tapIndex, int limit, float historyBonus)
        {
            int score = 0;
            float gateProduct = 1f;
            int step = 0;
            for (int i = tapIndex; i < limit; i++)
            {
                step++;
                FloorSpec f = floors[i];
                if (f.Kind == FloorKind.Gate)
                {
                    gateProduct *= f.GateValue;
                }
                score += FloorPoints(step, gateProduct, historyBonus, f.Kind);
            }
            return score;
        }

        /// <summary>
        /// 残りショット数shotsで得られる最大スコア。firstTapは最初のタップ位置(最適解の一手目)。
        /// </summary>
        public static int BestScore(FloorSpec[] floors, int limit, int shots, float historyBonus, out int firstTap)
        {
            firstTap = -1;
            if (shots <= 0 || limit <= 0)
            {
                return 0;
            }

            int best = 0;
            for (int j = 0; j < limit; j++)
            {
                int ignored;
                int total = SimulateTap(floors, j, limit, historyBonus) + BestScore(floors, j, shots - 1, historyBonus, out ignored);
                if (total > best)
                {
                    best = total;
                    firstTap = j;
                }
            }
            return best;
        }

        /// <summary>
        /// 1発だけ撃つ場合の最高スコア。bonusOnly=trueなら「ボーナス階を巻き込む撃ち方」に限る。
        /// includesBonusは、その最高スコアの撃ち方がボーナス階を巻き込むか。
        /// </summary>
        public static int BestSingleTap(FloorSpec[] floors, float historyBonus, bool bonusOnly, out bool includesBonus)
        {
            int limit = floors.Length;
            int best = 0;
            includesBonus = false;
            for (int j = 0; j < limit; j++)
            {
                bool hasBonus = ChainIncludesBonus(floors, j, limit);
                if (bonusOnly && !hasBonus)
                {
                    continue;
                }

                int score = SimulateTap(floors, j, limit, historyBonus);
                if (score > best)
                {
                    best = score;
                    includesBonus = hasBonus;
                }
            }
            return best;
        }

        /// <summary>
        /// 「素直に高得点を狙った1発」で☆3つに届くか。ボーナス階を巻き込む1発の最高スコアが☆マーク以上であること。
        /// (1ショットのステージでは、それより高得点になる「ボーナス階を外す1発」があってもいけない。
        /// 外すと4000点超えでも☆2つ、という不公平が起きるため。)
        /// </summary>
        public static bool OneTapCanEarnAllStars(StageSpec spec, float historyBonus)
        {
            bool ignored;
            int withBonus = BestSingleTap(spec.Floors, historyBonus, true, out ignored);
            if (withBonus < spec.StarScore)
            {
                return false;
            }
            if (spec.Shots <= 1)
            {
                bool bestIncludes;
                BestSingleTap(spec.Floors, historyBonus, false, out bestIncludes);
                return bestIncludes;
            }
            return true;
        }

        /// <summary>
        /// このステージで☆3つ(ボーナス階を壊す・MAXに届く・ゲージ右端の☆まで届く)を全部取れる撃ち方が存在するか。
        /// MAXに届いてもショットと階が残っていれば続けられる実際のルールどおり、
        /// タップ位置が単調に下がる全ての撃ち方を総当たりで調べる。
        /// </summary>
        public static bool AllStarsFeasible(StageSpec spec, float historyBonus)
        {
            return FindThreeStarPlan(spec, historyBonus, null);
        }

        /// <summary>
        /// ☆3つを取れる撃ち方(タップする階の並び)を探す。見つかればtrueで、planに順番に入れる(planがnullなら存在確認のみ)。
        /// 実機同様の自動プレイ検証(StageAutoPlayer)が、この手順をそのまま実行して本当に☆3つになるか確かめる。
        /// </summary>
        public static bool FindThreeStarPlan(StageSpec spec, float historyBonus, System.Collections.Generic.List<int> plan)
        {
            return PlanSearch(spec, spec.Floors.Length, spec.Shots, 0, false, historyBonus, plan);
        }

        private static bool PlanSearch(StageSpec spec, int limit, int shotsLeft, int score, bool bonus, float historyBonus,
            System.Collections.Generic.List<int> plan)
        {
            for (int j = 0; j < limit; j++)
            {
                int total = score + SimulateTap(spec.Floors, j, limit, historyBonus);
                bool gotBonus = bonus || ChainIncludesBonus(spec.Floors, j, limit);

                if (plan != null)
                {
                    plan.Add(j);
                }

                if (gotBonus && total >= spec.StarScore)
                {
                    return true;
                }
                if (shotsLeft > 1 && j > 0 && PlanSearch(spec, j, shotsLeft - 1, total, gotBonus, historyBonus, plan))
                {
                    return true;
                }

                if (plan != null)
                {
                    plan.RemoveAt(plan.Count - 1);
                }
            }
            return false;
        }

        /// <summary>
        /// tapIndexをタップして巻き込まれる階(tapIndex以上limit未満)にボーナス階が含まれるか。
        /// </summary>
        public static bool ChainIncludesBonus(FloorSpec[] floors, int tapIndex, int limit)
        {
            for (int i = tapIndex; i < limit; i++)
            {
                if (floors[i].Kind == FloorKind.Bonus)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
