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

    public enum StarReason
    {
        BonusFloor,
        Combo,
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
        public const int ComboStarStep = 5;

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
        /// このステージで☆3つ(ボーナス階・1発で連鎖5・☆マークまで)を全部取れる撃ち方が存在するか。
        /// 実際のルールどおり、MAXに達した一撃でステージが終わる(その後は撃てない)ことを前提に、
        /// タップ位置が単調に下がる全ての撃ち方を総当たりで調べる。ステージ生成で「☆3が取れないステージ」を作らないために使う。
        /// </summary>
        public static bool AllStarsFeasible(StageSpec spec, float historyBonus)
        {
            return AllStarsSearch(spec, spec.Floors.Length, spec.Shots, 0, false, false, historyBonus);
        }

        private static bool AllStarsSearch(StageSpec spec, int limit, int shotsLeft, int score, bool bonus, bool combo, float historyBonus)
        {
            for (int j = 0; j < limit; j++)
            {
                int total = score + SimulateTap(spec.Floors, j, limit, historyBonus);
                bool gotBonus = bonus || ChainIncludesBonus(spec.Floors, j, limit);
                bool gotCombo = combo || limit - j >= ComboStarStep;

                if (total >= spec.TargetScore)
                {
                    // MAXに達した一撃でステージ終了。このときに3条件が揃っていれば☆3
                    if (gotBonus && gotCombo && total >= spec.StarScore)
                    {
                        return true;
                    }
                    continue;
                }

                if (shotsLeft > 1 && j > 0 && AllStarsSearch(spec, j, shotsLeft - 1, total, gotBonus, gotCombo, historyBonus))
                {
                    return true;
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
