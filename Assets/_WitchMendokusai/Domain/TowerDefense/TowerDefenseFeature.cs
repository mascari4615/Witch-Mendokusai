using System;
using System.Collections.Generic;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	/// <summary>개척 갈래가 자기를 심는 자리. 목록은 FeatureManifest</summary>
	public sealed class TowerDefenseFeature : IFeatureInstaller
	{
		public string Id => "towerDefense";

		public void RegisterDataTypes(IDictionary<Type, string> assetPrefixes)
		{
			// 미등록이면 DataSOAddressableSync 가 import 마다 LogError (무음 실패 트랩 회피)
			assetPrefixes[typeof(TowerDefenseStageSO)] = "TDS";
		}

		public bool InstallScene(IContainerBuilder builder, SingletonCatalog catalog)
		{
			// 프리팹 미생성 (코드 먼저 push) 이면 안 심음. 부팅은 안 깨짐
			TowerDefenseModeController prefab = catalog.Get<TowerDefenseModeController>();
			if (prefab == null)
			{
				return false;
			}

			builder.RegisterComponentInNewPrefab(prefab, Lifetime.Scoped);
			return true;
		}

		public void ResolveScene(IObjectResolver container)
		{
			BootGuard.EagerResolve<TowerDefenseModeController>(container, "Scene");
		}
	}
}
