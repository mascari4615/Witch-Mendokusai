using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 몬스터에 얹는 특징 하나 (자국 남김, 죽으면 터짐, 분열, 치유 파동, 분노, 끌어당김). 몸이나 성격과 따로 갈아 끼움
	// 몬스터 자산 (Monster.Affixes) 에 여럿 끼울 수 있음. 여러 유닛이 나눠 쓰므로 자산에는 상태를 두지 않음. 유닛마다의 기억은 AffixMemory
	// 때: 켜질 때, 매 프레임, 맞을 때, 쓰러질 때. 돌리는 것은 MonsterAffixDriver. 정본 memo wm/design/monster-affix-20261126
	public abstract class MonsterAffix : ScriptableObject
	{
		/// <summary>도감과 개발 창에 보이는 특징 이름 (임시 표기)</summary>
		public abstract string KindName { get; }

		[field: SerializeField, Tooltip("몸 색에 곱함. 특징이 몸에서 보이게. 흰색이면 그대로")] public Color Tint { get; private set; } = Color.white;

		public virtual void OnSpawn(MonsterObject monster, AffixMemory memory) { }

		public virtual void Tick(MonsterObject monster, AffixMemory memory, float deltaTime) { }

		public virtual void OnHurt(MonsterObject monster, AffixMemory memory, DamageInfo damageInfo) { }

		// 쓰러진 자리. 몸은 곧 꺼지므로 늦게 일어날 일은 몸에 기대지 않음
		public virtual void OnDeath(MonsterObject monster, AffixMemory memory, Vector3 position) { }

		public virtual void Validate(Monster owner, List<string> errors) { }

		protected static PlayerObject CurrentPlayer => PlayerProvider.Instance != null ? PlayerProvider.Instance.CurrentObject : null;
	}
}
