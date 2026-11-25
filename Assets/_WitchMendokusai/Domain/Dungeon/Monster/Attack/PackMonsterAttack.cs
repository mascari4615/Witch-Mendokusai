using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 무리면 공격, 혼자면 공격하지 않고 주인공에게서 거리를 두고 떠돎 (길 안내하는 척)
	// 같은 종의 살아 있는 개체를 세어 둘을 오감. 박자와 예고는 무리 공격의 값, 이 자산의 칸은 안 씀
	[CreateAssetMenu(fileName = "MA_Pack", menuName = "WM/Variable/MonsterAttack/" + nameof(PackMonsterAttack))]
	public class PackMonsterAttack : MonsterAttack
	{
		public override string KindName => "무리";

		[field: Header("무리")]
		[field: SerializeField, Tooltip("무리일 때 쓰는 공격")] public MonsterAttack GroupAttack { get; private set; }
		[field: SerializeField, Tooltip("나를 포함해 이만큼 모이면 무리"), Min(2)] public int PackSize { get; private set; } = 3;
		[field: SerializeField, Tooltip("무리로 세는 거리 (m)"), Min(0.5f)] public float PackRadius { get; private set; } = 5f;
		[field: SerializeField, Tooltip("무리를 다시 세는 간격 (초)"), Min(0.1f)] public float CountInterval { get; private set; } = 0.5f;

		[field: Header("혼자")]
		[field: SerializeField, Tooltip("주인공과의 거리 (m)"), Min(0.5f)] public float LureDistance { get; private set; } = 4.5f;
		[field: SerializeField, Tooltip("거리가 이만큼 어긋나면 다가가거나 물러남 (m)"), Min(0f)] public float LureBand { get; private set; } = 0.8f;
		[field: SerializeField, Tooltip("혼자일 때 사거리 (TriggerRange) 안에 들어오면 쓰는 공격. 비우면 안 침")] public MonsterAttack SoloAttack { get; private set; }

		public override BTRunner CreateBehaviour(UnitObject unitObject, PlayerProvider playerProvider) =>
			new BT_PackAttack(unitObject, playerProvider, this);

		public override void Interrupt(BTRunner behaviour) => ((BT_PackAttack)behaviour).Interrupt();

		public bool IsGrouped(UnitObject unitObject)
		{
			float radiusSquared = PackRadius * PackRadius;
			Vector3 center = unitObject.transform.position;
			int count = 0;
			foreach (GameObject candidate in ObjectBufferManager.GetObjects(ObjectType.Monster))
			{
				if (candidate == null || candidate.activeInHierarchy == false || (candidate.transform.position - center).sqrMagnitude > radiusSquared)
					continue;
				if (candidate.TryGetComponent(out MonsterObject monster) && monster.UnitData.ID == unitObject.UnitData.ID && monster.IsAlive)
					count++;
			}
			return count >= PackSize;
		}

		// 혼자일 때 갈 방향. 멀면 다가가고 가까우면 물러나고 알맞으면 둘레를 돎
		public static Vector3 LureDirection(Vector3 self, Vector3 player, float distance, float band)
		{
			Vector3 fromPlayer = self - player;
			fromPlayer.y = 0f;
			if (fromPlayer.sqrMagnitude < 0.0001f)
				return Vector3.right;
			float current = fromPlayer.magnitude;
			Vector3 outward = fromPlayer / current;
			if (current > distance + band)
				return -outward;
			if (current < distance - band)
				return outward;
			return Vector3.Cross(Vector3.up, outward);
		}

		public override void Validate(Monster owner, List<string> errors)
		{
			base.Validate(owner, errors);
			if (GroupAttack == null)
				errors.Add($"{name}: 무리 공격이 비어 있음");
			else if (GroupAttack is PackMonsterAttack)
				errors.Add($"{name}: 무리 공격에 무리를 다시 넣을 수 없음");
			else
				GroupAttack.Validate(owner, errors);
			if (SoloAttack is PackMonsterAttack)
				errors.Add($"{name}: 혼자 공격에 무리를 다시 넣을 수 없음");
			else if (SoloAttack != null)
				SoloAttack.Validate(owner, errors);
		}
	}
}
