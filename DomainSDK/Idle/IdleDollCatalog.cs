using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>Unity나 에셋 경로를 모르는 인형 정의 모음.</summary>
    public sealed class IdleDollCatalog
    {
        private readonly IdleDollKind[] kinds;
        private readonly int[] starters;

        public IdleDollCatalog(IEnumerable<IdleDollKind> definitions)
            : this(definitions, null)
        {
        }

        /// <summary>
        /// <paramref name="starters"/> 는 새 판이 처음부터 가진 인형. null 이면 id 0 하나
        /// (사용자 결정 2026-09-21: 시작 셋은 욘, 링, 알리사. 시험 카탈로그는 옛대로 0 하나)
        /// </summary>
        public IdleDollCatalog(IEnumerable<IdleDollKind> definitions, IEnumerable<int> starters)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            kinds = new List<IdleDollKind>(definitions).ToArray();
            if (kinds.Length == 0)
            {
                throw new ArgumentException("인형 정의가 하나도 없다.", nameof(definitions));
            }

            for (int index = 0; index < kinds.Length; index++)
            {
                if (kinds[index].Id != index)
                {
                    throw new ArgumentException("인형 ID는 0부터 빈칸 없이 배열 순서와 같아야 한다.", nameof(definitions));
                }
            }

            this.starters = starters == null ? new[] { 0 } : new List<int>(starters).ToArray();
            if (this.starters.Length == 0)
            {
                throw new ArgumentException("시작 인형이 하나도 없다.", nameof(starters));
            }

            for (int index = 0; index < this.starters.Length; index++)
            {
                if (Knows(this.starters[index]) == false)
                {
                    throw new ArgumentException("시작 인형 ID 가 카탈로그에 없다: " + this.starters[index], nameof(starters));
                }
            }
        }

        /// <summary>새 판이 처음부터 가진 인형 id. 첫 번째가 대표 (공용 수치의 임자)</summary>
        public IReadOnlyList<int> Starters => starters;

        public int Count => kinds.Length;

        public bool Knows(int id) => id >= 0 && id < kinds.Length;

        public IdleDollKind KindOf(int id)
        {
            if (Knows(id) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "등록되지 않은 인형 ID");
            }

            return kinds[id];
        }

        public void IdsOfGrade(IdleDollGrade grade, List<int> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            into.Clear();
            for (int index = 0; index < kinds.Length; index++)
            {
                if (kinds[index].Grade == grade)
                {
                    into.Add(kinds[index].Id);
                }
            }
        }
    }
}
