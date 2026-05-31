using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 한 아레나 매치의 데이터 묶음 — 맵(구조/크기) + 모드(목표) + 로스터(누가 어느 팀, 어떤 전술).
	/// 맵 ⊥ 모드 ⊥ 로스터 3축 전부 데이터 커스텀. ArenaMatch(Mono)가 본 SO 로 매치를 셋업.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(ArenaMatchConfig), menuName = "WM/Arena/ArenaMatchConfig")]
	public class ArenaMatchConfig : DataSO
	{
		[field: Header("_" + nameof(ArenaMatchConfig))]
		[field: Tooltip("맵 구조/크기 (ArenaMapSO — 직사각/원형/레인 등).")]
		[field: SerializeField] public ArenaMapSO Map { get; private set; }

		[field: Tooltip("승패 규칙/목표 (ArenaModeSO — 전멸/점령/넥서스 등).")]
		[field: SerializeField] public ArenaModeSO Mode { get; private set; }

		[field: Tooltip("매치 제한시간(초). 초과 시 최다 생존 팀 승(동률 무승부) — 교착 무한 방지. 0 이하 = 무제한.")]
		[field: SerializeField, Min(0f)] public float TimeLimitSeconds { get; private set; } = 60f;

		[field: Tooltip("출전 로스터 — 각 엔트리 = 유닛 데이터 + 전술 + 팀.")]
		[field: SerializeField] public List<ArenaUnitEntry> Roster { get; private set; } = new();

		/// <summary> 로스터 한 칸 — 어떤 유닛(Unit SO)을 어느 팀에서 어떤 전술로 출전시킬지. </summary>
		[Serializable]
		public class ArenaUnitEntry
		{
			[field: Tooltip("출전 유닛 (Doll/Monster 등 Unit SO — Prefab/스탯 보유).")]
			[field: SerializeField] public Unit UnitData { get; private set; }

			[field: Tooltip("이 유닛의 전술 프로그램(우선순위 룰 리스트).")]
			[field: SerializeField] public TacticProgram Tactic { get; private set; }

			[field: Tooltip("소속 팀 (0 = 욘 팀, 1 = 라이벌 팀).")]
			[field: SerializeField] public int TeamId { get; private set; }
		}
	}
}
