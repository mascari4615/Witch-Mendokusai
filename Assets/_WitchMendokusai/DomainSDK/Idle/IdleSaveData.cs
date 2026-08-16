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
        /// <summary>옛 저장 호환으로 남긴다 — 지금은 안 읽는다.</summary>
        public double DamageDealtToTarget;

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

        /// <summary>등급별 떨어진 개수. 옛 저장에는 없어 null 로 온다.</summary>
        public long[] DroppedByTier;
        public double[] DropProgressByTier;

        /// <summary>주사위 상태 — 이게 없으면 껐다 켜서 다시 굴리기가 공짜가 된다.</summary>
        public long RandomState;
        public double BestPotentialValue;
        public int BestPotentialGrade;

        /// <summary>마지막으로 본 시각 (Unix 초, UTC).</summary>
        public long LastSeenUnixSeconds;
    }
}
