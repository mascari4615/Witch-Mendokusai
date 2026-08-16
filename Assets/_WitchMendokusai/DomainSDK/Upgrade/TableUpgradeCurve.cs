using System;

namespace WitchMendokusai.DomainSDK.Upgrade
{
    /// <summary>
    /// 표로 값을 내는 곡선 — 레벨마다 값과 효과를 손으로 박는다. WM 이 원래 쓰던 형태다
    /// (`UpgradeData.PricePerLevel[] / ValuePerLevel[]`).
    ///
    /// ★ 왜 남기나 — 손으로 박는 건 <b>레벨이 적을 때 가장 정확한 도구</b>다. 열 단계짜리 능력치는
    ///   수식보다 표가 낫다(원하는 값을 그대로 적는다). 방치형만 수식으로 가면 되고, 둘이 같은 문을 쓰면 된다.
    ///
    /// ★ 표 밖의 레벨은 마지막 칸을 되풀이한다 — 배열 밖 접근으로 터지는 것보다 낫고,
    ///   `MaxLevel` 이 표 길이라 정상 흐름에서는 거기까지 가지도 않는다.
    /// </summary>
    [Serializable]
    public sealed class TableUpgradeCurve : IUpgradeCurve
    {
        private readonly double[] costPerLevel;
        private readonly double[] valuePerLevel;

        /// <summary>표 길이가 곧 상한이다 — 값 표와 효과 표 중 짧은 쪽.</summary>
        public int MaxLevel { get; }

        public TableUpgradeCurve(double[] costPerLevel, double[] valuePerLevel)
        {
            this.costPerLevel = costPerLevel ?? Array.Empty<double>();
            this.valuePerLevel = valuePerLevel ?? Array.Empty<double>();
            MaxLevel = Math.Min(this.costPerLevel.Length, this.valuePerLevel.Length);
        }

        /// <summary>기존 SO 의 int 배열에서 그대로 옮겨 담는다 — 마이그레이션용.</summary>
        public static TableUpgradeCurve FromLegacy(int[] pricePerLevel, float[] valuePerLevel)
        {
            double[] costs = ToDoubles(pricePerLevel);
            double[] values = ToDoubles(valuePerLevel);
            return new TableUpgradeCurve(costs, values);
        }

        public double CostToRaiseFrom(int level)
        {
            return Sample(costPerLevel, level);
        }

        public double TotalValueAt(int level)
        {
            double total = 0d;
            for (int index = 0; index < level; index++)
            {
                total += Sample(valuePerLevel, index);
            }

            return total;
        }

        private static double Sample(double[] table, int level)
        {
            if (table.Length == 0)
            {
                return 0d;
            }

            if (level < 0)
            {
                return table[0];
            }

            return level < table.Length ? table[level] : table[table.Length - 1];
        }

        private static double[] ToDoubles(int[] source)
        {
            if (source == null)
            {
                return Array.Empty<double>();
            }

            double[] result = new double[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                result[index] = source[index];
            }

            return result;
        }

        private static double[] ToDoubles(float[] source)
        {
            if (source == null)
            {
                return Array.Empty<double>();
            }

            double[] result = new double[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                result[index] = source[index];
            }

            return result;
        }
    }
}
