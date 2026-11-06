using UnityEngine;

namespace WitchMendokusai
{
	public class ScaleOverLife : MonoBehaviour
	{
		[SerializeField] private Vector3 startScale = Vector3.one;
		[SerializeField] private Vector3 endScale = Vector3.one;
		[SerializeField, Min(0.01f)] private float duration = 0.2f;

		private float elapsed;

		private void OnEnable()
		{
			elapsed = 0f;
			transform.localScale = startScale;
		}

		private void Update()
		{
			elapsed += Time.deltaTime;
			transform.localScale = Vector3.Lerp(startScale, endScale, Mathf.Clamp01(elapsed / duration));
		}
	}
}
