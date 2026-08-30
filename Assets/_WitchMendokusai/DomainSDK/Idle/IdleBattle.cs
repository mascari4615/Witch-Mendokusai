using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>적 종류. 사거리와 속도 차이 (combat.md 3)</summary>
    public enum IdleFoeKind
    {
        /// <summary>근접. 사거리 <see cref="IdleTuning.FoeMeleeRange"/></summary>
        Melee = 0,

        /// <summary>원거리. 사거리 <see cref="IdleTuning.FoeRangedRange"/></summary>
        Ranged = 1,
    }

    /// <summary>전장에 서 있는 적 하나. 라이브 층이라 저장 안 함</summary>
    public sealed class IdleFoe
    {
        /// <summary>처치 누적 기준 번호. 무대가 같은 적을 같은 물체로 잇는 열쇠</summary>
        public long Index;

        public IdleFoeKind Kind;

        public bool Boss;

        public double X;

        public double Y;

        public double Health;

        public double MaxHealth;

        /// <summary>다음 공격까지 남은 초</summary>
        public double Cooldown;

        /// <summary>사거리 (m)</summary>
        public double Range;

        /// <summary>이동 속도 (m/s)</summary>
        public double Speed;

        /// <summary>공격 간격 (s)</summary>
        public double AttackSeconds;

        /// <summary>한 방 피해</summary>
        public double Damage;

        public double HealthRatio => MaxHealth > 0d ? (Health < 0d ? 0d : Health / MaxHealth) : 0d;
    }

    /// <summary>이번 틱에 난 타격 하나. 무대의 숫자와 볼트 근거</summary>
    public readonly struct IdleHit
    {
        public IdleHit(int seat, long foeIndex, double damage, bool byFoe)
        {
            Seat = seat;
            FoeIndex = foeIndex;
            Damage = damage;
            ByFoe = byFoe;
        }

        /// <summary>인형 자리. 적이 때린 타격이면 맞은 자리</summary>
        public int Seat { get; }

        /// <summary>적 번호. 인형이 때렸으면 맞은 적, 적이 때렸으면 때린 적</summary>
        public long FoeIndex { get; }

        public double Damage { get; }

        /// <summary>참이면 적이 인형을 때린 것</summary>
        public bool ByFoe { get; }
    }

    /// <summary>
    /// 라이브 전투의 위치 상태 (combat.md 5). 저장 안 함. 켤 때마다 새 웨이브
    ///
    /// ★ 체력, 부활, 처치, 구역은 여전히 <see cref="IdleState"/> 의 것. 여기는 위치와 적 목록과 쿨만
    /// </summary>
    public sealed class IdleBattle
    {
        /// <summary>자리별 x. 오른쪽이 적</summary>
        public double[] X = new double[IdleSquad.SEAT_COUNT];

        /// <summary>자리별 y (줄)</summary>
        public double[] Y = new double[IdleSquad.SEAT_COUNT];

        /// <summary>자리별 다음 공격까지 남은 초</summary>
        public double[] Cooldown = new double[IdleSquad.SEAT_COUNT];

        /// <summary>자리별 지금 목표 적 번호. 없으면 -1</summary>
        public long[] Target = new long[IdleSquad.SEAT_COUNT];

        /// <summary>자리별 이번 틱 이동 여부. 무대의 걷기 연출용</summary>
        public bool[] Moving = new bool[IdleSquad.SEAT_COUNT];

        public readonly List<IdleFoe> Foes = new List<IdleFoe>();

        /// <summary>이번 <see cref="IdleBattleSim.Advance"/> 에서 난 타격. 부를 때마다 초기화</summary>
        public readonly List<IdleHit> Hits = new List<IdleHit>();

        /// <summary>이 구역에서 몇 번째 웨이브인가 (0 부터)</summary>
        public int Wave;

        /// <summary>틱으로 못 채운 이월 초</summary>
        public double Carry;

        /// <summary>한 번이라도 세웠나. 거짓이면 첫 Advance 가 Reset</summary>
        public bool Ready;

        /// <summary>실측 창: 이 구역에서 잰 초</summary>
        public double MeasureSeconds;

        /// <summary>실측 창: 그동안 처치</summary>
        public long MeasureKills;

        /// <summary>실측 창이 열린 구역</summary>
        public int MeasureStage;

        /// <summary>여태 만든 적 수. 번호 발급</summary>
        public long Spawned;

        /// <summary>마지막으로 세운 구역. 구역이 바뀌면 다음 틱이 Reset</summary>
        public int StageSeen = -1;

        public IdleFoe FoeOf(long index)
        {
            for (int at = 0; at < Foes.Count; at++)
            {
                if (Foes[at].Index == index)
                {
                    return Foes[at];
                }
            }

            return null;
        }
    }
}
