using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// ステージ番号から難易度カーブに沿ったタワー構成を決定的に生成する。
    /// 最適解を総当たりで求めて、クリア目標(MAX)と3つ目の☆のしきい値を決め、
    /// 「一番下を押せばいい」だけの単調な配置(悪ゲートが効かない配置)は避ける。
    /// </summary>
    public static class StageGenerator
    {
        private const int MinFloors = 6;
        private const int MaxFloors = 10;
        private const int MaxRerolls = 80;
        private static readonly float[] GoodGateValues = { 2f, 3f };
        private const float BadGateValue = 0.5f;
        private const float TargetRatio = 0.6f;
        private const float StarRatio = 0.75f;
        private const int ScoreRounding = 50;

        /// <summary>
        /// 条件を満たす構成を探して返す。優先順:
        /// (1)☆3が取れる・素直な1発で☆3に届く・単調でない (2)☆3が取れる・素直な1発で☆3に届く (3)☆3が取れる (4)最後の候補。
        /// 「☆3が取れないステージ」「高得点を狙って撃ったのに☆が足りないステージ」を作らないため、生成のたびに総当たりで確認する。
        /// </summary>
        public static StageSpec Generate(int stageIndex, float historyBonus)
        {
            StageSpec fairFallback = null;
            StageSpec feasibleFallback = null;
            StageSpec last = null;
            for (int attempt = 0; attempt < MaxRerolls; attempt++)
            {
                StageSpec spec = Build(stageIndex, attempt, historyBonus);
                last = spec;

                if (!ScoreRules.AllStarsFeasible(spec, historyBonus))
                {
                    continue;
                }

                bool fair = ScoreRules.OneTapCanEarnAllStars(spec, historyBonus);
                bool nonTrivial = stageIndex < 2 || spec.FirstOptimalTap > 0;
                if (fair && nonTrivial)
                {
                    return spec;
                }
                if (fair && fairFallback == null)
                {
                    fairFallback = spec;
                }
                if (feasibleFallback == null)
                {
                    feasibleFallback = spec;
                }
            }
            return fairFallback ?? feasibleFallback ?? last;
        }

        private static StageSpec Build(int stageIndex, int attempt, float historyBonus)
        {
            System.Random rng = new System.Random(stageIndex * 7919 + 13 + attempt * 104729);

            int floorCount = Mathf.Clamp(MinFloors + (stageIndex - 1) / 2, MinFloors, MaxFloors);
            int shots = stageIndex <= 2 ? 1 : (stageIndex <= 5 ? 2 : 3);
            int goodGates = Mathf.Clamp(1 + (stageIndex - 1) / 3, 1, 3);
            int badGates = stageIndex >= 2 ? Mathf.Clamp(1 + (stageIndex - 2) / 4, 1, 3) : 0;

            FloorSpec[] floors = new FloorSpec[floorCount];
            for (int i = 0; i < floorCount; i++)
            {
                floors[i] = FloorSpec.Normal;
            }

            for (int i = 0; i < badGates; i++)
            {
                int idx = PickFreeIndex(rng, floors, 1, floorCount - 1);
                if (idx >= 0)
                {
                    floors[idx] = FloorSpec.Gate(BadGateValue);
                }
            }
            for (int i = 0; i < goodGates; i++)
            {
                int idx = PickFreeIndex(rng, floors, 1, floorCount);
                if (idx >= 0)
                {
                    floors[idx] = FloorSpec.Gate(GoodGateValues[rng.Next(GoodGateValues.Length)]);
                }
            }

            // ☆のボーナス階は1つ(壊すと☆が出る。得点も2倍)
            int bonusIdx = PickFreeIndex(rng, floors, 0, floorCount);
            if (bonusIdx >= 0)
            {
                floors[bonusIdx] = FloorSpec.Bonus;
            }

            int firstTap;
            int optimal = ScoreRules.BestScore(floors, floorCount, shots, historyBonus, out firstTap);
            int target = Mathf.Max(ScoreRounding * 2, RoundTo(optimal * TargetRatio));
            int star = Mathf.Max(target + ScoreRounding, RoundTo(optimal * StarRatio));

            return new StageSpec
            {
                StageIndex = stageIndex,
                Floors = floors,
                Shots = shots,
                OptimalScore = optimal,
                FirstOptimalTap = firstTap,
                TargetScore = target,
                StarScore = star
            };
        }

        private static int RoundTo(float value)
        {
            return Mathf.RoundToInt(value / ScoreRounding) * ScoreRounding;
        }

        private static int PickFreeIndex(System.Random rng, FloorSpec[] floors, int minInclusive, int maxExclusive)
        {
            maxExclusive = Mathf.Min(maxExclusive, floors.Length);
            if (maxExclusive <= minInclusive)
            {
                return -1;
            }

            for (int tries = 0; tries < 20; tries++)
            {
                int idx = rng.Next(minInclusive, maxExclusive);
                if (floors[idx].Kind == FloorKind.Normal)
                {
                    return idx;
                }
            }
            return -1;
        }
    }
}
