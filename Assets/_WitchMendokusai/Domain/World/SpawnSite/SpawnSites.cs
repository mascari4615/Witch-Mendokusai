using UnityEngine;

namespace WitchMendokusai
{
	// 지금 판의 스폰 자리 공급자 한 곳. 물결 (MonsterSpawner) 밖의 스폰과 바닥 질의 (소환, 분열, 와드, 상자, 채집 노드, 맵 오브젝트, 궁극기 효과) 도 여기로
	// 판이 Register 로 자기 공급자를 끼움 (MonsterSpawner 는 물결 시작 때). 없으면 기본 설정의 레이 공급자
	public static class SpawnSites
	{
		private static ISpawnSiteSource active;
		private static ISpawnSiteSource fallback;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			active = null;
			fallback = null;
		}

		public static ISpawnSiteSource Active => active ?? (fallback ??= new RaycastSpawnSiteSource(new RaycastSpawnSiteSettings()));

		public static void Register(ISpawnSiteSource source) => active = source;

		public static void Unregister(ISpawnSiteSource source)
		{
			if (active == source)
				active = null;
		}

		// 몸 (body) 의 발밑 바닥. 몸 높이 이하에서 가장 높은 면, 점프 중이어도 위 다리를 안 잡음
		public static bool TryFloorBelow(Vector3 body, out SpawnSite floor) =>
			Active.TryProject(new SpawnSiteQuery { Candidate = body, ReferenceHeight = body.y, Band = SpawnSiteBand.AtOrBelowReference, Layer = SpawnLayer.Ground }, out floor);

		// 높이 referenceHeight 에 가까운 층의 자리. 이미 바닥 높이를 아는 후보 (맵 오브젝트, 효과 착지점)
		public static bool TryNearHeight(Vector3 candidate, float referenceHeight, out SpawnSite site) =>
			Active.TryProject(new SpawnSiteQuery { Candidate = candidate, ReferenceHeight = referenceHeight, Band = SpawnSiteBand.NearReference, Layer = SpawnLayer.Ground }, out site);

		// 기준 몸 (anchorBody, 소환하는 몬스터나 플레이어) 과 같은 층의 자리. 기준의 발밑 바닥 높이를 먼저 구함
		public static bool TryNearBody(Vector3 candidate, Vector3 anchorBody, out SpawnSite site)
		{
			float referenceHeight = TryFloorBelow(anchorBody, out SpawnSite floor) ? floor.Ground.y : anchorBody.y;
			return TryNearHeight(candidate, referenceHeight, out site);
		}
	}
}
