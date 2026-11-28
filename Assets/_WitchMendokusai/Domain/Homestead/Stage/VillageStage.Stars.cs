using UnityEngine;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 별 보는 언덕. 마을 칸 (StarTuning.SpotX, SpotZ) 에 망원경 하나와 상호작용 자리
	// 모델은 집 가구 망원경 (F_telescope) 을 밖에 맞게 키워 씀. 화면은 UIStarPanel
	public sealed partial class VillageStage
	{
		private const string STAR_MODEL = "F_telescope";

		[Header("Stars")]
		[Tooltip("밖에 세운 망원경 크기. 집 가구 크기의 배")]
		[SerializeField] private float starModelScale = 1.8f;
		[SerializeField] private float starModelYaw = 200f;
		[Tooltip("상호작용 자리를 망원경 앞으로 뺀 거리 (m)")]
		[SerializeField] private float starSpotAhead = 0.9f;

		private void BuildStars()
		{
			StarTuning tuning = Catalog.StarTuning;
			Vector3 position = new(tuning.SpotX, 0f, tuning.SpotZ);
			Models.Build(STAR_MODEL, outdoor, position, starModelYaw, starModelScale);
			Vector3 front = position + Quaternion.Euler(0f, starModelYaw, 0f) * new Vector3(0f, 0f, -starSpotAhead);
			HomesteadSpot.Create(outdoor, front, "StarSpot", () => "별 보는 언덕", StarRole, () => manager.Panels.Show(HomesteadPanelType.Stars), () => "별 보기");
		}

		private string StarRole()
		{
			if (Sim.HasStargazed(manager.Hour, manager.CalendarDay))
				return Sim.HasOmen ? "오늘의 점을 받음" : "오늘 밤은 봤음";
			return Sim.IsStarHour(manager.Hour) ? "별이 떴음" : $"밤 {Catalog.StarTuning.FromHour}시부터";
		}
	}
}
