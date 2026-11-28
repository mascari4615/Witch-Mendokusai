using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 별 보는 언덕
	// - 밤 (StarTuning 시각) 에만, 하룻밤에 한 번 (쉬기와 같은 결)
	// - 밤은 자정을 넘김. 하늘 달력의 날은 자정에 바뀌므로 그 밤이 시작된 날 (NightOf) 로 셈
	// - 오늘 밤 별자리는 계절의 별자리 중 밤의 날로 하나. 이으면 별 조각 (처음이면 더) 과 오늘의 점
	// - 점은 다음 던전 한 판. 식탁 음식과 따로 걸림. 흉조 없음. 안 쓰고 또 보면 새 점으로 바뀜
	public sealed partial class HomesteadSim
	{
		private StarTuning StarTuning => Catalog.StarTuning;

		private bool IsNightWrapped => StarTuning.FromHour > StarTuning.ToHour;

		public bool IsStarHour(int hour)
		{
			int from = StarTuning.FromHour;
			int to = StarTuning.ToHour;
			return IsNightWrapped ? hour >= from || hour < to : hour >= from && hour < to;
		}

		// 그 시각이 속한 밤이 시작된 하늘 달력의 날. 자정 뒤 새벽은 전날 밤
		public long NightOf(long calendarDay, int hour) => IsNightWrapped && hour < StarTuning.ToHour ? calendarDay - 1 : calendarDay;

		// 그 계절에 뜨는 별자리 중 밤의 날로 하나. 계절에 하나도 없으면 전체에서
		public ConstellationDef TonightConstellation(int season, long night)
		{
			List<ConstellationDef> sky = Catalog.Constellations.FindAll(constellation => constellation.Season == season);
			if (sky.Count == 0)
			{
				sky = Catalog.Constellations;
			}
			int index = (int)(((night % sky.Count) + sky.Count) % sky.Count);
			return sky[index];
		}

		public bool IsConstellationFound(string constellationId) => State.Constellations.Contains(constellationId);

		public bool HasStargazed(int hour, long calendarDay) => State.StarGazedOn == NightOf(calendarDay, hour);

		public HomesteadResult CanStargaze(int hour, long calendarDay)
		{
			if (HasStargazed(hour, calendarDay))
			{
				return HomesteadResult.Fail(HomesteadFailure.AlreadyDone);
			}
			return IsStarHour(hour) ? HomesteadResult.Success : HomesteadResult.Fail(HomesteadFailure.NotReady);
		}

		public StarLink StartStarLink(int season, int hour, long calendarDay) => new StarLink(TonightConstellation(season, NightOf(calendarDay, hour)));

		public int ShardsFor(string constellationId) => IsConstellationFound(constellationId) ? StarTuning.RepeatShards : StarTuning.FirstShards;

		// 다 이은 뒤 부름. 시각과 하룻밤 한 번은 여기서 다시 봄 (잇는 사이 아침이 되면 못 받고 오늘 밤 다시)
		public HomesteadResult FinishStargaze(StarLink link, int hour, long calendarDay)
		{
			if (link.IsDone == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotReady);
			}
			HomesteadResult result = CanStargaze(hour, calendarDay);
			if (result.Ok == false)
			{
				return result;
			}
			string id = link.Constellation.Id;
			Give(StarTuning.Shard, ShardsFor(id));
			if (IsConstellationFound(id) == false)
			{
				State.Constellations.Add(id);
			}
			State.Omen = id;
			State.StarGazedOn = NightOf(calendarDay, hour);
			NotifyChanged();
			return HomesteadResult.Success;
		}

		public bool HasOmen => State.Omen.Length > 0;

		public ConstellationDef OmenConstellation => HasOmen ? Catalog.Get<ConstellationDef>(State.Omen) : null;

		// 던전 시작에 부름. 받은 점의 별자리, 없으면 null
		public ConstellationDef TakeOmen()
		{
			if (HasOmen == false)
			{
				return null;
			}
			ConstellationDef omen = OmenConstellation;
			State.Omen = string.Empty;
			NotifyChanged();
			return omen;
		}
	}
}
