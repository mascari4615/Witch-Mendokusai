using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	public static class Bootstrap
	{
		[UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
		public static void OnBooting()
		{
			if (VContainerSettings.Instance == null)
			{
				VContainerSettings.LoadInstanceFromPreloadAssets();
			}
			VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance();
		}
	}
}
