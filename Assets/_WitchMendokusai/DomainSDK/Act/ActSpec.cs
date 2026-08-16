using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Act
{
    /// <summary>
    /// 한 행동의 선언 (TASK-WM-408) — 「이 행동은 시간 N분과 이러이러한 것을 먹고, 저러저러한 것을 남긴다」.
    /// 순수 POCO (DomainSDK, 결정적, EditMode 직접 테스트).
    ///
    /// ★ 왜 「행동」이 규칙의 주인인가 (WM 은 여러 게임이 동시에 사는 세계):
    ///   ① 규칙을 <b>코어</b>가 쥐면 취침·기력 같은 한 장르의 법이 전 게임에 강제된다.
    ///   ② 규칙을 <b>지역</b>이 쥐면 경계를 넘는 순간 몸의 법칙이 바뀐다(더 이상하다).
    ///   ③ 그래서 규칙은 <b>행동</b>이 쥔다 — 밭 갈기가 지치는 것은 밭 갈기의 성질이지
    ///      농장이라는 땅의 성질이 아니다. 장르색은 여기 <b>수치</b>에서 나온다.
    ///
    /// 아무것도 선언하지 않은 행동(<see cref="Free"/>)은 세계에 아무 일도 일으키지 않는다 —
    /// 「강제 0」이 말이 아니라 구조로 성립하는 자리(오락실 캐비닛 게임이 여기 산다).
    /// </summary>
    public sealed class ActSpec
    {
        private static readonly ActNeedDelta[] NO_NEED_DELTAS = Array.Empty<ActNeedDelta>();
        private static readonly ActResourceDelta[] NO_RESOURCE_DELTAS = Array.Empty<ActResourceDelta>();

        public ActSpec(int minutes, IReadOnlyList<ActNeedDelta> needDeltas = null, IReadOnlyList<ActResourceDelta> resourceDeltas = null)
        {
            Minutes = minutes > 0 ? minutes : 0;
            NeedDeltas = needDeltas ?? NO_NEED_DELTAS;
            ResourceDeltas = resourceDeltas ?? NO_RESOURCE_DELTAS;
        }

        /// <summary>이 행동이 먹는 세계 시간(분). 0 = 시간을 안 쓰는 행동(시계 밖에서 노는 것들).</summary>
        public int Minutes { get; }

        /// <summary>욕구 변화 목록 — 음수 소모 / 양수 회복.</summary>
        public IReadOnlyList<ActNeedDelta> NeedDeltas { get; }

        /// <summary>자원 변화 목록 — 음수 소모 / 양수 생성.</summary>
        public IReadOnlyList<ActResourceDelta> ResourceDeltas { get; }

        /// <summary>세계에 아무 대가도 없는 행동. 시간도 안 흐르고 아무것도 안 변한다.</summary>
        public static ActSpec Free { get; } = new ActSpec(0);

        /// <summary>선언한 게 하나라도 있나 — 없으면 적용해도 세계는 그대로다.</summary>
        public bool IsFree => Minutes == 0 && NeedDeltas.Count == 0 && ResourceDeltas.Count == 0;
    }
}
