using UnityEngine;

namespace WitchMendokusai
{
	[RequireComponent(typeof(Renderer))]
	public class FadeOverLife : MonoBehaviour
	{
		private static readonly int ALPHA_ID = Shader.PropertyToID("_Alpha");

		[SerializeField, Min(0.01f)] private float duration = 0.2f;
		[SerializeField, Range(0f, 1f)] private float startAlpha = 1f;
		[SerializeField, Range(0f, 1f)] private float endAlpha = 0f;

		private Renderer targetRenderer;
		private MaterialPropertyBlock properties;
		private float elapsed;

		private void Awake()
		{
			targetRenderer = GetComponent<Renderer>();
			properties = new MaterialPropertyBlock();
		}

		private void OnEnable()
		{
			elapsed = 0f;
			Apply();
		}

		private void Update()
		{
			elapsed += Time.deltaTime;
			Apply();
		}

		private void Apply()
		{
			properties.SetFloat(ALPHA_ID, Mathf.Lerp(startAlpha, endAlpha, Mathf.Clamp01(elapsed / duration)));
			targetRenderer.SetPropertyBlock(properties);
		}
	}
}
