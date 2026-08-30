using System;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 껐다 켜도 이어지게 하는 저장 꼴 (TASK-WM-406).
    ///
    /// ★ <b>덜 깎은 피해까지</b> 담는다 — 이걸 빼면 저장할 때마다 진행 중이던 타격이 버려져
    ///   자주 저장할수록 손해가 난다. 코어가 「스텝을 쪼개도 결과가 같다」를 보장하는 근거이기도 하다.
    ///
    /// ★ <b>마지막으로 본 시각</b>을 담는다 — 이게 오프라인 보상의 유일한 재료다.
    ///   기기 시계라 사람이 앞으로 돌릴 수 있다. 그건 <see cref="IdleSession"/> 이 판정한다(음수 = 0).
    /// </summary>
    [Serializable]
    public struct IdleSaveData
    {
        public double Resource;
        public long Kills;
        public long HitsOnTarget;
        public double AttackProgress;
        public int DamageLevel;
        public int AttackSpeedLevel;

        /// <summary>단계 — 옛 저장에는 없어 0 으로 온다. <see cref="IdleState.Load"/> 가 메운다.</summary>
        public int Stage;
        public int KillsInStage;
        public int BestStage;

        /// <summary>여기 머무를지 — 사람이 정한 것이라 저장을 건너야 한다.</summary>
        public bool HoldingStage;

        /// <summary>리셋 점수 — 리셋을 건너 살아남는 유일한 값.</summary>
        public long PrestigePoints;
        public int Ascensions;

        /// <summary>기지 — 생산자 종류별 보유 수.</summary>
        public long[] Owned;

        /// <summary>가방과 착용 — 모험이 가져온 것.</summary>
        public IdleItem[] BagItems;
        public IdleItem[] WornItems;
        public long DropSequence;

        /// <summary>뽑아서 가진 영웅들 (TASK-WM-406).</summary>
        public IdleHeroOwned[] Heroes;

        /// <summary>내보낸 셋 — 영웅 id, 빈 자리는 -1.</summary>
        public int[] Party;

        /// <summary>마지막 최고등급 이후 뽑은 횟수 — 천장.</summary>
        public int PullsSincePity;

        /// <summary>여태 뽑은 총 횟수 — 값이 이걸 따라 오른다.</summary>
        public long PullsDone;

        /// <summary>쓸 수 있는 환생석 — 배수(PrestigePoints)와 갈라 둔다.</summary>
        public long Stones;

        /// <summary>등급별 떨어진 개수. 옛 저장에는 없어 null 로 온다.</summary>
        public long[] DroppedByTier;
        public double[] DropProgressByTier;

        /// <summary>주사위 상태 — 이게 없으면 껐다 켜서 다시 굴리기가 공짜가 된다.</summary>
        public long RandomState;
        public double BestPotentialValue;
        public int BestPotentialGrade;

        /// <summary>마지막으로 본 시각 (Unix 초, UTC).</summary>
        public long LastSeenUnixSeconds;

        /// <summary>카드 코스트 — 옛 저장에는 없어 0 으로 온다 (V2).</summary>
        public double Cost;

        /// <summary>긴급 보급이 남은 시간(초).</summary>
        public double SupplySecondsLeft;

        public int[] CardDeck;

        /// <summary>자리별 남은 체력·부활 게이지 — 옛 저장에는 없어 null 로 온다 (V2 부대층).</summary>
        public double[] SeatHealth;
        public double[] SeatReviveSeconds;

        /// <summary>실패 뒤 반복 중인가 · 마지막으로 깨고 내려간 구역.</summary>
        public bool Repeating;
        public int ClearedStage;

        /// <summary>자리 체력을 한 번이라도 세웠나 — 「안 세운 판」과 「전멸한 판」을 가른다.</summary>
        public bool SeatsReady;

        /// <summary>오프라인 실측 근사 (combat.md 6)</summary>
        public int MeasuredStage;
        public double MeasuredKillsPerSecond;
    }
}
