using System;

namespace WitchMendokusai.DomainSDK.Idle
{
	/// <summary>
	/// 부대 — <b>맞고 쓰러지고 일어난다</b> (V2, 사용자 방향 2026-08-23).
	///
	/// ★ 여태 이 게임의 전투는 <b>일방적</b>이었다. 적이 반격하지 않으니 「더 내려간다」의
	///   유일한 벽이 시간(처치 속도)뿐이었고, 그래서 구역은 위험이 아니라 <b>기다림</b>이었다.
	///   이제 적이 때린다 — 벽이 시간에서 <b>생존</b>으로 옮겨 온다.
	///
	/// ★ 자리(seat) 셋. 편성의 <b>메인 칸</b> 그대로 (seat == 메인 칸 번호).
	///   플레이어 인형(자리 0, 늘 있던 나)은 2026-08-30 삭제 (C10). 대신 시작 인형 하나 지급
	///   (<see cref="IdleHeroes.EnsureStarter"/>). 빈 자리: 싸우지도 맞지도 않음.
	///   편성의 <b>보조 칸</b>(<see cref="IdleHeroes.SUPPORT_SLOTS"/>)은 여기 자리가 <b>없다</b> -
	///   전장에 안 서니 맞지도, 쓰러지지도, 일어나지도 않는다 (사용자 결정 2026-08-30).
	///
	/// ★ <b>맨 앞이 맞는다</b> — 서 있는 자리 중 가장 앞이 피해를 받는다. 흩뿌리면
	///   전멸이 한꺼번에 오고, 그러면 「하나 쓰러졌다」는 신호가 영영 안 뜬다.
	///
	/// ★ <b>부활은 하나라도 서 있을 때만</b> (사용자 방향 7): 쓰러진 자리는 게이지가
	///   차면 만렙 체력으로 돌아온다. 모두 쓰러지면 부활이 아니라 <b>실패</b>다(방향 5).
	///
	/// ★ 스텝 불변을 지킨다 — 이 층의 사건(쓰러짐·부활)은 <see cref="SecondsToNextEvent"/> 가
	///   미리 알려 주고, <see cref="IdleModel.Step"/> 이 그 경계에서 끊는다.
	///   그래야 「자리 비운 8시간을 한 번에」와 「0.1초씩 288000번」이 같은 답을 낸다.
	/// </summary>
	public static class IdleSquad
	{
		/// <summary>자리 수. 메인 칸 수와 동일</summary>
		public const int SEAT_COUNT = IdleHeroes.MAIN_SLOTS;

		/// <summary>이 자리에 누군가 있나. 자리는 메인 칸, 앉힌 인형 필수</summary>
		public static bool SeatTaken(IdleState state, int seat)
		{
			return IdleHeroes.IsMainSlot(seat) && seat < state.Party.Length && state.Party[seat] >= 0;
		}

		/// <summary>
		/// 이 자리의 최대 체력.
		///
		/// ★ 체력도 <b>키운 만큼</b> 는다 — 장비·환생이 공격만 올리고 체력은 안 올리면
		///   깊이가 늘수록 반드시 전멸한다(적 피해는 단계 지수라서). 같은 재료가 둘 다 민다.
		/// ★ 영웅은 <b>등급·★</b> 만큼 더 단단하다 — 뽑기의 값어치가 생존으로도 보이게.
		/// </summary>
		public static double MaxHealthOf(IdleState state, IdleTuning tuning, int seat)
		{
			if (SeatTaken(state, seat) == false)
			{
				return 0d;
			}

			double health = tuning.SeatBaseHealth
				* IdleGear.BaseMultiplier(state, tuning)
				* IdleModel.PrestigeMultiplier(state, tuning);

			int id = state.Party[seat];
			int index = state.IndexOfHero(id);

			if (index < 0)
			{
				return health;
			}

			IdleHeroOwned owned = state.Heroes[index];
			IdleHeroKind kind = IdleHeroes.KindOf(id);

			// 등급 무게 × ★ 계단 — 도감 쪽 규칙과 같은 꼴이라 새로 배울 것이 없다.
			double grade = 1d + (int)kind.Grade * tuning.HeroGradeHealthStep;
			double stars = 1d + owned.Stars * tuning.HeroStarStep;
			return health * grade * stars;
		}

		/// <summary>
		/// 지금 단계 적들이 <b>초당</b> 넣는 피해.
		///
		/// ★ 깊이의 지수다 — 그래서 아무리 세져도 언젠가는 벽이 온다. 그 벽이
		///   「물러나 파밍」과 「환생」을 부르는 자리다(둘 다 이미 있는 문법).
		/// </summary>
		public static double EnemyDamagePerSecond(IdleState state, IdleTuning tuning)
		{
			return tuning.EnemyDamageByStage.At(state.Stage - 1);
		}

		/// <summary>
		/// 이 자리가 서 있나 — <b>묻기만 한다</b>(판을 안 세운다).
		///
		/// ★ 아직 한 번도 안 세운 판(<see cref="IdleState.SeatsReady"/> 거짓)은 <b>전원 서 있는 것</b>으로 본다.
		///   그래야 사진 찍기·값 묻기가 판을 건드리지 않고, 이 층을 얹기 전의 곡선이 그대로 산다.
		/// </summary>
		public static bool Standing(IdleState state, int seat)
		{
			if (SeatTaken(state, seat) == false)
			{
				return false;
			}

			return state.SeatsReady == false || state.SeatHealth[seat] > 0d;
		}

		/// <summary>서 있는 자리 수.</summary>
		public static int StandingCount(IdleState state)
		{
			int standing = 0;

			for (int seat = 0; seat < SEAT_COUNT; seat++)
			{
				if (Standing(state, seat))
				{
					standing++;
				}
			}

			return standing;
		}

		/// <summary>설 수 있는 자리 수 — 빈 파티 자리는 안 센다.</summary>
		public static int TakenCount(IdleState state)
		{
			int taken = 0;

			for (int seat = 0; seat < SEAT_COUNT; seat++)
			{
				if (SeatTaken(state, seat))
				{
					taken++;
				}
			}

			return taken;
		}

		/// <summary>
		/// 지금 판이 내는 <b>싸움의 몫</b> — 쓰러진 자리는 안 때린다.
		///
		/// ★ 전원 서 있으면 1 이다. 그래서 이 층을 얹기 전의 판정·곡선이 그대로 산다.
		/// </summary>
		public static double FightingShare(IdleState state)
		{
			// 아직 안 세운 판은 전원 서 있는 것으로 (Standing 과 같은 규칙)
			// 시작 인형은 첫 스텝에서 지급. 그 전에 묻는 공격 속도가 0 이면 시뮬 셈 전부 무한대
			if (state.SeatsReady == false)
			{
				return 1d;
			}

			int taken = TakenCount(state);
			if (taken <= 0)
			{
				return 0d;
			}

			return (double)StandingCount(state) / taken;
		}

		/// <summary>맨 앞에 서 있는 자리 — 피해를 받는 자리다. 아무도 없으면 -1.</summary>
		public static int FrontSeat(IdleState state)
		{
			for (int seat = 0; seat < SEAT_COUNT; seat++)
			{
				if (Standing(state, seat))
				{
					return seat;
				}
			}

			return -1;
		}

		/// <summary>
		/// 다음 사건(쓰러짐·부활)까지 몇 초인가 — 없으면 무한.
		///
		/// ★ <see cref="IdleModel.Step"/> 이 이 값으로 스텝을 끊는다. 사건 경계를 안 끊으면
		///   「한 번에 밟기」와 「쪼개 밟기」가 갈린다(부활 뒤에는 판이 더 세지니까).
		/// </summary>
		public static double SecondsToNextEvent(IdleState state, IdleTuning tuning)
		{
			double soonest = double.PositiveInfinity;

			int front = FrontSeat(state);
			if (front >= 0)
			{
				double perSecond = EnemyDamagePerSecond(state, tuning);
				if (perSecond > 0d)
				{
					soonest = state.SeatHealth[front] / perSecond;
				}
			}

			// 부활은 <b>하나라도 서 있을 때만</b> 돈다 — 전멸이면 게이지가 멎는다.
			if (front >= 0)
			{
				for (int seat = 0; seat < SEAT_COUNT; seat++)
				{
					if (SeatTaken(state, seat) == false || state.SeatHealth[seat] > 0d)
					{
						continue;
					}

					double left = tuning.ReviveSeconds - state.SeatReviveSeconds[seat];
					if (left > 0d && left < soonest)
					{
						soonest = left;
					}
				}
			}

			return soonest;
		}

		/// <summary>
		/// 시간을 흘린다 — 맞고, 쓰러지고, 일어난다. 전멸하면 <paramref name="wiped"/> 가 참.
		///
		/// ★ <see cref="IdleModel.Step"/> 이 사건 경계에서 끊어 부르므로 이 안에서
		///   사건은 <b>많아야 하나</b>다 — 그래서 셈이 단순하고 결정적이다.
		/// </summary>
		public static void Advance(IdleState state, IdleTuning tuning, double seconds, out bool wiped)
		{
			wiped = false;

			if (seconds <= 0d)
			{
				return;
			}

			state.EnsureSeatRoom(tuning);

			int front = FrontSeat(state);
			if (front < 0)
			{
				// 이미 전멸한 판 — 실패 처리를 아직 안 받은 상태다. 여기서 다시 알린다.
				wiped = true;
				return;
			}

			// ① 맞는다 — 맨 앞이 받는다.
			double damage = EnemyDamagePerSecond(state, tuning) * seconds;
			if (damage > 0d)
			{
				state.SeatHealth[front] -= damage;

				if (state.SeatHealth[front] <= 1e-9d)
				{
					state.SeatHealth[front] = 0d;
					state.SeatReviveSeconds[front] = 0d;
				}
			}

			// ② 일어난다 — 하나라도 서 있을 때만.
			bool anyoneStanding = FrontSeat(state) >= 0;

			if (anyoneStanding)
			{
				for (int seat = 0; seat < SEAT_COUNT; seat++)
				{
					if (SeatTaken(state, seat) == false || state.SeatHealth[seat] > 0d)
					{
						continue;
					}

					state.SeatReviveSeconds[seat] += seconds;

					if (state.SeatReviveSeconds[seat] + 1e-9d >= tuning.ReviveSeconds)
					{
						state.SeatHealth[seat] = MaxHealthOf(state, tuning, seat);
						state.SeatReviveSeconds[seat] = 0d;
					}
				}
			}
			else
			{
				wiped = true;
			}
		}

		/// <summary>
		/// 전멸했다 — <b>이 구역은 실패</b>. 클리어했던 구역으로 물러나 <b>반복</b>에 들어간다 (방향 5·6).
		///
		/// ★ 잃는 것은 <b>이번 구역의 진행</b>뿐이다. 자원·장비·영웅은 그대로 —
		///   실패가 벌이 되면 아무도 깊이 안 내려간다. 실패는 <b>브레이크</b>지 손실이 아니다.
		/// </summary>
		public static void FallBack(IdleState state, IdleTuning tuning)
		{
			state.EnsureSeatRoom(tuning);

			int safe = state.ClearedStage > 0 ? state.ClearedStage : 1;
			if (safe > state.Stage)
			{
				safe = state.Stage;
			}

			state.Stage = safe < 1 ? 1 : safe;
			state.KillsInStage = 0;
			state.HitsOnTarget = 0L;
			state.AttackProgress = 0d;
			state.Repeating = true;

			HealAll(state, tuning);
		}

		/// <summary>
		/// 사람이 <b>다음 구역</b>을 누른다 — 반복을 끝내고 한 칸 내려간다 (방향 6).
		///
		/// ★ 자동으로 안 내려간다. 실패한 판에 자동으로 다시 밀어 넣으면 그건 도전이 아니라
		///   벽에 머리를 박는 것이다 — <b>다시 갈지는 사람이 정한다.</b>
		/// </summary>
		public static bool TryAdvanceStage(IdleState state, IdleTuning tuning)
		{
			if (state.Repeating == false)
			{
				return false;
			}

			state.Repeating = false;
			state.Stage += 1;
			state.KillsInStage = 0;
			state.HitsOnTarget = 0L;
			state.AttackProgress = 0d;

			if (state.Stage > state.BestStage)
			{
				state.BestStage = state.Stage;
			}

			HealAll(state, tuning);
			return true;
		}

		/// <summary>
		/// 처치할 때마다 <b>숨을 돌린다</b> — 잡은 만큼 회복 (V2).
		///
		/// ★ 없으면 <b>시간이 곧 죽음</b>이다: 회복이 구역 클리어에만 있으면 머물러 파밍하는 판은
		///   반드시 전멸한다 — 「어디서 사냥할까」라는 선택이 통째로 가짜가 된다(실측 2026-08-23,
		///   머무르기 시험 넷이 그렇게 깨졌다).
		///
		/// ★ 뜻도 맞는다 — 적을 잡으면 <b>그 적은 더 이상 안 때린다</b>. 단일 스트림 모델에서
		///   그걸 표현하는 가장 단순한 꼴이 「처치당 회복」이다.
		///   그래서 벽은 시계가 아니라 <b>처치 속도</b>가 만든다: 깊어져 잘 못 잡으면 죽는다.
		///
		/// ★ 결정적이다 — 처치 수는 스텝 불변이므로 회복도 그렇다.
		/// </summary>
		public static void HealOnKills(IdleState state, IdleTuning tuning, long kills)
		{
			// 자리를 아직 안 세운 판(시뮬·오프라인 정산)은 부대층이 안 도는 자리다 — 건드리지 않는다.
			if (state.SeatsReady == false || kills <= 0L || tuning.HealPerKillShare <= 0d)
			{
				return;
			}

			for (int seat = 0; seat < SEAT_COUNT; seat++)
			{
				// 쓰러진 자리는 회복이 아니라 <b>부활</b>을 기다린다 — 두 길이 섞이면 부활이 뜻을 잃는다.
				if (Standing(state, seat) == false)
				{
					continue;
				}

				double max = MaxHealthOf(state, tuning, seat);
				double healed = state.SeatHealth[seat] + max * tuning.HealPerKillShare * kills;
				state.SeatHealth[seat] = healed > max ? max : healed;
			}
		}

		/// <summary>전원 만렙 체력으로 — 실패 뒤·구역 이동 뒤의 채비.</summary>
		public static void HealAll(IdleState state, IdleTuning tuning)
		{
			state.EnsureSeatRoom(tuning);

			for (int seat = 0; seat < SEAT_COUNT; seat++)
			{
				state.SeatHealth[seat] = MaxHealthOf(state, tuning, seat);
				state.SeatReviveSeconds[seat] = 0d;
			}
		}

		/// <summary>이 자리의 남은 체력 비율(0~1) — 화면이 막대로 그릴 재료. 묻기만 한다.</summary>
		public static double HealthRatioOf(IdleState state, IdleTuning tuning, int seat)
		{
			if (SeatTaken(state, seat) == false)
			{
				return 0d;
			}

			// 아직 안 세운 판은 만렙으로 보인다 — 사진이 판을 세우지 않게.
			if (state.SeatsReady == false)
			{
				return 1d;
			}

			double max = MaxHealthOf(state, tuning, seat);
			if (max <= 0d)
			{
				return 0d;
			}

			double ratio = state.SeatHealth[seat] / max;
			return ratio < 0d ? 0d : (ratio > 1d ? 1d : ratio);
		}

		/// <summary>부활까지 얼마나 찼나(0~1) — 쓰러진 자리에만 뜻이 있다.</summary>
		public static double ReviveRatioOf(IdleState state, IdleTuning tuning, int seat)
		{
			if (tuning.ReviveSeconds <= 0d)
			{
				return 0d;
			}

			double ratio = state.SeatReviveSeconds[seat] / tuning.ReviveSeconds;
			return ratio < 0d ? 0d : (ratio > 1d ? 1d : ratio);
		}
	}
}
