using UnityEngine;

namespace WitchMendokusai
{
	// 맴돌기. 주인공 둘레를 돌며 원을 조금씩 좁힘. 다 좁히면 원을 다시 넓혀 돎
	// 시키는 행동: 원 밖으로 뛰쳐나가기, 도는 앞길에 공격 놓기
	[CreateAssetMenu(fileName = "MT_Orbit", menuName = "WM/Monster/Temperament/Orbit")]
	public sealed class OrbitTemperament : MonsterTemperament
	{
		[SerializeField, Tooltip("처음 도는 반지름 (m)"), Min(0.5f)] private float startRadius = 6f;
		[SerializeField, Tooltip("가장 좁은 반지름 (m)"), Min(0f)] private float endRadius = 1.5f;
		[SerializeField, Tooltip("넓은 원에서 좁은 원까지 걸리는 초"), Min(0.1f)] private float closeSeconds = 6f;
		[SerializeField, Tooltip("반지름을 맞추려 안팎으로 끌리는 세기"), Min(0f)] private float radialPull = 1.2f;

		public override string KindName => "맴돌기";

		// 기억의 NextTime 은 이번 원을 시작한 시각, Side 는 도는 쪽
		public override TemperamentStep Steer(in TemperamentContext context, TemperamentMemory memory)
		{
			float elapsed = context.Now - memory.NextTime;
			if (elapsed > closeSeconds || elapsed < 0f)
			{
				memory.NextTime = context.Now;
				elapsed = 0f;
			}
			float radius = Mathf.Lerp(startRadius, endRadius, elapsed / closeSeconds);
			Vector3 outward = TemperamentContext.Toward(context.Player, context.Self);
			if (outward == Vector3.zero)
				outward = Vector3.forward;
			Vector3 tangent = Vector3.Cross(Vector3.up, outward) * memory.Side;
			// 원보다 멀면 안으로 (음수), 가까우면 밖으로 (양수). 반지름 비율로 재서 원 크기와 상관없이 같은 세기
			float radialError = Mathf.Clamp((radius - context.Distance) / radius, -1f, 1f);
			Vector3 direction = tangent + outward * radialError * radialPull;
			return new TemperamentStep { Direction = direction.normalized, Speed = 1f, FacesPlayer = true };
		}
	}
}
