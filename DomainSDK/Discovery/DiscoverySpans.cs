using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Discovery
{
    /// <summary>시간대 묶기. 도감의 사는 곳 막대가 씀</summary>
    public static class DiscoverySpans
    {
        /// <summary>겹치거나 이어진 시간대를 하나로. 시작 순서로 돌려줌</summary>
        public static List<(float Start, float End)> Merge(IReadOnlyList<(float Start, float End)> spans)
        {
            List<(float Start, float End)> sorted = new List<(float Start, float End)>(spans);
            sorted.Sort((left, right) => left.Start.CompareTo(right.Start));
            List<(float Start, float End)> merged = new List<(float Start, float End)>();
            foreach ((float start, float end) in sorted)
            {
                int last = merged.Count - 1;
                if (last >= 0 && start <= merged[last].End)
                {
                    merged[last] = (merged[last].Start, end > merged[last].End ? end : merged[last].End);
                }
                else
                {
                    merged.Add((start, end));
                }
            }
            return merged;
        }
    }
}
