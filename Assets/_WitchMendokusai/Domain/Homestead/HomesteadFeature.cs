using System;
using System.Collections.Generic;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	/// <summary>생활 갈래 (집, 마을, 농사, 채집, 낚시, 갱도, 박물관) 가 자기를 심는 자리. 목록은 FeatureManifest</summary>
	public sealed class HomesteadFeature : IFeatureInstaller
	{
		public string Id => "homestead";

		// 자료는 글 파일 (Resources/Homestead). 전용 DataSO 없음
		public void RegisterDataTypes(IDictionary<Type, string> assetPrefixes)
		{
		}

		public bool InstallScene(IContainerBuilder builder, SingletonCatalog catalog)
		{
			builder.RegisterComponentOnNewGameObject<HomesteadManager>(Lifetime.Scoped, nameof(HomesteadManager));
			return true;
		}

		public void ResolveScene(IObjectResolver container) => BootGuard.EagerResolve<HomesteadManager>(container, "Scene");

		public IFeatureSaveSlice CreateSaveSlice() => new HomesteadSaveSlice();
	}
}
