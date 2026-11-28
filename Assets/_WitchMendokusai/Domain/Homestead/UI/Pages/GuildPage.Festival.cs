using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 길드 게시판의 축제 탭. 여는 조건, 준비물 칸, 열기, 오늘 축제의 인사
	public sealed partial class GuildPage
	{
		private HomesteadEntry FestivalEntry(FestivalDef festival)
		{
			bool isToday = Sim.TryGetFestivalToday(out FestivalDef today) && today.Id == festival.Id;
			bool canOpen = Sim.CanOpenFestival(festival.Id).Ok;
			int full = 0;
			for (int slot = 0; slot < festival.Slots.Count; slot++)
			{
				if (festival.Slots[slot].Optional == false && Sim.IsSlotFull(festival.Id, slot))
					full++;
			}
			int needed = festival.Slots.FindAll(slot => slot.Optional == false).Count;
			bool isLocked = Sim.State.TownHall < festival.TownHall;
			return new HomesteadEntry
			{
				Id = festival.Id,
				Icon = Art.Icon(HomesteadSim.HALL),
				Name = festival.Name,
				Sub = isToday ? "오늘 축제" : isLocked ? $"회관 {festival.TownHall}단 필요" : canOpen ? "열 수 있음" : $"준비 {full} / {needed}",
				IsDim = isLocked,
				IsMarked = canOpen || (isToday && CheerableCount() > 0),
				Detail = () => FestivalDetail(festival),
			};
		}

		private int CheerableCount() => Catalog.Residents.FindAll(resident => Sim.CanCheer(resident.Id)).Count;

		private HomesteadDetail FestivalDetail(FestivalDef festival)
		{
			FestivalState state = Sim.FestivalOf(festival.Id);
			HomesteadDetail detail = new() { Icon = Art.Icon(HomesteadSim.HALL), Title = festival.Name, Note = festival.Note };
			if (Sim.TryGetFestivalToday(out FestivalDef today) && today.Id == festival.Id)
			{
				detail.Lines.Add("오늘은 축제 날입니다. 회관 앞에 모인 주민에게 말을 걸어 축하 인사를 건네세요.");
				detail.Lines.Add($"인사 한 번에 호감 +{festival.CheerPoints}. 남은 주민 {CheerableCount()}");
				return detail;
			}
			detail.Lines.Add("여는 조건");
			foreach (FestivalNeed need in Sim.FestivalNeeds(festival))
				detail.Lines.Add($"  {(need.IsMet ? "다 됨" : "아직")}  {NeedText(need)}");
			if (state.Times > 0 && Sim.Day < Sim.FestivalReadyDay(festival.Id))
				detail.Lines.Add($"  다시 열기까지 {Sim.FestivalReadyDay(festival.Id) - Sim.Day}일");
			detail.Lines.Add("준비물 (나눠 바쳐도 남습니다)");
			for (int slot = 0; slot < festival.Slots.Count; slot++)
			{
				FestivalSlotDef def = festival.Slots[slot];
				string optional = def.Optional ? $" (선택, 채우면 온 주민 호감 +{festival.FeastPoints})" : string.Empty;
				detail.Lines.Add($"  {def.Name} {Sim.Given(festival.Id, slot)} / {def.Amount}{optional}");
				int captured = slot;
				AddAction(detail, $"{def.Name} 바치기", Sim.CanOffer(festival.Id, slot), () => Sim.Offer(festival.Id, captured), UiSfxEvent.Purchase);
			}
			RewardDef reward = state.Times == 0 ? festival.FirstReward : festival.Reward;
			detail.Lines.Add($"여는 보상: {HomesteadText.Reward(reward, Catalog)}{(state.Times == 0 ? " (처음)" : string.Empty)}");
			AddAction(detail, "축제 열기", OpenCheck(festival), () => Sim.OpenFestival(festival.Id), UiSfxEvent.LevelUp);
			return detail;
		}

		// 꾸밈 부족은 Locked 로 오지만 그 글 ("다른 길로 얻습니다") 은 맞지 않아 아직으로
		private HomesteadResult OpenCheck(FestivalDef festival)
		{
			HomesteadResult result = Sim.CanOpenFestival(festival.Id);
			return result.Failure == HomesteadFailure.Locked ? HomesteadResult.Fail(HomesteadFailure.NotReady) : result;
		}

		private static string NeedText(FestivalNeed need)
		{
			switch (need.Kind)
			{
				case FestivalNeedKind.TownHall:
					return $"회관 {need.Need}단 (지금 {need.Now}단)";
				case FestivalNeedKind.Residents:
					return $"온 주민 {need.Need}명 (지금 {need.Now}명)";
				default:
					return $"마을 꾸밈 {need.Need}개 (지금 {need.Now}개)";
			}
		}
	}
}
