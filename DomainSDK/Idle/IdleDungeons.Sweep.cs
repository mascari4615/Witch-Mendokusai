namespace WitchMendokusai.DomainSDK.Idle
{
	// IdleDungeons.cs 의 Sweep 조각. 같은 클래스의 partial. 소탕과 칸 보상 셈, 판 밖에서 도는 것
	public static partial class IdleDungeons
	{
		/// <summary>칸이 주는 장비 등급. 표에 없으면 칸 구역의 최고 등급 (천장은 환생 횟수)</summary>
		public static int GearTierOf(IdleState state, IdleTuning tuning, IdleDungeonStageSpec cell)
		{
			return cell.GearTier > 0 ? cell.GearTier : IdleDrops.MaxTierAt(cell.Level, state.Ascensions, tuning);
		}

		/// <summary>소탕 한 판이 주는 골드 (재화 던전). 화면이 칸에 적을 때도 같은 셈</summary>
		public static double SweepGoldOf(IdleState state, IdleTuning tuning, IdleDungeonKind kind, IdleDungeonStageSpec cell)
		{
			return kind == IdleDungeonKind.Gold && cell != null
				? IdleModel.IncomePerSecond(state, tuning) * cell.SweepGoldSeconds
				: 0d;
		}

		/// <summary>
		/// 남은 입장권을 한 번에 쓴다 (소탕). 한 번이라도 깬 칸만 (사용자 2026-09-08). 한 판은 그 칸 풀 클리어 몫
		///
		/// ★ 무작위 없음. 사람이 누를 때만 도는 자리지만 보상까지 굴리면 저장을 껐다 켜서 다시 뽑는 길이 생김
		/// ★ 가방이 차면 장비는 그만 들어오지만 골드와 조각은 계속 들어옴
		/// </summary>
		public static bool TrySweep(IdleState state, IdleTuning tuning, IdleDungeonKind kind, int difficulty, int stage,
			out IdleDungeonReward reward)
		{
			reward = new IdleDungeonReward(kind, 0, 0d, 0L, 0);
			IdleDungeonStageSpec cell = CellOf(tuning, kind, difficulty, stage);

			if (state.Dungeon.Active || cell == null || IsCleared(state, kind, difficulty, stage) == false)
			{
				return false;
			}

			int runs = 0;
			double gold = 0d;
			long shards = 0L;
			int gear = 0;
			int tier = GearTierOf(state, tuning, cell);

			while (TrySpend(state, kind))
			{
				runs++;
				switch (kind)
				{
					case IdleDungeonKind.Gold:
						double got = SweepGoldOf(state, tuning, kind, cell);
						state.Resource += got;
						gold += got;
						break;
					case IdleDungeonKind.Boss:
						shards += cell.Shards > 0L ? cell.Shards : 0L;
						state.PrestigeShards += cell.Shards > 0L ? cell.Shards : 0L;
						gear += IdleGear.Stow(state, tuning, tier, cell.GearCount);
						break;
					case IdleDungeonKind.Gear:
						gear += IdleGear.Stow(state, tuning, tier, cell.GearCount * (cell.Waves > 0 ? cell.Waves : 1));
						break;
				}
			}

			if (runs == 0)
			{
				return false;
			}

			reward = new IdleDungeonReward(kind, runs, gold, shards, gear);
			return true;
		}
	}
}
