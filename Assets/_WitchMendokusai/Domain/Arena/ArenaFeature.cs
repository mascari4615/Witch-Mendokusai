using System;
using System.Collections.Generic;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	/// <summary>투기장 갈래가 자기를 심는 자리. 목록은 FeatureManifest</summary>
	public sealed class ArenaFeature : IFeatureInstaller
	{
		public string Id => "arena";

		public void RegisterDataTypes(IDictionary<Type, string> assetPrefixes)
		{
			// 전용 DataSO 없음
		}

		public bool InstallScene(IContainerBuilder builder, SingletonCatalog catalog)
		{
			ArenaModeController prefab = catalog.Get<ArenaModeController>();
			if (prefab == null)
			{
				return false;
			}

			// Scoped 수명을 쓰는 이유. GameModeManager 구독 수명과 맞고, World.unity 에 안 놓아 다세션 씬 경합에서 자유롭다
			builder.RegisterComponentInNewPrefab(prefab, Lifetime.Scoped);
			return true;
		}

		public void ResolveScene(IObjectResolver container)
		{
			BootGuard.EagerResolve<ArenaModeController>(container, "Scene");
		}

		// 판 밖에 남기는 것 없음
		public IFeatureSaveSlice CreateSaveSlice() => null;
	}
}
