using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 던전 선택 규칙. 전부 기본 꺼짐
	[Serializable]
	public class DungeonWaveOptions
	{
		[field: Header("최종 보스 클리어")]
		[field: SerializeField, Tooltip("최종 보스 처치로 클리어. 원래 목표와 병행")] public bool ClearOnFinalBossKill { get; private set; }
		[field: SerializeField, Tooltip("비우면 시작 시각이 가장 늦은 보스 웨이브의 보스")] public Monster FinalBoss { get; private set; }
		[field: SerializeField, Tooltip("최종 보스 등장 뒤 주기 웨이브 생성률 배율. 0 이면 멈춤"), Range(0f, 1f)] public float AfterFinalBossPeriodicRate { get; private set; } = 0.1f;

		[field: Header("경험치 요구")]
		[field: SerializeField, Tooltip("위협 단계 물량 배율에 맞춰 레벨업 경험치 요구를 올림")] public bool ScaleExpWithSpawnRate { get; private set; }
		[field: SerializeField, Tooltip("1 이면 물량과 같은 비율, 0.5 면 절반만 따라감"), Min(0f)] public float ExpFollow { get; private set; } = 1f;

		[field: Header("보스 등장 잡몹 정리")]
		[field: SerializeField, Tooltip("보스가 나오면 잡몹을 드롭 없이 정리. 정예와 보스는 남김")] public bool ClearMobsOnBossSpawn { get; private set; }
		[field: SerializeField, Tooltip("보스 등장 뒤 정리까지 초"), Min(0f)] public float ClearMobsDelay { get; private set; } = 0.5f;

		public float ExpRequirementMultiplier(float spawnRate) => ScaleExpWithSpawnRate ? WavePatternRules.ExpRequirementMultiplier(spawnRate, ExpFollow) : 1f;

		// 최종 보스 판정 대상. 지정이 없으면 웨이브에서 찾음, 못 찾거나 웨이브에 없으면 설정 오류
		// 같은 보스 자산이 중간 보스로도 나오면 첫 등장과 처치에 반응하니 최종 보스 전용 자산을 씀
		public Monster ResolveFinalBoss(IReadOnlyList<MonsterWave> waves)
		{
			if (FinalBoss != null)
			{
				foreach (MonsterWave wave in waves)
					if (Array.IndexOf(wave.Monsters, FinalBoss) >= 0)
						return FinalBoss;
				throw new InvalidOperationException($"최종 보스 {FinalBoss.name} 가 웨이브에 없음");
			}
			Monster latestBoss = null;
			float latestStart = float.MinValue;
			foreach (MonsterWave wave in waves)
			{
				Monster boss = Array.Find(wave.Monsters, monster => monster.Type == MonsterType.Boss);
				if (boss == null || wave.StartTime < latestStart)
					continue;
				latestBoss = boss;
				latestStart = wave.StartTime;
			}
			if (latestBoss == null)
				throw new InvalidOperationException("최종 보스 클리어가 켜졌는데 보스 웨이브가 없음. FinalBoss 를 지정하거나 보스 웨이브 추가");
			return latestBoss;
		}
	}
}
