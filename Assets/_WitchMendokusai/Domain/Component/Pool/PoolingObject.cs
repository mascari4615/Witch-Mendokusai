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

			objectPoolManager.Despawn(gameObject);
		}
	}
}
