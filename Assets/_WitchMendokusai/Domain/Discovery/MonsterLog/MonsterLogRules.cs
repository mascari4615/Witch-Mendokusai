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
		[SerializeField, Tooltip("일반 몬스터. 칸마다 필요한 누적 처치 수. 칸 순서는 첫 기록, 사는 곳, 몸과 버릇, 전리품, 이야기")] private int[] normalTargets = { 1, 50, 300, 1000, 2000 };
		[SerializeField, Tooltip("드문 몬스터. 한 판에 적게 나와 같은 목표로는 너무 오래 걸리는 종")] private int[] rareTargets = { 1, 10, 50, 150, 300 };
		[SerializeField, Tooltip("드문 몬스터의 ID. 자산을 물면 부팅 때 딸려 오므로 번호로")] private int[] rareMonsterIds = { };
		[SerializeField, Tooltip("보스. 임시 값")] private int[] bossTargets = { 1, 2, 4, 7, 10 };

		/// <summary>이 종의 단계 목표. 이야기 글이 없는 종은 마지막 칸 없이</summary>
		public IReadOnlyList<int> TargetsOf(Monster monster, bool hasStory)
		{
			int[] targets = monster.Type == MonsterType.Boss ? bossTargets : IsRare(monster.ID) ? rareTargets : normalTargets;
			return hasStory ? targets : new ArraySegment<int>(targets, 0, MonsterLogPage.SLOT_STORY);
		}

		public bool IsRare(int monsterId) => Array.IndexOf(rareMonsterIds, monsterId) >= 0;

		public IReadOnlyList<int> RareMonsterIds => rareMonsterIds;

		/// <summary>목표 표가 칸 수와 맞고 오름차순인지. 틀리면 던짐</summary>
		public void ValidateTargets()
		{
			ValidateTargets(nameof(normalTargets), normalTargets);
			ValidateTargets(nameof(rareTargets), rareTargets);
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
