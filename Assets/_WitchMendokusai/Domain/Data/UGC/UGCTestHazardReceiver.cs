using UnityEngine;

namespace WitchMendokusai
{
	public class UGCTestHazardReceiver : MonoBehaviour
	{
		[SerializeField] private Color enabledColor = new Color(1f, 0.2f, 0.2f, 1f);
		[SerializeField] private Color disabledColor = new Color(0.2f, 1f, 0.2f, 1f);

		private Renderer cachedRenderer;
		private Collider cachedCollider;
		private bool isEnabled = true;

		private void Awake()
		{
			cachedRenderer = GetComponent<Renderer>();
			cachedCollider = GetComponent<Collider>();
			UGCMaterialSafety.EnsureUsableMaterial(cachedRenderer, enabledColor);
			UGCObjectRegistry.Register(gameObject.name, "Hazard", gameObject);
			ApplyState();
		}

		private void OnDestroy()
		{
			UGCObjectRegistry.Unregister(gameObject.name, gameObject);
		}

		public void UGC_SetHazardEnabled(bool enabled)
		{
			isEnabled = enabled;
			ApplyState();
			Debug.Log($"[UGC] Hazard '{name}' enabled={isEnabled}");
		}

		private void ApplyState()
		{
			if (cachedRenderer != null)
			{
				cachedRenderer.enabled = true;
				cachedRenderer.material.color = isEnabled ? enabledColor : disabledColor;
			}

			if (cachedCollider != null)
				cachedCollider.enabled = isEnabled;
		}

	}
}
