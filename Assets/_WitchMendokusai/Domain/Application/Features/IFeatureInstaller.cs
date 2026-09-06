using System;
using System.Collections.Generic;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	/// <summary>
	/// 갈래 하나가 자기를 설치하는 계약.
	///
	/// 왜: 공용 코드 (씬 조립, SO 타입 표, 저장) 가 갈래 이름을 알면 갈래를 못 뗌
	/// 뒤집기. 공용은 목록만 순회, 무엇을 심을지는 갈래가 앎
	/// 목록 자리는 <see cref="FeatureManifest"/> 하나뿐
	/// </summary>
	public interface IFeatureInstaller
	{
		/// <summary>기록과 로그에 쓰는 이름. 갈래마다 하나</summary>
		string Id { get; }

		/// <summary>이 갈래가 쓰는 DataSO 타입과 자산 이름 앞머리. 없으면 아무것도 안 담는다</summary>
		void RegisterDataTypes(IDictionary<Type, string> assetPrefixes);

		/// <summary>씬 조립에 심기. 프리팹이 아직 없으면 아무것도 안 하고 false</summary>
		bool InstallScene(IContainerBuilder builder, SingletonCatalog catalog);

		/// <summary>심은 것 깨우기. <see cref="InstallScene"/> 이 true 를 준 경우에만 불린다</summary>
		void ResolveScene(IObjectResolver container);

		/// <summary>세이브에 남길 조각. 남길 것이 없으면 null</summary>
		IFeatureSaveSlice CreateSaveSlice();
	}
}
