using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 오늘의 점이 다음 던전 한 판에 거는 것. 식탁 음식 (MealBuff) 과 겹치지 않는 셋. 흉조 없음
	public enum OmenBuff
	{
		Pickup = 0,
		Gold = 1,
		Crit = 2,
	}

	// 별 보기 수치. stars.json 의 StarTuning 하나 (다른 생활 갈래의 Tuning 과 따로 두어 파일끼리 안 부딪침)
	// - 밤은 FromHour 부터 ToHour 전까지. From 이 To 보다 크면 자정을 넘김 (20시부터 다음날 4시 전)
	public sealed class StarTuning
	{
		public int FromHour { get; set; } = 20;
		public int ToHour { get; set; } = 4;
		public string Shard { get; set; } = "star_shard";
		public int FirstShards { get; set; } = 2;
		public int RepeatShards { get; set; } = 1;
		// 판 좌표 (0~1) 에서 별끼리 가장 가까운 거리. 손가락으로 눌러 갈라야 함
		// 별 동그라미는 판의 10% (Homestead.uss 의 wm-hs-star__dot) 라 그보다 커야 안 겹침
		public float MinStarGap { get; set; } = 0.11f;
		// 마을 칸 좌표. 별 보는 언덕 자리
		public float SpotX { get; set; }
		public float SpotZ { get; set; }
	}

	// 별자리 하나. 별 자리는 0~1 판 좌표 (왼쪽 아래가 0), 선은 별 번호 둘
	// - Season 의 밤에만 하늘에 뜸. 오늘 밤 하나는 날짜로 정함 (HomesteadSim.TonightConstellation)
	// - Omen 과 Value 는 처음이든 다시든 같음. 점은 다음 던전 한 판
	public sealed class ConstellationDef : HomesteadDef
	{
		public int Season { get; set; }
		public List<float[]> Stars { get; set; } = new List<float[]>();
		public List<int[]> Lines { get; set; } = new List<int[]>();
		public OmenBuff Omen { get; set; }
		public int Value { get; set; }
		// 점 글. 좋은 말만
		public string Saying { get; set; } = string.Empty;
	}

	// 별 잇기 한 판. 엔진 밖 순수 상태
	// - 별을 누르면 고른 별이 됨. 고른 별과 선으로 이어진 별을 누르면 그 선이 그어지고 그 별이 새로 고른 별
	// - 선이 아닌 별을 누르면 고르기만 바뀜. 틀림, 벌, 시간 제한 없음
	public sealed class StarLink
	{
		public ConstellationDef Constellation { get; }
		public int Selected { get; private set; } = -1;
		public bool IsDone => drawn.Count >= Constellation.Lines.Count;
		public int DrawnCount => drawn.Count;

		private readonly HashSet<int> drawn = new HashSet<int>();

		public StarLink(ConstellationDef constellation) => Constellation = constellation;

		public bool IsDrawn(int line) => drawn.Contains(line);

		// 그은 선 번호, 아니면 -1
		public int Press(int star)
		{
			if (star < 0 || star >= Constellation.Stars.Count || IsDone)
			{
				return -1;
			}
			int line = Selected >= 0 ? LineBetween(Selected, star) : -1;
			Selected = star;
			if (line < 0 || drawn.Add(line) == false)
			{
				return -1;
			}
			return line;
		}

		private int LineBetween(int a, int b)
		{
			for (int i = 0; i < Constellation.Lines.Count; i++)
			{
				int[] line = Constellation.Lines[i];
				if ((line[0] == a && line[1] == b) || (line[0] == b && line[1] == a))
				{
					return i;
				}
			}
			return -1;
		}
	}

	public static class StarSkyCheck
	{
		public const int SEASONS = 4;

		public static void Check(HomesteadCatalog catalog, List<string> problems)
		{
			StarTuning tuning = catalog.StarTuning;
			if (tuning.FromHour < 0 || tuning.FromHour > 23 || tuning.ToHour < 0 || tuning.ToHour > 23 || tuning.FromHour == tuning.ToHour)
			{
				problems.Add($"별 보기 시각 {tuning.FromHour}~{tuning.ToHour} 이 0~23 안의 다른 두 시각이 아님");
			}
			if (catalog.TryGet(tuning.Shard, out GoodsDef _) == false)
			{
				problems.Add($"별 보기 보상 '{tuning.Shard}' 이 물건에 없음");
			}
			if (tuning.FirstShards < tuning.RepeatShards || tuning.RepeatShards < 1)
			{
				problems.Add($"별 조각 처음 {tuning.FirstShards}, 다시 {tuning.RepeatShards}. 다시가 1 이상이고 처음이 더 많거나 같아야 함");
			}
			int[] perSeason = new int[SEASONS];
			foreach (ConstellationDef constellation in catalog.Constellations)
			{
				CheckOne(constellation, tuning.MinStarGap, problems);
				if (constellation.Season >= 0 && constellation.Season < SEASONS)
				{
					perSeason[constellation.Season]++;
				}
			}
			for (int season = 0; season < SEASONS && catalog.Constellations.Count > 0; season++)
			{
				if (perSeason[season] == 0)
				{
					problems.Add($"계절 {season} 의 밤에 뜨는 별자리가 없음");
				}
			}
		}

		private static void CheckOne(ConstellationDef constellation, float minGap, List<string> problems)
		{
			string id = constellation.Id;
			for (int i = 0; i < constellation.Stars.Count; i++)
			{
				for (int j = i + 1; j < constellation.Stars.Count; j++)
				{
					float dx = constellation.Stars[i][0] - constellation.Stars[j][0];
					float dy = constellation.Stars[i][1] - constellation.Stars[j][1];
					if (dx * dx + dy * dy < minGap * minGap)
					{
						problems.Add($"{id}: 별 {i} 과 {j} 가 {minGap} 보다 가까움");
					}
				}
			}
			if (constellation.Season < 0 || constellation.Season >= SEASONS)
			{
				problems.Add($"{id}: 계절 {constellation.Season}");
			}
			if (constellation.Stars.Count < 3 || constellation.Lines.Count < 2)
			{
				problems.Add($"{id}: 별 {constellation.Stars.Count}, 선 {constellation.Lines.Count}. 별 셋 선 둘 이상");
			}
			if (constellation.Value <= 0)
			{
				problems.Add($"{id}: 점 값 {constellation.Value}");
			}
			foreach (float[] star in constellation.Stars)
			{
				if (star.Length != 2 || star[0] < 0f || star[0] > 1f || star[1] < 0f || star[1] > 1f)
				{
					problems.Add($"{id}: 별 자리가 0~1 판 밖이거나 두 수가 아님");
				}
			}
			HashSet<int> touched = new HashSet<int>();
			foreach (int[] line in constellation.Lines)
			{
				if (line.Length != 2 || line[0] == line[1] || line[0] < 0 || line[1] < 0 || line[0] >= constellation.Stars.Count || line[1] >= constellation.Stars.Count)
				{
					problems.Add($"{id}: 없는 별을 잇는 선");
					continue;
				}
				touched.Add(line[0]);
				touched.Add(line[1]);
			}
			if (touched.Count != constellation.Stars.Count)
			{
				problems.Add($"{id}: 선에 안 닿는 별이 있음");
			}
		}
	}
}
