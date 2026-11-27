using UnityEngine;

namespace WitchMendokusai
{
	// 스폰 자리의 높이와 층. 수평 후보는 정책 (Threat, Patterns, 이벤트) 이 고르고, 설 자리는 공급자 (ISpawnSiteSource) 가 답함
	// 예전에는 플레이어 위치에 수평 거리를 더한 자리 그대로라 점프 중이면 공중에 생겼음 (2026-11-27)
	// 설 곳이 없을 때: 주기 생성은 다시 고르고 끝내 없으면 건너뜀. 보스, 1회 웨이브, 사건, 패턴은 대체 자리 (그 자리의 발밑 바닥 높이) 에 경고와 함께 냄
	// 정본 memo wm/reference/monster-spawn-ground-20261127.md
	public partial class MonsterSpawner
	{
		[SerializeField, Tooltip("기본 공급자 (레이) 설정. 판이 SiteSource 를 바꿔 끼우면 안 씀")] private RaycastSpawnSiteSettings spawnSiteSettings = new();
		[SerializeField, Min(1), Tooltip("주기 생성과 재배치에서 설 자리를 못 찾았을 때 후보를 다시 고르는 횟수")] private int spawnSiteAttempts = 4;

		private ISpawnSiteSource siteSource;
		private float lastAnchorFloorHeight;
		private bool hasAnchorFloorHeight;
		// 한 프레임에 여러 마리를 고르니 플레이어 발밑은 프레임마다 한 번만 쏨
		private int anchorFloorFrame = -1;

		// 판이 자기 공급자를 끼우는 자리 (노드 그래프 판 등). null 이면 기본 레이 공급자
		public ISpawnSiteSource SiteSource
		{
			get => siteSource ??= new RaycastSpawnSiteSource(spawnSiteSettings);
			set => siteSource = value;
		}

		// 판마다 앞 판의 발밑 높이를 버림
		private void ResetSpawnSites()
		{
			hasAnchorFloorHeight = false;
			anchorFloorFrame = -1;
		}

		// 기준은 플레이어 발밑 바닥. 이미 바닥 위인 자리 (맵 오브젝트 정예) 는 그 높이를 기준으로 줘서 그 층을 고르게
		private bool TryResolveSpawnSite(Vector3 candidate, out SpawnSite site) => TryResolveSpawnSite(candidate, AnchorFloorHeight(), out site);

		private bool TryResolveSpawnSite(Vector3 candidate, float referenceHeight, out SpawnSite site)
		{
			SpawnSiteQuery query = new()
			{
				Candidate = candidate,
				ReferenceHeight = referenceHeight,
				Band = SpawnSiteBand.NearReference,
				Layer = SpawnLayer.Ground,
			};
			return SiteSource.TryProject(query, out site);
		}

		// 설 곳을 못 찾았을 때의 대체 자리. 플레이어 몸 높이가 아니라 발밑 바닥 높이
		private SpawnSite FallbackSite(Vector3 candidate, float referenceHeight, string reason)
		{
			Debug.LogWarning($"[MonsterSpawner] 설 자리 없음 ({reason}). 대체 자리 {candidate.x:F1}, {referenceHeight:F1}, {candidate.z:F1}");
			Vector3 position = new(candidate.x, referenceHeight, candidate.z);
			return new SpawnSite { Position = position, Ground = position };
		}

		// 플레이어 발밑 바닥 높이. 몸 높이가 아니라서 점프해도 그대로. 구멍 위처럼 바닥이 없으면 마지막으로 찾은 높이
		private float AnchorFloorHeight()
		{
			Vector3 playerPosition = playerProvider.Current.transform.position;
			if (anchorFloorFrame == Time.frameCount)
				return hasAnchorFloorHeight ? lastAnchorFloorHeight : playerPosition.y;
			anchorFloorFrame = Time.frameCount;
			SpawnSiteQuery query = new()
			{
				Candidate = playerPosition,
				ReferenceHeight = playerPosition.y,
				Band = SpawnSiteBand.AtOrBelowReference,
				Layer = SpawnLayer.Ground,
			};
			if (SiteSource.TryProject(query, out SpawnSite floor))
			{
				lastAnchorFloorHeight = floor.Ground.y;
				hasAnchorFloorHeight = true;
			}
			return hasAnchorFloorHeight ? lastAnchorFloorHeight : playerPosition.y;
		}

		// 화면 밖 판정과 거리 계산의 원점. 플레이어 수평 위치에 발밑 바닥 높이
		private Vector3 AnchorPosition()
		{
			Vector3 position = playerProvider.Current.transform.position;
			position.y = AnchorFloorHeight();
			return position;
		}

		private bool TryPickPeriodicSite(out SpawnSite site)
		{
			for (int attempt = 0; attempt < spawnSiteAttempts; attempt++)
				if (TryResolveSpawnSite(PickPeriodicSpawnPosition(), out site))
					return true;
			site = default;
			return false;
		}

		private bool TryPickRelocationSite(out SpawnSite site)
		{
			for (int attempt = 0; attempt < spawnSiteAttempts; attempt++)
				if (TryResolveSpawnSite(PickRelocation(), out site))
					return true;
			site = default;
			return false;
		}
	}
}
