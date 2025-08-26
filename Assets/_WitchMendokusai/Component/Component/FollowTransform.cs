using UnityEngine;

namespace WitchMendokusai
{
	public class FollowTransform : MonoBehaviour
	{
		[SerializeField] private Transform target;

		private void Update()
		{
			if (target == null)
				return;

			transform.position = target.position;
		}
	}
}