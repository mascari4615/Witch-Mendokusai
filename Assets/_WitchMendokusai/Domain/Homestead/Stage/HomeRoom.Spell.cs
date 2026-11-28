using UnityEngine;

namespace WitchMendokusai
{
	// 욘의 집 연구대. 생활 주문을 배우는 자리 (SpellPage). 쓰는 것은 마을의 기다리는 칸 (VillageStage.Spell)
	// - 프리팹 (HomeHouse) 을 고치지 않고 집 원점 기준 자리에 마법 책상 가구 모형을 세움
	public sealed partial class HomeRoom
	{
		private const string SPELL_DESK_MODEL = FURNITURE_KEY + "magic_desk";

		[Header("Spell")]
		[Tooltip("연구대 자리. 집 프리팹 원점 기준 (제작대와 같은 서쪽)")]
		[SerializeField] private Vector3 spellSpotLocal = new(-9.6f, 0f, 0.2f);
		[Tooltip("연구대 모형의 방향 (도)")]
		[SerializeField] private float spellDeskYaw = 90f;
		[Tooltip("모형과 다가서는 자리 사이. 모형 앞쪽 거리")]
		[SerializeField] private float spellStandOff = 0.9f;

		private Transform spellSpot;

		private void BuildSpell()
		{
			Models.Build(SPELL_DESK_MODEL, house.transform, spellSpotLocal, spellDeskYaw);
			spellSpot = AddSpot("SpellSpot", "연구대", SpellRole, () => manager.Panels.Show(HomesteadPanelType.Spell), () => "연구");
			spellSpot.position = house.transform.TransformPoint(spellSpotLocal + Quaternion.Euler(0f, spellDeskYaw, 0f) * new Vector3(0f, 0f, -spellStandOff));
		}

		private string SpellRole()
		{
			int known = Sim.State.Spells.Count;
			return known == 0 ? "생활 주문 연구" : $"배운 주문 {known} / {Sim.Catalog.Spells.Count}";
		}
	}
}
