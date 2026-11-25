using System;
using System.Collections.Generic;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	/// <summary>자취 연구 갈래가 자기를 심는 자리. 목록은 FeatureManifest. 화면은 아직 없고 세이브 조각만</summary>
	public sealed class TraceFeature : IFeatureInstaller
	{
		public string Id => "trace";

		// 조합판 자산은 Resources 의 TraceBookData 하나
		public void RegisterDataTypes(IDictionary<Type, string> assetPrefixes)
		{
		}

		public bool InstallScene(IContainerBuilder builder, SingletonCatalog catalog) => false;

		public void ResolveScene(IObjectResolver container)
		{
		}

		public IFeatureSaveSlice CreateSaveSlice() => new TraceSaveSlice();
	}
}
