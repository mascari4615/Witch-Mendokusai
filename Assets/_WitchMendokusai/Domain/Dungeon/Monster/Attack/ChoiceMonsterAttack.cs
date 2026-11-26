using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[Serializable]
	public class ChoiceBranch
	{
		[field: SerializeField, Tooltip("이 갈래가 하는 공격 (낱개나 순서)")] public MonsterAttack Attack { get; private set; }
		[field: SerializeField, Tooltip("고를 확률의 무게. 다른 갈래와 비율로"), Min(0.01f)] public float Weight { get; private set; } = 1f;

		[field: Header("단서")]
		[field: SerializeField, Tooltip("몸 둘레에 돋는 물체. 갈래마다 다른 물체나 다른 개수")] public CombatFx CueFx { get; private set; }
		[field: SerializeField, Tooltip("물체 개수"), Min(1)] public int CueCount { get; private set; } = 1;
		[field: SerializeField, Tooltip("단서 동안 몸 색 곱. 흰색이면 안 바꿈")] public Color BodyTint { get; private set; } = Color.white;

		// 단서가 다른 갈래와 겹치는가 (같은 물체, 같은 개수, 같은 색)
		public bool LooksLike(ChoiceBranch other) => CueFx == other.CueFx && CueCount == other.CueCount && BodyTint == other.BodyTint;
	}

	// 갈래 선택. 같은 준비 몸짓 (Pose) 에서 갈래 하나를 무작위로 고르고, 그 갈래의 단서 (물체, 개수, 몸 색) 를 CueSeconds 동안 먼저 보인 뒤 갈래 공격을 돌림
	// 단서는 갈래 공격의 예고가 끝날 때까지 남음. 정답 행동은 단서를 읽어야 갈림. 정본 memo wm/design/boss-choice-20261126
	[CreateAssetMenu(fileName = "MA_Choice", menuName = "WM/Variable/MonsterAttack/" + nameof(ChoiceMonsterAttack))]
	public class ChoiceMonsterAttack : MonsterAttack
	{
		public override string KindName => "갈래 선택";

		[field: Header("갈래")]
		[field: SerializeField, Tooltip("둘 이상. 갈래 안에 갈래, 무리, 단계는 안 넣음")] public List<ChoiceBranch> Branches { get; private set; } = new();
		[field: SerializeField, Tooltip("단서만 보이고 멈춰 있는 초. 이 뒤에 갈래 공격의 예고가 시작됨"), Min(0.1f)] public float CueSeconds { get; private set; } = 0.8f;
		[field: SerializeField, Tooltip("단서 물체가 몸에서 떨어진 거리 (m)"), Min(0f)] public float CueDistance { get; private set; } = 1.1f;
		[field: SerializeField, Tooltip("물체가 여럿일 때 벌어지는 각도 (도). 화면 위쪽을 가운데로"), Range(0f, 360f)] public float CueSpreadDegrees { get; private set; } = 70f;
		[field: SerializeField, Tooltip("단서 물체 크기 (m)"), Min(0.1f)] public float CueRadius { get; private set; } = 0.6f;

		public override BTRunner CreateBehaviour(UnitObject unitObject, PlayerProvider playerProvider) =>
			new BT_ChoiceAttack(unitObject, playerProvider, this);

		public override void Interrupt(BTRunner behaviour) => ((BT_ChoiceAttack)behaviour).Interrupt();

		// random01 (0 이상 1 미만) 으로 무게대로 고른 갈래 차례
		public int PickBranch(float random01)
		{
			float total = 0f;
			foreach (ChoiceBranch branch in Branches)
				total += branch.Weight;
			float pick = Mathf.Clamp01(random01) * total;
			for (int i = 0; i < Branches.Count; i++)
			{
				pick -= Branches[i].Weight;
				if (pick < 0f)
					return i;
			}
			return Branches.Count - 1;
		}

		// 단서 물체 자리 (몸 기준). 화면 위쪽 (+z) 을 가운데로 벌림
		public void CueOffsets(int count, List<Vector3> results)
		{
			bool isMany = count > 1;
			float step = isMany ? CueSpreadDegrees / (count - 1) : 0f;
			float start = 90f - (isMany ? CueSpreadDegrees / 2f : 0f);
			for (int i = 0; i < count; i++)
			{
				float radians = (start + step * i) * Mathf.Deg2Rad;
				results.Add(new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * CueDistance);
			}
		}

		public override void Validate(Monster owner, List<string> errors)
		{
			base.Validate(owner, errors);
			if (Branches.Count < 2)
				errors.Add($"{name}: 갈래가 둘 미만");
			for (int i = 0; i < Branches.Count; i++)
			{
				ChoiceBranch branch = Branches[i];
				if (branch.Attack == null)
					errors.Add($"{name}: {i}번 갈래 공격이 비어 있음");
				else if (branch.Attack is ChoiceMonsterAttack or PackMonsterAttack or PhaseMonsterAttack)
					errors.Add($"{name}: 갈래 안에 {branch.Attack.name} 을 넣을 수 없음 (낱개나 순서만)");
				else
					branch.Attack.Validate(owner, errors);
				// 갈래 사거리가 더 짧으면 단서를 낸 뒤 갈래가 쫓기만 하며 멈춤
				if (branch.Attack != null && branch.Attack.TriggerRange < TriggerRange)
					errors.Add($"{name}: {i}번 갈래 사거리 {branch.Attack.TriggerRange} 가 갈래 선택 사거리 {TriggerRange} 보다 짧음");
				if (branch.CueFx == null)
					errors.Add($"{name}: {i}번 갈래 단서 물체가 비어 있음");
				for (int j = 0; j < i; j++)
				{
					if (branch.LooksLike(Branches[j]))
						errors.Add($"{name}: {j}번과 {i}번 갈래의 단서가 같아 못 읽음");
				}
			}
		}
	}
}
