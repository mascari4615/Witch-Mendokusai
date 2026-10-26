using UnityEngine;
using VContainer;
using static WitchMendokusai.WMHelper;

namespace WitchMendokusai
{
	public class PoolingObject : MonoBehaviour
	{
		private ObjectPoolManager objectPoolManager;

		[Inject]
		public void Construct(ObjectPoolManager objectPoolManager)
		{
			this.objectPoolManager = objectPoolManager;
		}

		private void OnDisable()
		{
			if (IsPlaying == false)
				return;

			// Teardown may destroy the injected Unity owner before disabling this object.
			// A missing injection remains an error; only an already destroyed owner is released.
			if (objectPoolManager == null && ReferenceEquals(objectPoolManager, null) == false)
				return;

			objectPoolManager.Despawn(gameObject);
		}
	}
}
