using System;
using System.Collections.Generic;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	/// <summary>솥 지도 제조 갈래가 자기를 심는 자리. 목록은 FeatureManifest</summary>
	public sealed class AlchemyMapFeature : IFeatureInstaller
	{
		public string Id => "alchemyMap";

		public void RegisterDataTypes(IDictionary<Type, string> assetPrefixes)
		{
			// 전용 DataSO 없음
		}

		public bool InstallScene(IContainerBuilder builder, SingletonCatalog catalog)
		{
			CauldronMapController prefab = catalog.Get<CauldronMapController>();
			if (prefab == null)
			{
				return false;
			}

			builder.RegisterComponentInNewPrefab(prefab, Lifetime.Scoped);
			return true;
		}

		public void ResolveScene(IObjectResolver container)
		{
			BootGuard.EagerResolve<CauldronMapController>(container, "Scene");
		}

		// 판 밖에 남기는 것 없음
		public IFeatureSaveSlice CreateSaveSlice() => null;
	}
}
