using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 웨이브 패턴 일정표와 던전 선택 규칙
	public partial class Dungeon
	{
		[PropertyOrder(120)][field: SerializeField, Tooltip("웨이브 패턴 일정표. 비우면 MonsterWaves 만")] public List<WavePatternSlot> WavePatterns { get; private set; } = new();

		[PropertyOrder(121)][field: SerializeField] public DungeonWaveOptions WaveOptions { get; private set; } = new();

		// 판 시작 전 일정표와 선택 규칙 검사. 설정 오류는 입장 전에 예외
		public void ValidateWaves()
		{
			foreach (WavePatternSlot slot in WavePatterns)
			{
				if (slot.Pattern == null)
					throw new System.InvalidOperationException($"{name}: 웨이브 패턴 일정표에 빈 칸");
				slot.Pattern.Validate();
			}
			if (WaveOptions.ClearOnFinalBossKill)
				WaveOptions.ResolveFinalBoss(MonsterWaves);
			ValidateThreat();
		}
	}
}
