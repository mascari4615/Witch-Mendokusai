using System;

namespace WitchMendokusai.DomainSDK.Care
{
    /// <summary>
    /// TASK-WM-171 — 소원이 요구하는 재료 한 항목 (아이템 id + 필요 개수).
    ///
    /// ⚠ ITEM-DECOUPLED — 의도적으로 <c>string</c> id (Item 타입 직접 참조 X). 본 substrate 는
    /// 소원 완성 판정의 *수학* 만 다루므로 아이템 시스템과 평행표면을 만들지 않는다. 이후 Phase 1+
    /// 에서 adapter 가 string id ↔ <see cref="WitchMendokusai.DomainSDK.Item"/> 를 잇는다.
    /// (패턴: <see cref="WitchMendokusai.DomainSDK.Life.NeedSpec"/> — readonly struct + 생성자 검증)
    /// </summary>
    public readonly struct WishMaterialReq
    {
        /// <summary>요구되는 아이템의 식별자(데이터 영역). 빈 문자열·null 불가.</summary>
        public readonly string ItemId;

        /// <summary>필요 개수. 1 이상.</summary>
        public readonly int Count;

        public WishMaterialReq(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentException("itemId 는 비어있을 수 없다", nameof(itemId));
            }

            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "재료 요구 개수는 1 이상이어야 한다");
            }

            ItemId = itemId;
            Count = count;
        }
    }
}
