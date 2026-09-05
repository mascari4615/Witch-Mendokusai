using UnityEngine;

namespace WitchMendokusai
{
	public class UGCTestDoorReceiver : MonoBehaviour
	{
		[SerializeField] private Color openColor = new(0.2f, 0.9f, 0.4f, 1f);
		[SerializeField] private Color closedColor = new(0.8f, 0.2f, 0.2f, 1f);

		private Renderer cachedRenderer;
		private bool isOpen;

		private void Awake()
		{
			cachedRenderer = GetComponent<Renderer>();
			UGCMaterialSafety.EnsureUsableMaterial(cachedRenderer, openColor);
			UGCObjectRegistry.Register(gameObject.name, "Door", gameObject);
			ApplyVisual();
		}

		private void OnDestroy()
		{
			UGCObjectRegistry.Unregister(gameObject.name, gameObject);
		}

		public void UGC_SetDoorState(bool open)
		{
			isOpen = open;
			ApplyVisual();
			Debug.Log($"[UGC] Door '{name}' state changed. isOpen={isOpen}");
		}

		private void ApplyVisual()
		{
			if (cachedRenderer != null)
				cachedRenderer.material.color = isOpen ? openColor : closedColor;
		}

	}
}
