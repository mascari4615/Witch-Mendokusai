using UnityEditor;
using Entry = WitchMendokusai.UltimateFeelV2Batch2Builder.Entry;

namespace WitchMendokusai
{
	/// <summary>
	/// 강한 판 V2 배치 3 둘 (별자리, 밤의 날) 프리셋 생성. 절차는 배치 2 생성기 (UltimateFeelV2Batch2Builder.Run) 그대로
	/// - body: 3D 강한 판 에셋 (Ult3D_&lt;이름&gt;Heavy), feel: 박자표 (처음 값 UltimateFeelV2Batch3Seeds), 래퍼: FeelUltimateFx 크기 1.45
	/// - 카메라 물림: 별자리는 공중 별, 밤의 날은 칼끝이 화면 위 끝에 닿아 처음부터 넣음
	/// - 개발 키 constellation_heavy_v2, nightblade_heavy_v2
	/// 메뉴: WM/CombatFx/Build Ultimate Feel V2 Batch3, WM/CombatFx/Reseed Ultimate Feel V2 Batch3
	/// </summary>
	public static class UltimateFeelV2Batch3Builder
	{
		private static Entry[] Entries() => new[]
		{
			new Entry
			{
				Name = "ConstellationHeavyV2", DevKey = "constellation_heavy_v2", Body = "Ult3D_ConstellationHeavy", TargetForward = 0.9f,
				Swells = UltimateFeelV2Batch3Seeds.ConstellationSwells, Beats = UltimateFeelV2Batch3Seeds.ConstellationBeats,
				Trauma = UltimateFeelV2Seeds.Trauma(0.022f, 15f, 2f, 0.65f),
				PullBack = UltimateFeelV2Batch3Seeds.PullBack(0.22f, 0.9f, 0.9f),
			},
			new Entry
			{
				Name = "NightBladeHeavyV2", DevKey = "nightblade_heavy_v2", Body = "Ult3D_NightBladeHeavy", TargetForward = 1.2f,
				Swells = UltimateFeelV2Batch3Seeds.NightBladeSwells, Beats = UltimateFeelV2Batch3Seeds.NightBladeBeats,
				Trauma = UltimateFeelV2Seeds.Trauma(0.024f, 14f, 2.2f, 0.5f),
				PullBack = UltimateFeelV2Batch3Seeds.PullBack(0.2f, 0.4f, 0.8f),
			},
		};

		[MenuItem("WM/CombatFx/Build Ultimate Feel V2 Batch3")]
		public static void Build() => UltimateFeelV2Batch2Builder.Run(Entries(), false, "배치 3");

		[MenuItem("WM/CombatFx/Reseed Ultimate Feel V2 Batch3")]
		public static void Reseed() => UltimateFeelV2Batch2Builder.Run(Entries(), true, "배치 3");
	}
}
