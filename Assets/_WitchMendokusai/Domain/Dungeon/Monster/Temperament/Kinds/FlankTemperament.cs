using UnityEngine;

namespace WitchMendokusai
{
	// 뒤잡기. 주인공이 바라보는 반대쪽 옆으로 돌아 들어감. 등 뒤 자리에 닿으면 곧장
	// 시키는 행동: 뒤돌아보기, 몸을 돌려 정면에 두기
	[CreateAssetMenu(fileName = "MT_Flank", menuName = "WM/Monster/Temperament/Flank")]
	public sealed class FlankTemperament : MonsterTemperament
	{
		// 바라보는 쪽이 이보다 짧으면 (막 태어남) 주인공에서 나를 향하는 쪽을 앞으로 봄
		private const float NO_LOOK_SQUARED = 0.01f;

		[SerializeField, Tooltip("주인공 등 뒤 이만큼에 섬 (m)"), Min(0f)] private float behindDistance = 2.5f;
		[SerializeField, Tooltip("돌아 들어갈 때 옆으로 벌리는 거리 (m)"), Min(0f)] private float sideDistance = 2.5f;
		[SerializeField, Tooltip("등 뒤 자리에서 이 거리 안이면 덮침 (m)"), Min(0f)] private float strikeRange = 1.2f;

		public override string KindName => "뒤잡기";

		public override TemperamentStep Steer(in TemperamentContext context, TemperamentMemory memory)
		{
			Vector3 look = TemperamentContext.Flat(context.PlayerLook);
			if (look.sqrMagnitude < NO_LOOK_SQUARED)
				look = -TemperamentContext.Toward(context.Player, context.Self);
			look.Normalize();
			Vector3 behind = context.Player - look * behindDistance;
			if (Vector3.Distance(TemperamentContext.Flat(context.Self), TemperamentContext.Flat(behind)) <= strikeRange)
				return TemperamentStep.Walk(TemperamentContext.Toward(context.Self, context.Player));
			// 정면에 있으면 옆으로 크게 돌아감. 도는 쪽은 유닛마다 정해 둠 (Side)
			Vector3 side = Vector3.Cross(Vector3.up, look) * memory.Side;
			bool isInFront = Vector3.Dot(TemperamentContext.Toward(context.Player, context.Self), look) > 0f;
			Vector3 goal = isInFront ? context.Player + side * sideDistance : behind;
			return TemperamentStep.Walk(TemperamentContext.Toward(context.Self, goal));
		}
	}
}
