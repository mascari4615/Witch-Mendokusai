using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 알리사 닌자 수리검의 움직임. 관통, 튕김, 귀환을 카드 스탯에서 읽음. 순서와 속도 판정은 ShurikenFlight
	// 피해는 같은 오브젝트의 RepeatingDamagingObject (적중 횟수는 안 씀, 관통은 여기서 셈)
	public class ShurikenMovement : SkillComponent, IHitEffect
	{
		[SerializeField, Min(0f)] private float outboundSpeed = 16f;
		[SerializeField, Min(0.01f)] private float outboundSeconds = 0.6f;
		[SerializeField, Min(0f)] private float returnSpeed = 18f;
		[SerializeField, Min(0.01f)] private float returnTimeoutSeconds = 3f;
		[SerializeField, Min(0f)] private float catchDistance = 0.8f;
		[SerializeField] private Transform spinTransform;
		[SerializeField] private float spinDegreesPerSecond = 1080f;

		[Header("Stat")]
		[SerializeField] private UnitStatType returnStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType bounceStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType pierceStat = UnitStatType.NONE;

		private readonly ShurikenFlight flight = new();
		private readonly List<GameObject> visited = new();
		private SkillContext context;
		private Vector3 direction;
		private int pierceLeft;

		private PlayerProvider playerProvider;

		[Inject]
		public void Construct(PlayerProvider playerProvider) => this.playerProvider = playerProvider;

		public override void InitContext(SkillObject skillObject)
		{
			context = skillObject.Context;
			direction = Flat(transform.forward);
			visited.Clear();
			pierceLeft = Stat(pierceStat);
			flight.Begin(outboundSeconds, Stat(returnStat) > 0, Stat(bounceStat), returnTimeoutSeconds);
		}

		// 카드 스탯이 없는 칸은 0 (켜지지 않음)
		private int Stat(UnitStatType stat) => stat == UnitStatType.NONE ? 0 : SkillSpawn.Stacks(context, stat);

		public void OnHit(DamagingObject source, UnitObject victim)
		{
			if (flight.Phase != ShurikenFlightPhase.Outbound)
				return;
			visited.Add(victim.gameObject);
			Transform next = flight.BouncesLeft > 0 ? SurvivalTargeting.Nearest(transform.position, flight.ReachAfterBounce(outboundSpeed), visited) : null;
			if (flight.TryBounce(next != null))
			{
				direction = Flat(next.position - transform.position);
				return;
			}
			if (--pierceLeft >= 0)
				return;
			flight.Expend();
			EndIfDone();
		}

		private void Update()
		{
			flight.Tick(Time.deltaTime);
			if (spinTransform != null)
				spinTransform.Rotate(0f, spinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
			if (flight.Phase == ShurikenFlightPhase.Returning)
				Home();
			transform.position += flight.Speed(outboundSpeed, returnSpeed) * Time.deltaTime * direction;
			EndIfDone();
		}

		private void Home()
		{
			Vector3 toOwner = playerProvider.Current.transform.position - transform.position;
			toOwner.y = 0f;
			if (toOwner.sqrMagnitude <= catchDistance * catchDistance)
				flight.Catch();
			else
				direction = toOwner.normalized;
		}

		private void EndIfDone()
		{
			if (flight.Phase == ShurikenFlightPhase.Done)
				gameObject.SetActive(false);
		}

		private static Vector3 Flat(Vector3 vector)
		{
			vector.y = 0f;
			return vector.sqrMagnitude > 0.0001f ? vector.normalized : Vector3.forward;
		}
	}
}
