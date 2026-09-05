using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>Unity나 에셋 경로를 모르는 영웅 정의 모음.</summary>
    public sealed class IdleHeroCatalog
    {
        private readonly IdleHeroKind[] kinds;

        public IdleHeroCatalog(IEnumerable<IdleHeroKind> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            kinds = new List<IdleHeroKind>(definitions).ToArray();
            if (kinds.Length == 0)
            {
                throw new ArgumentException("영웅 정의가 하나도 없다.", nameof(definitions));
            }

            for (int index = 0; index < kinds.Length; index++)
            {
                if (kinds[index].Id != index)
                {
                    throw new ArgumentException("영웅 ID는 0부터 빈칸 없이 배열 순서와 같아야 한다.", nameof(definitions));
                }
            }
        }

        public int Count => kinds.Length;

        public bool Knows(int id) => id >= 0 && id < kinds.Length;

        public IdleHeroKind KindOf(int id)
        {
            if (Knows(id) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "등록되지 않은 영웅 ID");
            }

            return kinds[id];
        }

        public void IdsOfGrade(IdleHeroGrade grade, List<int> into)
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
