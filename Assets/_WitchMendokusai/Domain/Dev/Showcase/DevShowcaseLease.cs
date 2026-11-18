using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 진열장이 빌린 풀 몬스터 몸. 두뇌와 자동 시전을 끄고 불사로
	// 몸이 꺼지는 순간 (판 정리, 화면 청소, 걷기) 전부 되돌리고 스스로 빠짐. 풀에 고장 난 몸이 남지 않게
	public sealed class DevShowcaseLease : MonoBehaviour
	{
		private readonly List<UnitBrain> silenced = new();
		private MonsterObject monster;

		// 빌린 칸. 꺼지면 null
		public object Owner { get; private set; }

		// 활성화 뒤에 부름. 두뇌 끄기는 OnEnable 이 코루틴을 띄운 다음이어야 멈춤 (CombatUnitSpawner)
		public static DevShowcaseLease Take(MonsterObject monster, object owner)
		{
			DevShowcaseLease lease = monster.gameObject.AddComponent<DevShowcaseLease>();
			lease.monster = monster;
			lease.Owner = owner;
			// 자동 시전은 UnitObject.Init 이 보존하는 값이라 꺼질 때 명시 복구
			monster.SkillHandler.AutoCastEnabled = false;
			CombatUnitSpawner.SilenceBrains(monster.gameObject, lease.silenced);
			monster.Health.IsImmortal = true;
			return lease;
		}

		private void OnDisable()
		{
			Owner = null;
			CombatUnitSpawner.RestoreBrains(silenced);
			silenced.Clear();
			if (monster != null)
			{
				CombatUnitSpawner.RestoreAutoCast(monster.gameObject);
				monster.Health.IsImmortal = false;
			}
			Destroy(this);
		}
	}
}
