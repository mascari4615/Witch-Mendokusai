using System;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 장비를 차는 자리 (TASK-WM-406).
    ///
    /// ★ 자리가 나뉘어 있어야 <b>짜는 맛</b>이 난다. 하나뿐이면 「가장 좋은 것 하나」로 끝나고,
    ///   그건 고르는 게 아니라 정답을 아는 것이다.
    ///   울티마 스쿼드도 부위마다 나올 수 있는 잠재가 달라서 부위별로 다른 것을 노린다.
    /// </summary>
    /* ★ **판정 층이 이름을 양보한다** (2026-08-16). 게임 층에도 `ItemSlot` 이 있다
       (`Domain/Item/UI/Toolkit/ItemSlot.cs` — 화면의 칸). 이름이 겹치면 **유니티에서만**
       CS0436/CS0246 으로 터지고 엔진 밖 빌드는 못 본다. 그래서 `WM asmdef boundary gate` 가
       **25판 연속 빨강**이었다 — 늘 빨간 검사는 아무도 안 보게 되고, 그러면 진짜 회귀도 묻힌다.
       규칙대로 판정 층이 물러난다. 열거형은 값으로 저장되므로 저장된 자료는 그대로다. */
    public enum IdleItemSlot
    {
        Head = 0,
        Body = 1,
        Hands = 2,
        Feet = 3,
    }

    /// <summary>
    /// 떨어진 장비 하나 (TASK-WM-406).
    ///
    /// ★ 잠재는 <b>떨어질 때 안 붙는다</b> — 감정해야 붙는다.
    ///   떨구기는 결정적이고(오프라인이 그 위에 선다) 도박은 사람이 누를 때만 굴린다는
    ///   이 게임의 규칙(<see cref="IdlePotentials"/>)을 장비에도 그대로 적용한 것이다.
    /// </summary>
    [Serializable]
    public struct IdleItem
    {
        /// <summary>등급 (1부터). 깊이가 상한을 정한다.</summary>
        public int Tier;

        /// <summary>차는 자리.</summary>
        public IdleItemSlot Slot;

        /// <summary>감정해서 나온 값(비율). 0 이면 아직 감정 안 했다.</summary>
        public double PotentialValue;

        /// <summary>그 값의 등급 (<see cref="PotentialGrade"/>).</summary>
        public int PotentialGradeValue;

        public IdleItem(int tier, IdleItemSlot slot)
        {
            Tier = tier;
            Slot = slot;
            PotentialValue = 0d;
            PotentialGradeValue = 0;
        }

        /// <summary>아직 감정 안 했다.</summary>
        public bool IsRaw => PotentialGradeValue == 0 && PotentialValue <= 0d;

        /// <summary>비어 있는 자리를 나타내는 값 — 등급 0 은 존재하지 않는다.</summary>
        public bool IsEmpty => Tier <= 0;

        public PotentialGrade Grade => (PotentialGrade)PotentialGradeValue;
    }
}
