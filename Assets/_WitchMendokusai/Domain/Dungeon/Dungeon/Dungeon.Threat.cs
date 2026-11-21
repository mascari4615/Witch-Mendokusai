using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 횡단 위협 대응 선택 규칙. 생성과 반납 정책 (th4), 유한 경계 (th2). 정본 memo wm/design/crossing-threat-20261119
	public partial class Dungeon
	{
		[PropertyOrder(122)][field: SerializeField, Tooltip("주기 생성 자리와 화면 밖 적 처리")] public DungeonThreatPolicy ThreatPolicy { get; private set; } = new();

		[PropertyOrder(123)][field: SerializeField, Tooltip("유한 경계. Shape None 이면 무한 지면")] public DungeonArenaOptions Arena { get; private set; } = new();

		public void ValidateThreat()
		{
			List<string> errors = new();
			Arena.Validate(name, errors);
			foreach (MonsterWave wave in MonsterWaves)
				foreach (Monster monster in wave.Monsters)
					if (monster.Attack != null)
						monster.Attack.Validate(monster, errors);
			if (errors.Count > 0)
				throw new System.InvalidOperationException($"{name} 위협 설정 오류: {string.Join(", ", errors)}");
		}
	}
}
