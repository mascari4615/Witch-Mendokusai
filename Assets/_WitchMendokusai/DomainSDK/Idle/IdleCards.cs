namespace WitchMendokusai.DomainSDK.Idle
{
	/// <summary>손패의 카드 — 무엇을 하는 카드인가 (V2, concept-v2).</summary>
	public enum IdleCardKind
	{
		/// <summary>일제 사격 — 자동 공격 몇 초치를 즉시 몰아친다.</summary>
		Volley = 0,

		/// <summary>긴급 보급 — 한동안 기지 수입이 몇 배가 된다.</summary>
		Supply = 1,

		/// <summary>비밀 감정 — 감정 하나를 자원 없이 굴린다 (개수는 쓴다).</summary>
		Appraise = 2,
	}

	/// <summary>한 카드가 화면에 보이는 모습.</summary>
	public readonly struct IdleCardView
	{
		public IdleCardKind Kind { get; }

		/// <summary>이 카드를 내는 데 드는 코스트.</summary>
		public double Cost { get; }

		/// <summary>지금 낼 수 있나 — 판정은 코어가 한다 (버튼 흐리기는 친절이지 규칙이 아니다).</summary>
		public bool CanCast { get; }

		public IdleCardView(IdleCardKind kind, double cost, bool canCast)
		{
			Kind = kind;
			Cost = cost;
			CanCast = canCast;
		}
	}

	/// <summary>카드 한 장을 낸 결과 — 화면이 「무슨 일이 났나」를 보여줄 재료.</summary>
	public readonly struct IdleCardResult
	{
		public IdleCardKind Kind { get; }

		/// <summary>감정 카드였다면 그 굴림. <see cref="HasRoll"/> 이 참일 때만 뜻이 있다.</summary>
		public PotentialRoll Roll { get; }

		public bool HasRoll { get; }

		public IdleCardResult(IdleCardKind kind, PotentialRoll roll, bool hasRoll)
		{
			Kind = kind;
			Roll = roll;
			HasRoll = hasRoll;
		}
	}

	/// <summary>
	/// 카드와 코스트 — <b>개입의 전부</b> (V2, 사용자 방향 2026-08-23: 자동전투+카드 개입 계열 문법).
	///
	/// ★ 전투는 전부 자동이다. 사람 몫은 「코스트가 찼을 때 어느 카드를 내나」 하나로 모은다 —
	///   그래야 개입이 노동이 아니라 <b>가장 기분 좋은 순간</b>이 된다 (자동전투+카드 개입 계열의 검증된 균형).
	///
	/// ★ 코스트는 <b>시간이 채운다</b> (<see cref="IdleModel.Step"/>) — 방치와 정합.
	///   자리를 비우면 가득 찬 채로 맞이한다: 카드 시전이 곧 <b>복귀 보상</b>이다.
	///
	/// ★ 무작위가 없다 — 유일한 도박(감정 카드)은 기존 잠재 굴림(<see cref="IdlePotentials"/>)을
	///   그대로 태운다. 스텝 불변·오프라인 정산이 그 위에 서 있는 성질이라 여기서도 지킨다.
	/// </summary>
	public static class IdleCards
	{
		/// <summary>손패의 카드 수 — 화면·시험이 이 수로 돈다.</summary>
		public const int HAND_SIZE = 3;
		public const int DECK_SIZE = 6;
		public const int CARD_COUNT = HAND_SIZE;

		/// <summary>손패 뒤에 <b>줄 서 있는</b> 카드 수 (gap-2026-08-23 P1 순환 손패)</summary>
		public const int QUEUE_SIZE = DECK_SIZE - HAND_SIZE;

		private static readonly IdleCardKind[] DEFAULT_DECK =
		{
			IdleCardKind.Volley,
			IdleCardKind.Supply,
			IdleCardKind.Appraise,
			IdleCardKind.Volley,
			IdleCardKind.Supply,
			IdleCardKind.Volley,
		};

		public static void EnsureDeck(IdleState state)
		{
			if (state.CardDeck.Length == DECK_SIZE && HasKnownKinds(state.CardDeck))
			{
				return;
			}

			state.SetCardDeck(DEFAULT_DECK);
		}

		public static IdleCardKind HandAt(IdleState state, int handIndex)
		{
			EnsureDeck(state);
			return handIndex >= 0 && handIndex < HAND_SIZE ? (IdleCardKind)state.CardDeck[handIndex] : IdleCardKind.Volley;
		}

		/// <summary>
		/// 줄 선 카드. 다음에 손패로 올라올 순서대로
		///
		/// ★ 이게 안 보이면 순환이 무작위와 구별이 안 된다. "지금 볼리를 쓰면 다음이 보급" 을
		///   알아야 <c>어느 것을 먼저 쓰나</c> 가 결정이 된다 (gap-2026-08-23 P1)
		/// </summary>
		public static IdleCardKind QueuedAt(IdleState state, int queueIndex)
		{
			EnsureDeck(state);

			if (queueIndex < 0 || queueIndex >= QUEUE_SIZE)
			{
				return IdleCardKind.Volley;
			}

			return (IdleCardKind)state.CardDeck[HAND_SIZE + queueIndex];
		}

		/// <summary>낸 카드를 맨 뒤로 보낸다 (순환). 앞의 것들이 한 칸씩 당겨진다</summary>
		private static void SendToTheBack(IdleState state, int handIndex)
		{
			int used = state.CardDeck[handIndex];

			for (int index = handIndex; index < state.CardDeck.Length - 1; index++)
			{
				state.CardDeck[index] = state.CardDeck[index + 1];
			}

			state.CardDeck[state.CardDeck.Length - 1] = used;
		}

		public static bool TryCastHand(IdleState state, IdleTuning tuning, int handIndex,
			out IdleCardResult result)
		{
			result = default;
			if (handIndex < 0 || handIndex >= HAND_SIZE)
			{
				return false;
			}

			IdleCardKind kind = HandAt(state, handIndex);
			if (TryCast(state, tuning, kind, out result) == false)
			{
				return false;
			}

			SendToTheBack(state, handIndex);
			return true;
		}

		public static bool TryCastHandAt(IdleState state, IdleTuning tuning, int handIndex, long foeIndex,
			out IdleCardResult result)
		{
			result = default;
			if (handIndex < 0 || handIndex >= HAND_SIZE || HandAt(state, handIndex) != IdleCardKind.Volley)
			{
				return false;
			}

			if (CanCast(state, tuning, IdleCardKind.Volley) == false
				|| IdleBattleSim.StrikeForTarget(state, tuning, tuning.VolleySecondsOfAttack, foeIndex) == false)
			{
				return false;
			}

			state.Cost -= CostOf(IdleCardKind.Volley, tuning);
			SendToTheBack(state, handIndex);
			result = new IdleCardResult(IdleCardKind.Volley, default, false);
			return true;
		}

		private static bool HasKnownKinds(int[] deck)
		{
			for (int index = 0; index < deck.Length; index++)
			{
				if (deck[index] < (int)IdleCardKind.Volley || deck[index] > (int)IdleCardKind.Appraise)
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// 코스트가 찼으면 낼 수 있는 카드를 <b>앞에서부터</b> 한 장 (gap-2026-08-23 P1-6 AUTO).
		///
		/// ★ 한 번에 한 장만. 다 쏟아 내면 자동이 사람보다 늘 잘하게 되어 손패가 장식.
		///   앞에서부터 고르는 이유는 순환이 곧 순서인 것. 자동도 그 순서를 따라야 예고가 뜻을 가짐
		/// </summary>
		public static bool AutoCastOne(IdleState state, IdleTuning tuning, out IdleCardResult result)
		{
			result = default;

			if (state.AutoCast == false)
			{
				return false;
			}

			for (int handIndex = 0; handIndex < HAND_SIZE; handIndex++)
			{
				if (CanCast(state, tuning, HandAt(state, handIndex))
					&& TryCastHand(state, tuning, handIndex, out result))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>이 카드를 내는 데 드는 코스트.</summary>
		public static double CostOf(IdleCardKind kind, IdleTuning tuning)
		{
			switch (kind)
			{
				case IdleCardKind.Volley: return tuning.VolleyCost;
				case IdleCardKind.Supply: return tuning.SupplyCost;
				default: return tuning.AppraiseCardCost;
			}
		}

		/// <summary>
		/// 보급이 지금 기지 수입에 곱하는 배수 — 안 걸려 있으면 1.
		///
		/// ★ 판정이 여기 한 벌만 있다. 화면이 「×3 중」을 자기 눈으로 세면 언젠가 갈린다.
		/// </summary>
		public static double SupplyMultiplier(IdleState state, IdleTuning tuning)
		{
			return state.SupplySecondsLeft > 0d ? tuning.SupplyMultiplier : 1d;
		}

		/// <summary>
		/// 감정 카드가 굴릴 등급 — <b>가진 것 중 가장 높은</b> 감정 가능 등급. 없으면 0.
		///
		/// ★ 높은 등급일수록 잠재 바닥이 위라(등급 사이가 안 겹친다) 늘 가장 높은 것이 최선이다 —
		///   고를 것이 없는 선택은 코어가 대신 한다.
		/// </summary>
		public static int BestAppraisableTier(IdleState state)
		{
			for (int tier = state.DroppedByTier.Length; tier >= 2; tier--)
			{
				if (IdlePotentials.GradeFor(tier) != PotentialGrade.None
					&& state.DroppedByTier[tier - 1] > 0L)
				{
					return tier;
				}
			}

			return 0;
		}

		/// <summary>지금 이 카드를 낼 수 있나.</summary>
		public static bool CanCast(IdleState state, IdleTuning tuning, IdleCardKind kind)
		{
			if (state.Cost < CostOf(kind, tuning))
			{
				return false;
			}

			if (kind == IdleCardKind.Appraise)
			{
				return BestAppraisableTier(state) > 0;
			}

			return true;
		}

		/// <summary>
		/// 카드 한 장을 낸다. 코스트가 모자라면 아무 일도 안 일어난다.
		///
		/// ★ 보급은 <b>겹치지 않는다</b> — 남은 시간을 새로 채울 뿐이다. 겹쳐 쌓이면
		///   코스트를 모아 두었다 몰아 쓰는 것이 늘 정답이 되어 「언제 낼까」가 결정이 아니게 된다.
		/// </summary>
		public static bool TryCast(IdleState state, IdleTuning tuning, IdleCardKind kind,
			out IdleCardResult result)
		{
			result = default;

			if (CanCast(state, tuning, kind) == false)
			{
				return false;
			}

			state.Cost -= CostOf(kind, tuning);

			switch (kind)
			{
				case IdleCardKind.Volley:
					IdleModel.StrikeFor(state, tuning, tuning.VolleySecondsOfAttack);
					result = new IdleCardResult(kind, default, false);
					return true;

				case IdleCardKind.Supply:
					state.SupplySecondsLeft = tuning.SupplySeconds;
					result = new IdleCardResult(kind, default, false);
					return true;

				default:
					int tier = BestAppraisableTier(state);
					IdlePotentials.RollFree(state, tuning, tier, out PotentialRoll roll);
					result = new IdleCardResult(kind, roll, true);
					return true;
			}
		}
	}
}
