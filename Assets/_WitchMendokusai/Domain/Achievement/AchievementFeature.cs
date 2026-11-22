using System;
using System.Collections.Generic;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	/// <summary>
	/// 업적 갈래가 자기를 심는 자리. 목록은 FeatureManifest.
	/// 판정 (AchievementManager) 은 씬을 넘어 살아야 해서 RootLifetimeScope 가 심음. 여기는 세이브 조각만.
	/// 자료 종류 (ACH, ACC) 는 DataSODefine 기본 목록에 있음: 갈래 설치는 Play 때만 돌아 에디터 Addressable 자동 등록이 못 보기 때문
	/// </summary>
	public sealed class AchievementFeature : IFeatureInstaller
	{
		public string Id => "achievement";

		// 자료 종류는 DataSODefine 기본 목록에
		public void RegisterDataTypes(IDictionary<Type, string> assetPrefixes)
		{
		}

		// 씬에 심을 프리팹 없음
		public bool InstallScene(IContainerBuilder builder, SingletonCatalog catalog) => false;

		public void ResolveScene(IObjectResolver container)
		{
		}

		public IFeatureSaveSlice CreateSaveSlice() => new AchievementSaveSlice();
	}
}
