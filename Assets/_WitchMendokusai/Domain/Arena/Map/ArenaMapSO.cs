using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 아레나 맵을 *데이터*로 정의하는 SO 베이스 — 구조·크기·스폰을 SO 가 소유(씬에 하드코딩 X).
	/// 새 맵/구조 = 서브클래싱(RectangleArenaMap / 추후 CircleArenaMap / ObstacleArenaMap / LaneArenaMap 등),
	/// 크기·배치 = 인스펙터 필드(수치 노출 룰). 목표(승패)는 ArenaModeSO 가 직교로 담당.
	/// ArenaMatch 가 Build 로 구조를 생성하고 GetSpawns 로 유닛 배치 위치를 취득.
	/// </summary>
	public abstract class ArenaMapSO : DataSO
	{
		/// <summary> 이 맵이 지원하는 팀 수. </summary>
		public abstract int TeamCount { get; }

		/// <summary> 팀당 스폰(=출전 유닛) 수. </summary>
		public abstract int SpawnsPerTeam { get; }

		/// <summary> 해당 팀의 스폰 위치(맵 로컬 좌표). ArenaMatch 가 여기에 유닛 배치. </summary>
		public abstract IReadOnlyList<Vector3> GetSpawns(int teamId);

		/// <summary> 맵 구조(바닥/경계/장애물)를 root 아래 런타임 생성. 전 수치는 본 SO 데이터에서. </summary>
		public abstract void Build(Transform root);
	}
}
