using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class ArcFlight : SkillComponent
	{
		[SerializeField, Min(0.01f)] private float duration = 0.5f;
		[SerializeField, Min(0f)] private float arcHeight = 2f;
		[SerializeField, Min(0f)] private float dropHeight = 0f;

		private Vector3 start;
		private Vector3 end;
		private float elapsed;
		private ExplodeOnEnd explodeOnEnd;

		private PlayerProvider playerProvider;

		[Inject]
		public void Construct(PlayerProvider playerProvider)
		{
			this.playerProvider = playerProvider;
		}

		public override void InitContext(SkillObject skillObject)
		{
			explodeOnEnd = GetComponent<ExplodeOnEnd>();
			end = transform.position;
			start = dropHeight > 0f ? end + Vector3.up * dropHeight : playerProvider.Current.transform.position;
			transform.position = start;
			elapsed = 0f;
		}

		private void Update()
		{
			elapsed += Time.deltaTime;
			float ratio = Mathf.Clamp01(elapsed / duration);
			Vector3 position = Vector3.Lerp(start, end, ratio);
			position.y += arcHeight * 4f * ratio * (1f - ratio);
			transform.position = position;

			if (ratio < 1f)
				return;
			if (explodeOnEnd != null)
				explodeOnEnd.Explode();
			else
				gameObject.SetActive(false);
		}
	}
}
