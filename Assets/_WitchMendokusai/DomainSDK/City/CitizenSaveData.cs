using System;
using UnityEngine;

namespace WitchMendokusai
{
	// 시민 한 명의 영속 데이터 — 집/직장 셀 + 현재 상태. [Serializable] struct + 공개 필드(BuildingInstanceData
	// 동형 = 검증된 직렬화 경로). 비전-중립: 시민 정체(사역마/언데드/사람)는 스킨 deferred, 데이터는 셀+상태만.
	// 이동 런타임(현재 위치·경로 인덱스)은 RuntimeCitizen(INC-7) — first-use 전 선제 추가 X.
	[Serializable]
	public struct CitizenSaveData
	{
		public Vector3Int HomeCell;
		public Vector3Int WorkCell;
		public CitizenState State;

		public CitizenSaveData(Vector3Int homeCell, Vector3Int workCell, CitizenState state)
		{
			HomeCell = homeCell;
			WorkCell = workCell;
			State = state;
		}
	}
}
