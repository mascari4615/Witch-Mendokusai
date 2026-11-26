using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[Serializable]
	public class BossPartSlot
	{
		[field: SerializeField, Tooltip("부위 몸. 별도 몬스터 자산 (체력 따로). 부위 일 (PartBodyAffix) 을 끼워야 함")] public Monster Part { get; private set; }
		[field: SerializeField, Tooltip("보스에서 떨어진 자리 (m). x 는 오른쪽, y 는 앞 (월드 축)")] public Vector2 Offset { get; private set; } = new(2f, 0f);
		[field: SerializeField, Tooltip("부위가 맞은 피해 가운데 보스에게도 넘기는 몫. 0 이면 부위 체력만"), Range(0f, 1f)] public float DamageShare { get; private set; } = 0.5f;
	}

	// 보스 하나를 몸 여럿으로. 켜질 때 부위 몬스터를 둘레에 세우고 매 프레임 보스 기준 자리로 붙임. 보스가 쓰러지면 부위도 거둠
	// 부위가 꺾이면 그 부위에 걸린 패턴 (PartGatedMonsterAttack) 이 꺼짐. 시키는 행동: 어느 부위부터 꺾을지 고르기. 정본 memo wm/design/boss-arena-20261126
	[CreateAssetMenu(fileName = "AF_Parts", menuName = "WM/Monster/Affix/Parts")]
	public sealed class PartsAffix : MonsterAffix
	{
		[SerializeField] private List<BossPartSlot> slots = new();

		private static readonly List<MonsterObject> parts = new();

		public override string KindName => "부위";

		public IReadOnlyList<BossPartSlot> Slots => slots;

		public override void OnSpawn(MonsterObject monster, AffixMemory memory)
		{
			foreach (BossPartSlot slot in slots)
			{
				Vector3 position = BossParts.SlotPosition(monster.transform.position, slot.Offset);
				GameObject spawnedObject = ObjectPoolManager.Instance.Spawn(slot.Part.Prefab);
				MonsterObject part = spawnedObject.GetComponent<MonsterObject>();
				if (DungeonManagerBridge.IsDungeon)
					part.SetThreat(DungeonManager.Instance.CurThreat);
				spawnedObject.transform.position = position;
				part.Init(slot.Part);
				spawnedObject.SetActive(true);
				BossParts.Register(monster, part, slot.DamageShare);
			}
		}

		// 부위를 보스 둘레 자리에 붙임. 부위 몸도 쫓아 걸으려 하니 매 프레임 되돌림
		public override void Tick(MonsterObject monster, AffixMemory memory, float deltaTime)
		{
			BossParts.CollectParts(monster, parts);
			foreach (MonsterObject part in parts)
			{
				foreach (BossPartSlot slot in slots)
				{
					if (slot.Part != part.UnitData)
						continue;
					part.UnitMovement.SetMoveDirection(Vector3.zero);
					part.UnitMovement.Reposition(BossParts.SlotPosition(monster.transform.position, slot.Offset));
					break;
				}
			}
		}

		// 보스가 쓰러지면 남은 부위는 드랍 없이 거둠
		public override void OnDeath(MonsterObject monster, AffixMemory memory, Vector3 position)
		{
			BossParts.CollectParts(monster, parts);
			foreach (MonsterObject part in parts)
			{
				BossParts.Forget(part);
				ObjectPoolManager.Instance.Despawn(part.gameObject);
			}
		}

		public override void Validate(Monster owner, List<string> errors)
		{
			if (slots.Count == 0)
				errors.Add($"{name}: 부위가 비어 있음");
			HashSet<Monster> kinds = new();
			foreach (BossPartSlot slot in slots)
			{
				if (slot.Part == null)
				{
					errors.Add($"{name}: 부위 몸 빈 칸");
					continue;
				}
				if (kinds.Add(slot.Part) == false)
					errors.Add($"{name}: 같은 부위 {slot.Part.name} 가 둘. 꺾인 부위를 가를 수 없음");
				if (slot.Part == owner || slot.Part.Type == MonsterType.Boss)
					errors.Add($"{name}: 부위 {slot.Part.name} 가 보스 자신이거나 보스");
				bool hasBody = false;
				foreach (MonsterAffix affix in slot.Part.Affixes)
					hasBody |= affix is PartBodyAffix;
				if (hasBody == false)
					errors.Add($"{name}: 부위 {slot.Part.name} 에 {nameof(PartBodyAffix)} 없음 (피해 넘김, 혼자 남으면 거둠)");
				if (slot.Part.Loots != null && slot.Part.Loots.Count > 0)
					errors.Add($"{name}: 부위 {slot.Part.name} 에 드랍이 있음 (부위 꺾기로 사냥이 됨)");
			}
		}
	}
}
