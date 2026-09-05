using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 이 게임에 담긴 갈래 목록. 갈래 이름을 아는 자리는 여기 하나뿐.
	///
	/// 새 갈래를 붙일 때 손댈 곳도 여기 한 줄. 공용 코드 (씬 조립, SO 타입 표) 는 그대로.
	/// 갈래마다 어셈블리를 가르면 이 파일이 그 전부를 보는 조립 지점.
	/// </summary>
	public static class FeatureManifest
	{
		private static readonly List<IFeatureInstaller> installers = new()
		{
			new TowerDefenseFeature(),
			new ArenaFeature(),
			new AlchemyMapFeature(),
		};

		public static IReadOnlyList<IFeatureInstaller> Installers => installers;
	}
}
