using System;
using System.Collections.Generic;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>몬스터 기록 갈래가 자기를 심는 자리. 기록 (MonsterLogManager) 은 씬을 넘어 살아 RootLifetimeScope 가 심음. 여기는 세이브 조각만</summary>
	public sealed class MonsterLogFeature : IFeatureInstaller
	{
		public string Id => "monsterLog";

		public void RegisterDataTypes(IDictionary<Type, string> assetPrefixes)
		{
		}

		public bool InstallScene(IContainerBuilder builder, SingletonCatalog catalog) => false;

		public void ResolveScene(IObjectResolver container)
		{
		}

		public IFeatureSaveSlice CreateSaveSlice() => new MonsterLogSaveSlice();
	}
}
