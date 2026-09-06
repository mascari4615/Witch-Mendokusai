using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 이 게임에 담긴 갈래 목록. 갈래 이름을 아는 자리는 여기 하나뿐.
	///
	/// 새 갈래를 붙일 때 손댈 곳도 여기 한 줄. 공용 코드 (씬 조립, SO 타입 표, 저장) 는 Domain 의 FeatureRegistry 만 봄.
	/// 이 파일은 Domain 위 조립 어셈블리 (WM.App). 갈래 asmdef 전부를 보는 자리가 여기라 Domain 은 갈래를 몰라도 됨.
	/// 부팅 때 <see cref="Install"/> 이 Registry 를 채움 (씬이 뜨기 전, SubsystemRegistration)
	/// </summary>
	public static class FeatureManifest
	{
		[UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
		public static void Install() => FeatureRegistry.Install(installers);

		private static readonly List<IFeatureInstaller> installers = new()
		{
			new TowerDefenseFeature(),
			new ArenaFeature(),
			new AlchemyMapFeature(),
		};

		public static IReadOnlyList<IFeatureInstaller> Installers => installers;
	}
}
