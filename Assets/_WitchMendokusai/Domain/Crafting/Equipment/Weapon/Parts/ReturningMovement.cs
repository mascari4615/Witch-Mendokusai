using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class ReturningMovement : SkillComponent
	{
		[SerializeField, Min(0f)] private float outboundSpeed = 16f;
		[SerializeField, Min(0.01f)] private float outboundSeconds = 0.6f;
		[SerializeField, Min(0f)] private float returnSpeed = 18f;
		[SerializeField, Min(0f)] private float catchDistance = 0.8f;
		[SerializeField] private Transform spinTransform;
		[SerializeField] private float spinDegreesPerSecond = 720f;

		private Vector3 direction;
		private float elapsed;

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
			elapsed = 0f;
		}

		private void Update()
		{
			elapsed += Time.deltaTime;
			if (spinTransform != null)
				spinTransform.Rotate(0f, spinDegreesPerSecond * Time.deltaTime, 0f, Space.World);

			if (elapsed < outboundSeconds)
			{
				float speed = Mathf.Lerp(outboundSpeed, 0f, elapsed / outboundSeconds);
				transform.position += speed * Time.deltaTime * direction;
				return;
			}

			Vector3 toPlayer = playerProvider.Current.transform.position - transform.position;
			toPlayer.y = 0f;
			if (toPlayer.sqrMagnitude <= catchDistance * catchDistance)
			{
				gameObject.SetActive(false);
				return;
			}
			float ramp = Mathf.Clamp01((elapsed - outboundSeconds) / outboundSeconds);
			transform.position += returnSpeed * ramp * Time.deltaTime * toPlayer.normalized;
		}
	}
}
