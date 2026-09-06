using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 이 판에 심긴 갈래 목록. 공용 (씬 조립, SO 타입 표, 저장) 은 여기만 봄
	///
	/// 갈래 이름은 Domain 위 조립 어셈블리 (WM.App 의 FeatureManifest) 만 앎. 부팅 때 <see cref="Install"/> 로 채움
	/// Domain 이 갈래를 이름으로 알면 갈래 asmdef 가 Domain 을 참조하는 순간 순환. 그래서 목록은 위에서 내려옴
	/// </summary>
	public static class FeatureRegistry
	{
		private static readonly List<IFeatureInstaller> installers = new();

		public static IReadOnlyList<IFeatureInstaller> Installers => installers;

		/// <summary>조립 어셈블리가 부팅 때 한 번. 다시 부르면 목록을 갈아 끼움 (도메인 리로드, 시험)</summary>
		public static void Install(IEnumerable<IFeatureInstaller> features)
		{
			installers.Clear();
			installers.AddRange(features);
		}

		/// <summary>갈래마다 저장 조각 하나씩. 없는 갈래는 건너뜀</summary>
		public static List<IFeatureSaveSlice> CreateSaveSlices()
		{
			List<IFeatureSaveSlice> slices = new();
			foreach (IFeatureInstaller installer in installers)
			{
				IFeatureSaveSlice slice = installer.CreateSaveSlice();
				if (slice != null)
				{
					slices.Add(slice);
				}
			}
			return slices;
		}
	}
}
