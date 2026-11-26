using UnityEngine;

namespace WitchMendokusai
{
	// 거울. 주인공 움직임을 둘 사이 거울에 비춰 따라 함. 다가가면 마주 오고, 물러나면 같이 물러나고, 옆으로 가면 같은 옆으로
	// 주인공이 멈추면 같이 멈춤. 너무 멀어지면 천천히 돌아옴. 시키는 행동: 내 움직임으로 끌어당기기
	[CreateAssetMenu(fileName = "MT_Mirror", menuName = "WM/Monster/Temperament/Mirror")]
	public sealed class MirrorTemperament : MonsterTemperament
	{
		[SerializeField, Tooltip("주인공이 이보다 느리면 멈춘 것으로 봄 (m/s)"), Min(0f)] private float stillSpeed = 0.3f;
		[SerializeField, Tooltip("이보다 멀면 거울을 그만두고 돌아옴 (m)"), Min(0f)] private float leashDistance = 9f;
		[SerializeField, Tooltip("돌아올 때 세기 0~1"), Range(0f, 1f)] private float returnSpeed = 0.6f;

		public override string KindName => "거울";

		// 주인공 속도를 둘을 잇는 선에 수직인 거울에 비춤. 선 방향 성분만 뒤집힘
		public static Vector3 Reflect(Vector3 playerVelocity, Vector3 playerToSelf)
		{
			Vector3 normal = TemperamentContext.Flat(playerToSelf).normalized;
			Vector3 velocity = TemperamentContext.Flat(playerVelocity);
			return velocity - 2f * Vector3.Dot(velocity, normal) * normal;
		}

		public override TemperamentStep Steer(in TemperamentContext context, TemperamentMemory memory)
		{
			if (context.Distance > leashDistance)
				return TemperamentStep.Walk(TemperamentContext.Toward(context.Self, context.Player), returnSpeed);
			Vector3 velocity = TemperamentContext.Flat(context.PlayerVelocity);
			if (velocity.magnitude < stillSpeed)
				return TemperamentStep.Stay;
			Vector3 mirrored = Reflect(velocity, context.Self - context.Player);
			return new TemperamentStep { Direction = mirrored.normalized, Speed = 1f, FacesPlayer = true };
		}
	}
}
