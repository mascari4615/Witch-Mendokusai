using UnityEngine;

namespace WitchMendokusai
{
	public class RepeatingDamagingObject : DamagingObject
	{
		[Header("_" + nameof(RepeatingDamagingObject))]
		[SerializeField, Min(0.02f)] private float rehitInterval = 0.5f;

		protected override HitRule Rule => base.Rule.WithInterval(rehitInterval);

		private void OnTriggerStay(Collider other)
		{
			OnTriggerEnter(other);
		}
	}
}
