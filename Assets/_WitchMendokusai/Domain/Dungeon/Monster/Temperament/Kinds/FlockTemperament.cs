using UnityEngine;

namespace WitchMendokusai
{
	// 무리 짓기. 같은 성격이 모이면 용감해져 한 덩어리로 쫓고, 혼자면 겁먹어 거리를 두며 무리 쪽으로 감
	// 감각: 같은 성격 (Kin). 시키는 행동: 무리를 흩어 떨어진 놈부터 잡기
	[CreateAssetMenu(fileName = "MT_Flock", menuName = "WM/Monster/Temperament/Flock")]
	public sealed class FlockTemperament : MonsterTemperament
	{
		[SerializeField, Tooltip("반지름 안 같은 성격이 이만큼 이상이면 용감해짐 (나 빼고)"), Min(1)] private int braveCount = 2;
		[SerializeField, Tooltip("용감해진 뒤엔 이만큼 이상이면 용감 유지. 경계에서 오락가락 안 하게 braveCount 보다 작게"), Min(0)] private int staysBraveCount = 1;
		[SerializeField, Tooltip("용감할 때 무리 중심에서 이보다 멀면 중심 쪽으로 끌림 (m)"), Min(0f)] private float cohesionRadius = 2f;
		[SerializeField, Tooltip("무리 중심으로 끌리는 세기 (주인공 쪽이 1)"), Min(0f)] private float cohesionWeight = 0.8f;
		[SerializeField, Tooltip("혼자일 때 이 거리 안이면 물러남 (m)"), Min(0f)] private float aloneKeepDistance = 5f;
		[SerializeField, Tooltip("혼자일 때 물러나는 거리에서 이만큼 더 멀어야 다시 다가옴 (m). 사이에선 멈춰 봄"), Min(0f)] private float aloneBand = 1.5f;
		[SerializeField, Tooltip("혼자일 때 걷는 세기 (1 이 제 속도)"), Range(0f, 1f)] private float aloneSpeed = 0.6f;
		[SerializeField, Tooltip("용감해지는 순간 몸 부풀림 배율"), Min(1f)] private float bravePunch = 1.15f;

		public override string KindName => "무리 짓기";
		public override TemperamentSense Senses => TemperamentSense.Kin;

		public bool IsBrave(in TemperamentContext context, bool wasBrave) => context.KinCount >= (wasBrave ? Mathf.Min(staysBraveCount, braveCount) : braveCount);

		private void OnValidate() => staysBraveCount = Mathf.Min(staysBraveCount, braveCount);

		// 기억의 IsOn 은 지난 틱에 용감했나 (용감해지는 순간 부풀림)
		public override TemperamentStep Steer(in TemperamentContext context, TemperamentMemory memory)
		{
			Vector3 toPlayer = TemperamentContext.Toward(context.Self, context.Player);
			bool isBrave = IsBrave(context, memory.IsOn);
			float punch = isBrave && memory.IsOn == false ? bravePunch : 0f;
			memory.IsOn = isBrave;
			if (isBrave)
			{
				Vector3 toCenter = TemperamentContext.Flat(context.KinCenter - context.Self);
				Vector3 direction = toPlayer;
				if (toCenter.magnitude > cohesionRadius)
					direction += toCenter.normalized * cohesionWeight;
				TemperamentStep charge = TemperamentStep.Walk(direction.normalized);
				charge.Punch = punch;
				return charge;
			}
			if (context.Distance < aloneKeepDistance)
			{
				TemperamentStep back = TemperamentStep.Walk(-toPlayer, aloneSpeed);
				back.FacesPlayer = true;
				return back;
			}
			if (context.KinCount > 0)
				return TemperamentStep.Walk(TemperamentContext.Toward(context.Self, context.KinCenter), aloneSpeed);
			if (context.Distance < aloneKeepDistance + aloneBand)
				return TemperamentStep.Stay;
			return TemperamentStep.Walk(toPlayer, aloneSpeed);
		}
	}
}
