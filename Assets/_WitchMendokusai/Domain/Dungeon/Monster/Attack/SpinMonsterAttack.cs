using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[Serializable]
	public class SpinArm
	{
		[field: SerializeField, Min(0.5f)] public float Length { get; private set; } = 6f;
		[field: SerializeField, Min(0.2f)] public float Width { get; private set; } = 0.9f;
		[field: SerializeField, Tooltip("몸에서 이만큼은 안 아픔 (m)"), Min(0f)] public float InnerRadius { get; private set; } = 1f;
		[field: SerializeField, Tooltip("준비를 시작한 순간 주인공 쪽에서 벌어진 각도 (도)")] public float StartAngle { get; private set; } = 90f;
		[field: SerializeField, Tooltip("도는 빠르기 (도/초). 음수면 반대로")] public float DegreesPerSecond { get; private set; } = 30f;
		[field: SerializeField, Tooltip("제 빠르기가 될 때까지 (초)"), Min(0f)] public float SpinUpSeconds { get; private set; } = 0.5f;
		[field: SerializeField, Tooltip("이 초에 멈췄다 반대로 돎. 0 이면 안 바뀜"), Min(0f)] public float ReverseAtSeconds { get; private set; }
		[field: SerializeField, Tooltip("반대로 돌기 전 멈추는 초"), Min(0f)] public float ReversePauseSeconds { get; private set; } = 0.5f;
		[field: SerializeField, Tooltip("바늘 그림. Turn 과 Seconds 를 씀")] public CombatFx Fx { get; private set; }

		// 돌기 시작한 뒤 흐른 초에 따라 돌아간 각도. 그림과 판정이 같이 씀
		public float AngleAt(float seconds)
		{
			float time = Mathf.Max(0f, seconds);
			if (ReverseAtSeconds <= 0f || time <= ReverseAtSeconds)
				return Travel(time) * DegreesPerSecond;
			float turned = Travel(ReverseAtSeconds) * DegreesPerSecond;
			float back = time - ReverseAtSeconds - ReversePauseSeconds;
			return turned - Travel(Mathf.Max(0f, back)) * DegreesPerSecond;
		}

		// 서서히 빨라지는 동안을 뺀 "제 빠르기로 돈 초"
		private float Travel(float time) => SpinUpSeconds <= 0f || time >= SpinUpSeconds ? time - SpinUpSeconds * 0.5f : time * time / (2f * SpinUpSeconds);
	}

	// 도는 바늘. 예고 뒤 몸을 축으로 길이와 빠르기가 다른 띠가 한동안 돌고, 닿으면 피해
	[CreateAssetMenu(fileName = "MA_Spin", menuName = "WM/Variable/MonsterAttack/" + nameof(SpinMonsterAttack))]
	public class SpinMonsterAttack : MonsterAttack
	{
		public override string KindName => "도는 바늘";

		[field: Header("도는 바늘")]
		[field: SerializeField, Tooltip("예고 동안 바늘마다 쓸고 갈 쪽 부채. 비우면 없음")] public CombatFx SweepTelegraph { get; private set; }
		[field: SerializeField, Range(5f, 180f)] public float SweepDegrees { get; private set; } = 55f;
		[field: SerializeField, Tooltip("도는 초"), Min(0.5f)] public float SpinSeconds { get; private set; } = 5f;
		[field: SerializeField] public List<SpinArm> Arms { get; private set; } = new();
		[field: SerializeField, Tooltip("끝에서 이만큼은 거둬지는 중이라 안 아픔 (초). 바늘 그림의 거둬지는 초와 같게"), Min(0f)] public float FoldSeconds { get; private set; } = 0.25f;
		[field: SerializeField, Tooltip("닿아 있는 동안 피해를 다시 시도하는 간격 (초). 회피에 성공해도 이만큼 쉼"), Min(0.1f)] public float HitInterval { get; private set; } = 0.6f;
		[field: SerializeField, Tooltip("돌기 시작하는 순간 타격 이펙트 반지름 (m)"), Min(0.1f)] public float StrikeFxRadius { get; private set; } = 2f;
		[field: SerializeField, Min(1)] public int Damage { get; private set; } = 4;
		[field: SerializeField, Tooltip("피해가 실제로 들어간 순간 주인공에게 거는 것")] public MonsterHitEffect HitEffect { get; private set; } = new();

		protected override bool PlaysStrikeFx => true;

		public override BTRunner CreateBehaviour(UnitObject unitObject, PlayerProvider playerProvider) =>
			new BT_Spin(unitObject, playerProvider, this);

		public override void Interrupt(BTRunner behaviour) => ((BT_Spin)behaviour).Interrupt();

		public override void Validate(Monster owner, List<string> errors)
		{
			base.Validate(owner, errors);
			if (WindupSeconds <= 0f)
				errors.Add($"{name}: 도는 바늘은 예고가 있어야 피할 수 있음 ({nameof(WindupSeconds)} 0)");
			if (Arms.Count == 0)
				errors.Add($"{name}: 바늘이 비어 있음");
			foreach (SpinArm arm in Arms)
			{
				if (arm.Fx == null)
					errors.Add($"{name}: 바늘 그림이 비어 있음");
				if (arm.InnerRadius >= arm.Length)
					errors.Add($"{name}: 안 아픈 거리 {arm.InnerRadius} 가 길이 {arm.Length} 이상");
			}
			HitEffect.Validate(name, errors);
		}
	}
}
