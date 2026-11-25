using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Discovery;

namespace WitchMendokusai
{
	/// <summary>몬스터 기록의 단계 목표. 화면 설정과 나눔: 이건 판정이라 Root 가 읽고, 화면 설정은 도감 화면 프리팹이 듦</summary>
	[CreateAssetMenu(fileName = "SET_0_MonsterLog", menuName = "WM/Variable/" + nameof(MonsterLogRules))]
	public class MonsterLogRules : ScriptableObject
	{
		[SerializeField, Tooltip("일반 몬스터. 칸마다 필요한 누적 처치 수. 칸 순서는 첫 기록, 사는 곳, 몸과 버릇, 전리품, 이야기. 임시 값")] private int[] normalTargets = { 1, 20, 100, 300, 1000 };
		[SerializeField, Tooltip("보스. 임시 값")] private int[] bossTargets = { 1, 2, 4, 7, 10 };

		public IReadOnlyList<int> NormalTargets => normalTargets;
		public IReadOnlyList<int> BossTargets => bossTargets;

		/// <summary>목표 표가 칸 수와 맞고 오름차순인지. 틀리면 던짐</summary>
		public void ValidateTargets()
		{
			ValidateTargets(nameof(normalTargets), normalTargets);
			ValidateTargets(nameof(bossTargets), bossTargets);
		}

		private void ValidateTargets(string label, int[] targets)
		{
			DiscoveryStages.Validate($"{name} {label}", targets);
			if (targets.Length != MonsterLogPage.SLOT_COUNT)
			{
				throw new ArgumentException($"{name} {label}: 칸은 {MonsterLogPage.SLOT_COUNT}개, 목표는 {targets.Length}개");
			}
		}
	}
}
