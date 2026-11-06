using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class RepeatingDamagingObject : DamagingObject
	{
		[Header("_" + nameof(RepeatingDamagingObject))]
		[SerializeField, Min(0.02f)] private float rehitInterval = 0.5f;

		private readonly Dictionary<GameObject, float> nextHitTimes = new();

		public override void InitContext(SkillObject skillObject)
		{
			base.InitContext(skillObject);
			nextHitTimes.Clear();
		}

		protected override bool CanHit(GameObject target)
		{
			if (nextHitTimes.TryGetValue(target, out float nextHitTime) && Time.time < nextHitTime)
				return false;
			nextHitTimes[target] = Time.time + rehitInterval;
			return true;
		}

		private void OnTriggerStay(Collider other)
		{
			OnTriggerEnter(other);
		}
	}
}
