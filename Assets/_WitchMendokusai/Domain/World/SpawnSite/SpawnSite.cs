using UnityEngine;

namespace WitchMendokusai
{
	// 스폰 자리 공급의 계약. 정본 memo wm/reference/monster-spawn-ground-20261127.md
	// 부르는 쪽 (MonsterSpawner) 은 수평 후보만, 높이와 층은 판이 끼운 공급자가
	// 지금은 레이 공급자 하나 (평지, 언덕). 여러 층과 도달성이 필요한 판은 노드 그래프 공급자로 교체
	public enum SpawnLayer
	{
		Ground = 0,
		// 바닥 위로 띄움. 공중 이동이 아직 없어서 부르는 곳은 없음
		Air = 1,
	}

	// 여러 면이 맞을 때 어느 면을 고르나
	public enum SpawnSiteBand
	{
		// 기준 높이에 가장 가까운 면. 스폰 후보 (다리 위와 아래 중 플레이어 층)
		NearReference = 0,
		// 기준 높이 이하에서 가장 높은 면. 플레이어 발밑 바닥 (점프 중이어도 위 다리를 안 잡음)
		AtOrBelowReference = 1,
	}

	public struct SpawnSiteQuery
	{
		// x, z 만 씀. y 는 무시
		public Vector3 Candidate;
		public float ReferenceHeight;
		public SpawnSiteBand Band;
		public SpawnLayer Layer;
	}

	public struct SpawnSite
	{
		// 몬스터를 놓을 자리. 공중 층이면 바닥 위
		public Vector3 Position;
		// 그 아래 바닥. 스폰 원은 여기에
		public Vector3 Ground;
	}

	public interface ISpawnSiteSource
	{
		// 후보를 설 수 있는 자리로 옮김. 그 근처에 설 곳이 없으면 false. 부르는 쪽이 버리거나 다시 고르거나 대체 자리를 씀
		bool TryProject(in SpawnSiteQuery query, out SpawnSite site);
	}
}
