using System;
using System.Collections.Generic;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>봉인된 두루마리 갈래. 화면은 성질 놀이 창 (UIAspectPuzzlePanel) 이 맡아 여기는 세이브 조각만</summary>
	public sealed class AspectScrollFeature : IFeatureInstaller
	{
		public string Id => "scroll";

		public void RegisterDataTypes(IDictionary<Type, string> assetPrefixes)
		{
		}

		public bool InstallScene(IContainerBuilder builder, SingletonCatalog catalog) => false;

		public void ResolveScene(IObjectResolver container)
		{
		}

		public IFeatureSaveSlice CreateSaveSlice() => new ScrollSaveSlice();
	}
}
