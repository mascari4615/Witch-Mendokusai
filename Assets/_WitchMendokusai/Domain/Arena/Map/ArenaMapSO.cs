using System.Collections.Generic;
using UnityEngine;
// ★ 스폰 자리는 판정 쪽 (TASK-WM-214) — 규칙(SpawnRules)이 읽는 값이다.
using Vector3 = WitchMendokusai.Numerics.Vector3;

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

		/// <summary>
		/// Build 로 생성한 기하 정리(재매치 누수 방지). 기본 = root 자식 전부 파괴
		/// (Build 컨벤션 = 전부 root 아래 생성). 다른 정리가 필요한 맵은 override.
		/// </summary>
		public virtual void Teardown(Transform root)
		{
			if (root == null)
				return;
			for (int childIndex = root.childCount - 1; childIndex >= 0; childIndex--)
				Object.Destroy(root.GetChild(childIndex).gameObject);
		}
	}
}
