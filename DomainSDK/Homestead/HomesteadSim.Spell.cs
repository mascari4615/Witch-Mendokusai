using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 생활 주문. 집 연구대에서 한 번 배우고, 마을의 기다리는 칸에서 재료를 내고 씀
	// - 업그레이드 (도구 등급) 와 인형 일 (특전) 은 늘 켜져 손을 넓힘. 주문은 한 번 내고 기다림을 한 번 건너뜀
	// - 그래서 칸 수, 범위, 젖음은 건드리지 않음. 안 써도 기다리면 같은 결과 (벌 없음)
	// - 주문마다 하는 일은 코드가 id 로 앎 (PERK_WATER 와 같은 결). 값과 이름은 spell.json
	public sealed partial class HomesteadSim
	{
		// 북돋기: 자라는 밭 한 칸을 바로 익힘
		public const string SPELL_RIPEN = "s_ripen";

		public bool IsSpellKnown(string spellId) => State.Spells.Contains(spellId);

		public HomesteadResult CanResearch(string spellId)
		{
			SpellDef spell = Catalog.Get<SpellDef>(spellId);
			if (IsSpellKnown(spellId))
			{
				return HomesteadResult.Fail(HomesteadFailure.AlreadyDone);
			}
			return CanTakeAll(spell.Research);
		}

		public HomesteadResult Research(string spellId)
		{
			HomesteadResult result = CanResearch(spellId);
			if (result.Ok == false)
			{
				return result;
			}
			TakeAll(Catalog.Get<SpellDef>(spellId).Research);
			State.Spells.Add(spellId);
			NotifyChanged();
			return HomesteadResult.Success;
		}

		// 배웠고 쓰는 재료가 있는가. 대상 조건은 주문마다 따로 (CanRipen)
		public HomesteadResult CanCast(string spellId)
		{
			SpellDef spell = Catalog.Get<SpellDef>(spellId);
			if (IsSpellKnown(spellId) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.Locked, spellId);
			}
			return CanTakeAll(spell.Cast);
		}

		public HomesteadResult CanRipen(int index)
		{
			if (PhaseOf(State.Plots[index]) != PlotPhase.Growing)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotReady);
			}
			return CanCast(SPELL_RIPEN);
		}

		public HomesteadResult Ripen(int index)
		{
			HomesteadResult result = CanRipen(index);
			if (result.Ok == false)
			{
				return result;
			}
			TakeAll(Catalog.Get<SpellDef>(SPELL_RIPEN).Cast);
			PlotState plot = State.Plots[index];
			plot.Growth = GrowthNeeded(plot);
			NotifyChanged();
			return HomesteadResult.Success;
		}

		private HomesteadResult CanTakeAll(Dictionary<string, int> goods)
		{
			foreach (KeyValuePair<string, int> pair in goods)
			{
				if (Count(pair.Key) < pair.Value)
				{
					return HomesteadResult.Fail(HomesteadFailure.NotEnoughGoods, pair.Key);
				}
			}
			return HomesteadResult.Success;
		}

		private void TakeAll(Dictionary<string, int> goods)
		{
			foreach (KeyValuePair<string, int> pair in goods)
			{
				Take(pair.Key, pair.Value);
			}
		}
	}
}
