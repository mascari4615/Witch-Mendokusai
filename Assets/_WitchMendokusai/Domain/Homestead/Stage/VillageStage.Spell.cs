using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 생활 주문을 마을의 기다리는 칸에서 씀 (HomesteadSim.Spell)
	// - 북돋기: 물 준 자라는 밭의 "살피기" 자리. 배웠고 재료가 있으면 버튼이 "북돋기" 로 바뀜
	// - 물 주기, 갈기처럼 도구가 할 일이 있는 칸은 건드리지 않음 (도구 몫이 먼저)
	public sealed partial class VillageStage
	{
		private bool CanRipenHere(int index) => Sim.CanRipen(index).Ok;

		private string WaitVerb(int index) => CanRipenHere(index) ? "북돋기" : "살피기";

		private string WaitRole(int index)
		{
			string left = HomesteadText.Minutes(Sim.MinutesLeft(Sim.State.Plots[index]));
			return CanRipenHere(index) ? $"{left}, 북돋기로 바로" : left;
		}

		// 북돋기를 썼으면 true. 못 쓰는 칸이면 아무것도 안 하고 false (부른 쪽이 남은 시간을 말함)
		private bool TryRipen(int index)
		{
			if (CanRipenHere(index) == false)
				return false;
			if (Report(Sim.Ripen(index)) == false)
				return true;
			UiSfx.Play(UiSfxEvent.Confirm);
			StartCoroutine(Pop(plots[index].Wet.transform));
			Say($"{Catalog.Get<SpellDef>(HomesteadSim.SPELL_RIPEN).Name}: 바로 익었습니다.");
			return true;
		}
	}
}
