using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 지금 판의 상태 — 모은 자원, 축별 레벨, 그리고 아직 안 끝난 타격의 잔여.
    ///
    /// ★ double 을 쓴다 — 방치형은 곧 float 정밀도를 넘긴다(1e7 부터 1 단위가 사라진다).
    /// ★ 잔여 피해까지 상태다 — 이걸 안 들고 있으면 짧은 스텝을 여러 번 밟을 때 피해가 매번 버려져
    ///   같은 시간을 재도 스텝 크기에 따라 결과가 달라진다. 저장·오프라인 보상이 그 위에 서므로 치명적이다.
    /// </summary>
    public sealed partial class IdleState : ISavable<IdleSaveData>
    {
        /// <summary>모은 자원.</summary>
        public double Resource { get; set; }

        /// <summary>지금까지 처치한 대상 수 — 진행감·통계용.</summary>
        public long Kills { get; set; }

        /// <summary>지금 대상에게 이미 넣어 둔 피해.</summary>
        /// <summary>이번 대상을 이미 때린 횟수 — 스텝 불변의 근거다.</summary>
        public long HitsOnTarget { get; set; }

        /// <summary>아직 한 번을 못 채운 공격 — 이걸 들고 가야 쪼개 밟아도 결과가 같다.</summary>
        public double AttackProgress { get; set; }
        /// <summary>지금 내려와 있는 단계 (1부터).</summary>
        public int Stage { get; set; } = 1;

        /// <summary>이번 단계에서 처치한 수 — 이게 <see cref="IdleTuning.KillsPerStage"/> 에 닿으면 내려간다.</summary>
        public int KillsInStage { get; set; }

        /// <summary>
        /// 여태 닿아 본 가장 깊은 단계.
        ///
        /// ★ 지금은 안 쓴다 — <b>나중에 쓸 자리를 지금 저장에 만들어 둔다.</b> 대열 방치 전투 계열에서
        ///   「스테이지마다 나올 수 있는 장비 단계의 상한」이 이 값에 걸린다. 저장 형식은 나중에
        ///   바꾸기가 가장 비싼 물건이라, 확실히 올 칸은 미리 판다.
        /// </summary>
        public int BestStage { get; set; } = 1;

        /// <summary>
        /// 여기 <b>머문다</b> — 단계를 다 밀어도 안 내려간다.
        ///
        /// ★ 이 게임의 <b>첫 번째 진짜 선택</b>이다(TASK 의 「반복 결정 셋」 중 ① 어디서 사냥할까).
        ///   수치상 갈등은 이미 있었다 — 얕으면 빨리 잡아 <b>많이</b> 떨구고,
        ///   깊으면 느리지만 <b>좋은 것</b>이 떨어진다(등급 상한이 깊이에 걸려 있으니까).
        ///   여태는 그 갈등을 코어가 혼자 결정했다. 이제 사람이 정한다.
        /// </summary>
        public bool HoldingStage { get; set; }

        /// <summary>여태 모은 리셋 점수 — 리셋해도 안 사라진다. 이게 「다시 시작」을 보상으로 만드는 것.</summary>
        public long PrestigePoints { get; set; }

        /// <summary>
        /// 보스와 던전에서 <b>주운</b> 환생 조각 (economy.md 표 2, E3).
        ///
        /// ★ 계산분(<c>PrestigeStandingFor</c>)과 그릇을 분리. 한 그릇에 담으면
        ///   환생이 계산값을 <b>대입</b>하면서 주운 것을 통째로 삭제
        /// </summary>
        public long PrestigeShards { get; set; }

        /// <summary>던전 입장권, 던전마다 하나 (economy.md 4). 정본은 <see cref="IdleDungeons"/></summary>
        public long[] Tickets { get; private set; } = new long[IdleDungeons.COUNT];

        /// <summary>입장권을 마지막으로 채운 날 번호. 날이 바뀌었나를 이걸로 안다</summary>
        public long TicketDay { get; set; }

        /// <summary>무료 상자를 마지막으로 연 날 번호. 정본은 <see cref="IdleFreeBox"/></summary>
        public long FreeBoxDay { get; set; }

        /// <summary>고른 배속의 자리 (gap-2026-08-23 P1-6). 0 이 1배</summary>
        public int SpeedStep { get; set; }

        /// <summary>상점에서 산 가방 확장 묶음 수 (사용자 판정 2026-09-01). 환생 때 0 으로</summary>
        public int BagUpgrades { get; set; }

        /// <summary>코스트가 차면 알아서 카드를 내나 (P1-6)</summary>
        public bool AutoCast { get; set; }

        /// <summary>옛 저장에는 입장권 칸이 없어 null 이나 짧은 배열로 온다</summary>
        public void EnsureTicketRoom()
        {
            if (Tickets != null && Tickets.Length == IdleDungeons.COUNT)
            {
                return;
            }

            long[] made = new long[IdleDungeons.COUNT];

            if (Tickets != null)
            {
                int carry = Tickets.Length < made.Length ? Tickets.Length : made.Length;

                for (int index = 0; index < carry; index++)
                {
                    made[index] = Tickets[index];
                }
            }

            Tickets = made;
        }

        /// <summary>몇 번 리셋했나.</summary>
        public int Ascensions { get; set; }

        /// <summary>등급별로 여태 떨어진 개수 (0번째 = 1등급).</summary>
        public long[] DroppedByTier { get; private set; } = new long[0];

        /// <summary>
        /// 생산자 종류별 보유 수 — 기지가 내는 자원의 근거.
        ///
        /// ★ <b>첫 하나는 쥐여 준다</b>. 자원이 0 이면 아무것도 못 사고, 아무것도 못 사면
        ///   자원이 안 늘어 <b>게임이 시작되지 않는다</b>. 생산자 클리커 계열이 첫 클릭을 주는 자리와 같다.
        /// </summary>
        public long[] Owned { get; private set; } = new long[] { 1L };

        /// <summary>가방 — 모험이 가져온 장비.</summary>
        public System.Collections.Generic.List<IdleItem> Bag { get; private set; }
            = new System.Collections.Generic.List<IdleItem>();

        /// <summary>부위마다 차고 있는 것 (빈 자리는 등급 0).</summary>
        /// <summary>
        /// 인형별 장비 (사용자 2026-08-31: 유닛이 여럿인데 가방에서 바로 끼우는 건 기획 오류).
        /// 자리는 <c>인형 번호 * SLOT_COUNT + 부위</c>. 옛 판 공용 4칸은 시작 인형 것으로 이관
        /// </summary>
        public IdleItem[] Worn { get; private set; } = new IdleItem[IdleHeroes.Count * IdleGear.SLOT_COUNT];

        /// <summary>떨어진 순번 — 부위를 돌려 주는 데 쓴다(무작위 X, 결정적).</summary>
        public long DropSequence { get; set; }

        /// <summary>뽑아서 가진 영웅들 (TASK-WM-406).</summary>
        public System.Collections.Generic.List<IdleHeroOwned> Heroes { get; private set; }
            = new System.Collections.Generic.List<IdleHeroOwned>();

        /// <summary>
        /// 편성. 각 칸에 영웅 <see cref="IdleHeroKind.Id"/>, 빈 칸은 -1.
        /// 앞 <see cref="IdleHeroes.MAIN_SLOTS"/> 칸이 메인(출전), 뒤가 보조(전장 불참).
        ///
        /// ★ <b>보유</b>와 <b>출전</b>을 나눈 자리다. 안 나눴으면 전원 참전이 늘 정답이라
        ///   「누구를 내보낼까」가 결정이 아니게 된다.
        /// </summary>
        public int[] Party { get; private set; } = IdleHeroes.EmptyParty();

        /// <summary>천장까지 남은 셈 — 마지막 최고등급 이후 몇 번 뽑았나.</summary>
        public int PullsSincePity { get; set; }

        /// <summary>지나가는 것이 떠 있는 남은 시간(초). 0 이면 지금은 없다.</summary>
        public double VisitorSecondsLeft { get; set; }

        /// <summary>마지막으로 뜬 뒤 흐른 시간(초) — 기다린 만큼 잘 뜬다.</summary>
        public double SinceVisitorSeconds { get; set; }

        /// <summary>지금 걸린 폭주 (<see cref="IdleSurgeKind"/>).</summary>
        public int SurgeKind { get; set; }

        /// <summary>그 폭주가 남은 시간(초).</summary>
        public double SurgeSecondsLeft { get; set; }

        /// <summary>
        /// 쓸 수 있는 <b>환생석</b> — 뽑기에 낸다 (TASK-WM-406).
        ///
        /// ★ <b>배수와 갈라 둔다.</b> 배수는 <see cref="PrestigePoints"/>(여태 가장 깊이 간 자리)가
        ///   정하고, 이건 <b>쓰면 준다</b>. 하나로 겸했더니 뽑을수록 손해였다 —
        ///   실측 이레: 안 뽑음 1619단계 vs 다 뽑음 104단계.
        ///   한 재화가 <b>지수 성장</b>과 <b>일회성 소비</b>를 겸하면 소비 쪽은 늘 진다.
        ///   생산자 클리커 계열이 명성을 <b>누적</b> 쿠키로 매기는 것과 같은 수법이다.
        /// </summary>
        public long Stones { get; set; }

        /// <summary>
        /// 여태 뽑은 총 횟수 — <b>값이 여기를 따라 오른다</b>.
        ///
        /// ★ 환생해도 안 돌아간다. 뽑은 얼굴은 남으니 값도 남아야 앞뒤가 맞는다.
        /// </summary>
        public long PullsDone { get; set; }

        /// <summary>가진 영웅이 목록의 몇 번째인가. 없으면 -1.</summary>
        public int IndexOfHero(int id)
        {
            for (int index = 0; index < Heroes.Count; index++)
            {
                if (Heroes[index].Id == id)
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>생산자 칸을 넉넉히 잡아 둔다 — 늘리기만 한다.</summary>
        public void EnsureProducerRoom(int count)
        {
            if (Owned.Length >= count)
            {
                return;
            }

            long[] grown = new long[count];
            for (int i = 0; i < Owned.Length; i++)
            {
                grown[i] = Owned[i];
            }

            Owned = grown;
        }

        /// <summary>주사위의 지금 상태 — 저장에 실린다. 안 실으면 껐다 켜서 다시 굴리기가 공짜가 된다.</summary>
        public long RandomState { get; set; } = 0x2545F4914F6CDD1DL;

        /// <summary>여태 뽑은 가장 좋은 잠재 값(비율).</summary>
        public double BestPotentialValue { get; set; }

        /// <summary>그 잠재의 등급 (<see cref="PotentialGrade"/>).</summary>
        public int BestPotentialGrade { get; set; }

        /// <summary>등급별 잔여분 — 아직 하나가 안 된 몫. 이걸 들고 가야 쪼개 밟아도 총합이 같다.</summary>
        public double[] DropProgressByTier { get; private set; } = new double[0];

        /// <summary>
        /// 등급 칸을 넉넉히 잡아 둔다. 손잡이(<see cref="IdleTuning.MaxTier"/>)가 커질 수 있어
        /// <b>늘리기만</b> 한다 — 줄이면 이미 떨어진 것이 사라진다.
        /// </summary>
        public void EnsureTierRoom(int tierCount)
        {
            if (tierCount < 1)
            {
                tierCount = 1;
            }

            if (DroppedByTier.Length >= tierCount)
            {
                return;
            }

            long[] grownCounts = new long[tierCount];
            double[] grownProgress = new double[tierCount];

            for (int i = 0; i < DroppedByTier.Length; i++)
            {
                grownCounts[i] = DroppedByTier[i];
                grownProgress[i] = DropProgressByTier[i];
            }

            DroppedByTier = grownCounts;
            DropProgressByTier = grownProgress;
        }

        /// <summary>마지막으로 본 시각 (Unix 초, UTC). 오프라인 보상의 재료.</summary>
        public long LastSeenUnixSeconds { get; set; }

        /// <summary>
        /// 카드 코스트 — 시간이 채우고 카드가 쓴다 (V2, concept-v2).
        ///
        /// ★ 환생해도 <b>안 지운다</b> — 코스트는 판의 세기가 아니라 개입의 리듬이라,
        ///   지우면 환생 직후의 「바로 한 장」이 없어진다.
        /// </summary>
        public double Cost { get; set; }

        /// <summary>긴급 보급이 남은 시간(초) — 걸려 있는 동안 기지 수입이 몇 배가 된다.</summary>
        public double SupplySecondsLeft { get; set; }

        public int[] CardDeck { get; private set; } = new int[0];

        /// <summary>새 판은 기본 덱으로 선다. 빈 덱을 적고 불러오며 채우면 저장 왕복이 갈린다</summary>
        public IdleState()
        {
            IdleCards.EnsureDeck(this);
        }

        public void SetCardDeck(IdleCardKind[] deck)
        {
            CardDeck = new int[deck.Length];
            for (int index = 0; index < deck.Length; index++)
            {
                CardDeck[index] = (int)deck[index];
            }
        }

        /// <summary>자리별 남은 체력 (0 = 쓰러짐). 0번 = 나, 1~3 = 파티 자리 (V2 부대층).</summary>
        public double[] SeatHealth { get; private set; } = new double[IdleSquad.SEAT_COUNT];

        /// <summary>쓰러진 자리의 부활 게이지(초).</summary>
        public double[] SeatReviveSeconds { get; private set; } = new double[IdleSquad.SEAT_COUNT];

        /// <summary>실패해서 <b>반복</b> 중인가 — 클리어해도 안 내려간다. 사람이 「다음 구역」을 눌러야 푼다.</summary>
        public bool Repeating { get; set; }

        /// <summary>마지막으로 <b>깨고 내려간</b> 구역 — 실패하면 여기로 물러난다.</summary>
        public int ClearedStage { get; set; }

        /// <summary>
        /// 자리 체력을 <b>한 번이라도 세웠나</b>.
        ///
        /// ★ 이게 없으면 「아직 안 세운 판」과 「전멸한 판」이 <b>똑같이 체력 0</b> 이라 구별이 안 된다.
        ///   그래서 사진을 찍기만 해도(조회) 판을 세워야 했고, 그건 「묻는 자리는 판을 안 건드린다」를 깬다.
        /// </summary>
        public bool SeatsReady { get; set; }

        /// <summary>라이브 전투의 위치 층 (combat.md). 저장 안 함</summary>
        public IdleBattle Battle { get; } = new IdleBattle();

        /// <summary>실측이 있는 가장 깊은 구역. 0 이면 실측 없음</summary>
        public int MeasuredStage { get; set; }

        /// <summary>그 구역에서 라이브로 잰 초당 처치. 오프라인 정산의 근거</summary>
        public double MeasuredKillsPerSecond { get; set; }

        /// <summary>
        /// 자리 칸을 갖추고, <b>새로 온 자리는 만렙 체력</b>으로 세운다.
        ///
        /// ★ 옛 저장·새 판은 체력이 0 이라 그대로 두면 <b>시작하자마자 전멸</b>이다.
        /// ★ 쓰러진 자리(부활 게이지가 돌고 있다)와 <b>새로 앉은 자리</b>를 갈라 본다 —
        ///   안 가르면 부활 대기 중인 영웅이 매 프레임 공짜로 일어난다.
        /// </summary>
        public void EnsureSeatRoom(IdleTuning tuning)
        {
            if (SeatHealth.Length < IdleSquad.SEAT_COUNT)
            {
                SeatHealth = new double[IdleSquad.SEAT_COUNT];
                SeatReviveSeconds = new double[IdleSquad.SEAT_COUNT];
            }

            // 전장에 하나 필수. 자리 0(나) 삭제 뒤로는 시작 인형이 그 몫
            IdleHeroes.EnsureStarter(this);

            bool first = SeatsReady == false;
            SeatsReady = true;

            for (int seat = 0; seat < IdleSquad.SEAT_COUNT; seat++)
            {
                if (IdleSquad.SeatTaken(this, seat) == false)
                {
                    SeatHealth[seat] = 0d;
                    SeatReviveSeconds[seat] = 0d;
                    continue;
                }

                // 처음 세우는 판, 또는 <b>새로 앉힌 자리</b>(체력도 게이지도 0)는 만렙으로.
                if (first || (SeatHealth[seat] <= 0d && SeatReviveSeconds[seat] <= 0d))
                {
                    SeatHealth[seat] = IdleSquad.MaxHealthOf(this, tuning, seat);
                }
            }
        }

        /// <summary>공격력 레벨.</summary>
        public UpgradeLevel Damage { get; } = new UpgradeLevel();

        /// <summary>공격속도 레벨.</summary>
        public UpgradeLevel AttackSpeed { get; } = new UpgradeLevel();

        /// <summary>한 축의 레벨 상태를 고른다.</summary>
        public UpgradeLevel LevelOf(IdleUpgradeKind kind)
        {
            return kind == IdleUpgradeKind.Damage ? Damage : AttackSpeed;
        }
    }
}

