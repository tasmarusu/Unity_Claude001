using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// ステージ番号から難易度カーブに沿ったタワー構成を決定的に生成する。
    /// 最適解を総当たりで求めて、クリア目標(MAX)と3つ目の☆のしきい値を決め、
    /// 「一番下を押せばいい」だけの単調な配置は避ける。
    /// </summary>
    public static class StageGenerator
    {
        private const int MinFloors = 6;
        private const int MaxFloors = 12;
        private const int MaxRerolls = 30;
        private static readonly float[] GoodGateValues = { 2f, 3f };
        private const float BadGateValue = 0.5f;
        private const float TargetRatio = 0.6f;
        private const float StarRatio = 0.9f;
        private const int ScoreRounding = 50;

        public static StageSpec Generate(int stageIndex, float historyBonus)
        {
            StageSpec best = null;
            for (int attempt = 0; attempt < MaxRerolls; attempt++)
            {
                StageSpec spec = Build(stageIndex, attempt, historyBonus);
                best = spec;
                bool needsNonTrivial = stageIndex >= 2;
                if (!needsNonTrivial || spec.FirstOptimalTap > LowestSafeTap(spec.Floors))
                {
                    break;
                }
            }
            return best;
        }

        /// <summary>
        /// 撃てる一番下の階(保護階があればその1つ上)。最適解の一手目がここなら単調なステージになる。
        /// </summary>
        private static int LowestSafeTap(FloorSpec[] floors)
        {
            for (int i = floors.Length - 1; i >= 0; i--)
            {
                if (floors[i].Kind == FloorKind.Protected)
                {
                    return i + 1;
                }
            }
            return 0;
        }

        private static StageSpec Build(int stageIndex, int attempt, float historyBonus)
        {
            System.Random rng = new System.Random(stageIndex * 7919 + 13 + attempt * 104729);

            int floorCount = Mathf.Clamp(MinFloors + (stageIndex - 1) / 2, MinFloors, MaxFloors);
            int shots = stageIndex <= 2 ? 1 : (stageIndex <= 5 ? 2 : 3);
            int goodGates = Mathf.Clamp(1 + (stageIndex - 1) / 3, 1, 3);
            int badGates = stageIndex >= 2 ? Mathf.Clamp(1 + (stageIndex - 2) / 4, 1, 3) : 0;
            int protectedCount = stageIndex >= 3 ? Mathf.Clamp(1 + (stageIndex - 3) / 5, 1, 2) : 0;

            FloorSpec[] floors = new FloorSpec[floorCount];
            for (int i = 0; i < floorCount; i++)
            {
                floors[i] = FloorSpec.Normal;
            }

            // 保護階より下は撃てなくなる。1発でコンボ☆(連鎖5)が狙える階数を必ず上に残す
            int protectedMax = Mathf.Min(floorCount / 3 + 1, floorCount - ScoreRules.ComboStarStep);
            for (int i = 0; i < protectedCount; i++)
            {
                int idx = PickFreeIndex(rng, floors, 1, Mathf.Max(2, protectedMax));
                if (idx >= 0)
                {
                    floors[idx] = FloorSpec.Protect;
                }
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

            // ☆のボーナス階は撃てる範囲(保護階より上)の中に1つ
            int bonusIdx = PickFreeIndex(rng, floors, LowestSafeTap(floors), floorCount);
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
