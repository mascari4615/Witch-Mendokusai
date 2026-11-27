using System;
using System.Collections.Generic;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	/// <summary>봉인된 두루마리 갈래. 화면은 성질 놀이 창 (UIAspectPuzzlePanel). 여기는 세이브 조각과 처치에서 떨구기 (ScrollDropper)</summary>
	public sealed class AspectScrollFeature : IFeatureInstaller
	{
		public string Id => "scroll";

		public void RegisterDataTypes(IDictionary<Type, string> assetPrefixes)
		{
		}

		// 처치에서 두루마리를 떨구는 것. 씬마다 새로 심어 씬이 끝나면 듣기를 끔
		public bool InstallScene(IContainerBuilder builder, SingletonCatalog catalog)
		{
			builder.RegisterEntryPoint<ScrollDropper>(Lifetime.Scoped);
			return true;
		}

		public void ResolveScene(IObjectResolver container)
		{
		}

		public IFeatureSaveSlice CreateSaveSlice() => new ScrollSaveSlice();
	}
}
