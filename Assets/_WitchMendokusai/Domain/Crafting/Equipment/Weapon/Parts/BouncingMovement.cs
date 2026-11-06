using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class BouncingMovement : SkillComponent
	{
		[SerializeField, Min(0f)] private float speed = 12f;
		[SerializeField] private Vector2 boundsHalfExtents = new(13f, 8f);

		private Vector3 direction;

		private PlayerProvider playerProvider;

		[Inject]
		public void Construct(PlayerProvider playerProvider)
		{
			this.playerProvider = playerProvider;
		}

		public override void InitContext(SkillObject skillObject)
		{
			direction = transform.forward;
			direction.y = 0f;
			direction = direction.normalized;
		}

		private void Update()
		{
			Vector3 position = transform.position + speed * Time.deltaTime * direction;
			Vector3 center = playerProvider.Current.transform.position;

			float offsetX = position.x - center.x;
			if (Mathf.Abs(offsetX) > boundsHalfExtents.x && Mathf.Sign(offsetX) == Mathf.Sign(direction.x))
				direction.x = -direction.x;

			float offsetZ = position.z - center.z;
			if (Mathf.Abs(offsetZ) > boundsHalfExtents.y && Mathf.Sign(offsetZ) == Mathf.Sign(direction.z))
				direction.z = -direction.z;

			transform.position = position;
		}
	}
}
