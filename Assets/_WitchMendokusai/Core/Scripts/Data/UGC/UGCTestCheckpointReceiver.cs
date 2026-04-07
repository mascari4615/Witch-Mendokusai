using UnityEngine;

namespace WitchMendokusai
{
	public class UGCTestCheckpointReceiver : MonoBehaviour
	{
		[SerializeField] private Color activeColor = new(0.2f, 0.6f, 1f, 1f);
		[SerializeField] private Color inactiveColor = new(0.5f, 0.5f, 0.5f, 1f);

		private Renderer cachedRenderer;
		private bool isActiveCheckpoint;

		private void Awake()
		{
			cachedRenderer = GetComponent<Renderer>();
			UGCObjectRegistry.Register(gameObject.name, "Checkpoint", gameObject);
			ApplyVisual();
		}

		private void OnDestroy()
		{
			UGCObjectRegistry.Unregister(gameObject.name, gameObject);
		}

		public void UGC_ActivateCheckpoint(bool setAsRespawn)
		{
			isActiveCheckpoint = setAsRespawn;
			ApplyVisual();
			Debug.Log($"[UGC] Checkpoint '{name}' activated. setAsRespawn={setAsRespawn}");
		}

		private void ApplyVisual()
		{
			if (cachedRenderer != null)
				cachedRenderer.material.color = isActiveCheckpoint ? activeColor : inactiveColor;
		}
	}
}
