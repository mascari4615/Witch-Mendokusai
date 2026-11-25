using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 못과 개울, 낚시. 낚시터를 지으면 못, 두 단이면 개울에서도
	public sealed partial class VillageStage
	{
		private const string POND = "pond";
		private const string STREAM = "stream";
		private const int STREAM_DOCK_LEVEL = 2;
		private const int LINE_POINTS = 10;

		[Header("Water")]
		[SerializeField] private Vector2 waterFlow = new(0.012f, 0.02f);
		[SerializeField] private Color shoreColor = new(1.18f, 1.2f, 1.14f);
		[SerializeField] private Color deepColor = new(0.72f, 0.8f, 0.92f);
		[SerializeField] private Vector3 streamFrom = new(24f, 0f, -5f);
		[SerializeField] private Vector3 streamTo = new(33f, 0f, 6f);
		[SerializeField] private float streamWidth = 2.2f;
		[SerializeField] private Vector3 streamSpot = new(24.4f, 0f, -1.4f);
		[SerializeField] private float resultSeconds = 1.6f;
		[SerializeField] private float floatHeight = 0.5f;
		[SerializeField] private float lineWidth = 0.02f;
		[SerializeField] private float lineSag = 0.5f;
		[SerializeField] private Vector3 rodTip = new(0.4f, 0.9f, 0f);

		private Material waterMaterial;
		private FishingDuel duel;
		private string fishingWater;
		private bool reelHeld;
		private float resultTimer;
		private HomesteadSprite bobber;
		private LineRenderer fishingLine;
		private GameObject streamSpotObject;

		private void BuildWater()
		{
			waterMaterial = new Material(Art.Water) { name = "VillageWater", hideFlags = HideFlags.DontSave };
			Pool(pondCenter, pondRadius, shoreColor, 0f);
			Pool(pondCenter, pondRadius * 0.86f, Color.white, 0.012f);
			Pool(pondCenter + new Vector3(0.4f, 0f, 0.3f), pondRadius * 0.62f, deepColor, 0.024f);
			Vector3 along = streamTo - streamFrom;
			int steps = Mathf.CeilToInt(along.magnitude / (streamWidth * 0.5f));
			for (int i = 0; i <= steps; i++)
			{
				float ratio = i / (float)steps;
				Vector3 side = new Vector3(-along.z, 0f, along.x).normalized * Mathf.Sin(ratio * 5f) * 0.8f;
				Pool(streamFrom + along * ratio + side, streamWidth * 0.5f + Mathf.Sin(i * 1.9f) * 0.2f, Color.white, i * 0.0008f);
			}
			for (int i = 0; i < 26; i++)
			{
				float angle = i / 26f * Mathf.PI * 2f;
				float radius = pondRadius + 0.15f + Mathf.PerlinNoise(i * 0.7f, 1.3f) * 0.5f;
				Vector3 foot = pondCenter + new Vector3(Mathf.Cos(angle) * radius, -0.1f, Mathf.Sin(angle) * radius);
				// 낚시터가 들어설 자리는 비움
				if (Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, 180f)) < 22f)
					continue;
				VillageKit stone = new();
				float size = 0.5f + Mathf.PerlinNoise(i * 1.3f, 0.2f) * 0.7f;
				stone.Prism(Art.Stone, Color.white, Vector3.zero, new Vector3(size, size * 0.5f, size * 0.8f), 6, 0.6f);
				stone.Build("PondStone", outdoor, foot, i * 57f);
			}
			GameObject block = new("PondBlock");
			block.transform.SetParent(outdoor, false);
			block.transform.localPosition = pondCenter;
			CapsuleCollider capsule = block.AddComponent<CapsuleCollider>();
			capsule.radius = pondRadius - 0.2f;
			capsule.height = pondRadius * 2f + 4f;
			bobber = HomesteadSprite.Create(outdoor, pondCenter, Art.Icon("bobber"), floatHeight, 0.05f);
			bobber.gameObject.SetActive(false);
			fishingLine = new GameObject("FishingLine").AddComponent<LineRenderer>();
			fishingLine.transform.SetParent(outdoor, false);
			fishingLine.sharedMaterial = Art.Cloth;
			fishingLine.widthMultiplier = lineWidth;
			fishingLine.positionCount = LINE_POINTS;
			fishingLine.enabled = false;
			streamSpotObject = HomesteadSpot.Create(outdoor, streamSpot, "StreamSpot", () => "개울", () => "낚시", () => StartFishing(STREAM)).gameObject;
		}

		private void Pool(Vector3 center, float radius, Color color, float lift)
		{
			VillageKit kit = new();
			kit.Prism(waterMaterial, color, Vector3.zero, new Vector3(radius * 2f, 0.05f, radius * 2f), 22);
			kit.Build("Water", outdoor, center + Vector3.up * (0.01f + lift), 0f, castShadow: false);
		}

		private void FlowWater() => waterMaterial.SetTextureOffset("_BaseMap", waterFlow * Time.time);

		private void RefreshWaterSpots() => streamSpotObject.SetActive(Sim.BuildingLevel("dock") >= STREAM_DOCK_LEVEL);

		private void StartFishing(string water)
		{
			if (duel != null)
				return;
			fishingWater = water;
			duel = Sim.StartDuel(Sim.PickFish(water, manager.Hour, manager.Season));
			reelHeld = false;
			resultTimer = 0f;
			CombatSfx.Play(CombatSfxEvent.FishingCast, playerProvider.Current.transform.position);
			Vector3 target = water == POND ? pondCenter + new Vector3(-pondRadius * 0.35f, 0.1f, 0.6f) : streamSpot + new Vector3(1.6f, 0.1f, 0.6f);
			bobber.SetHome(target);
			bobber.transform.localPosition = target;
			bobber.gameObject.SetActive(true);
			manager.Hud.ShowDuel(true);
			manager.Panels.Fishing.Bind(PressReel, ReleaseReel, EndFishing);
			manager.Panels.Show(HomesteadPanelType.Fishing);
		}

		private void PressReel()
		{
			if (duel == null)
				return;
			reelHeld = true;
			duel.Press();
		}

		private void ReleaseReel() => reelHeld = false;

		// 화면을 닫으면 EndFishing 이 뒤따름
		private void StopFishing()
		{
			if (duel == null)
				return;
			if (manager.Panels != null && manager.Panels.CurPanelType == HomesteadPanelType.Fishing)
				manager.Panels.ClosePanel();
			EndFishing();
		}

		private void EndFishing()
		{
			if (duel == null)
				return;
			duel = null;
			reelHeld = false;
			if (bobber != null)
			{
				bobber.gameObject.SetActive(false);
				fishingLine.enabled = false;
			}
			if (manager.Hud != null)
				manager.Hud.ShowDuel(false);
		}

		private void StepFishing()
		{
			if (duel == null)
				return;
			if (duel.IsOver)
			{
				resultTimer -= Time.deltaTime;
				if (resultTimer <= 0f)
					StopFishing();
				return;
			}
			DrawLine();
			FishingPhase before = duel.Phase;
			duel.Step(Time.deltaTime, reelHeld);
			manager.Hud.SetDuel(duel);
			if (duel.Phase == FishingPhase.Bite && before == FishingPhase.Waiting)
			{
				CombatSfx.Play(CombatSfxEvent.FishingBite, bobber.transform.position);
				uiManager.SpeechBubble.Show(playerProvider.Current.transform, "!", Catalog.Tuning.BiteWindow);
			}
			if (duel.IsOver)
				FinishFishing();
		}

		// 팽팽할수록 곧게
		private void DrawLine()
		{
			fishingLine.enabled = true;
			Vector3 from = playerProvider.CurrentObject.transform.position + rodTip;
			Vector3 to = bobber.transform.position + Vector3.up * floatHeight * 0.8f;
			float sag = lineSag * (1f - duel.Tension);
			for (int i = 0; i < LINE_POINTS; i++)
			{
				float ratio = i / (LINE_POINTS - 1f);
				fishingLine.SetPosition(i, Vector3.Lerp(from, to, ratio) + Vector3.down * (Mathf.Sin(ratio * Mathf.PI) * sag));
			}
		}

		private void FinishFishing()
		{
			resultTimer = resultSeconds;
			switch (duel.Phase)
			{
				case FishingPhase.Caught:
					float size = Sim.Land(duel.Fish, duel.IsPerfect, out bool isRecord);
					UiSfx.Play(duel.IsPerfect ? UiSfxEvent.LevelUp : UiSfxEvent.Purchase);
					string text = $"{size}cm{(isRecord ? ", 가장 큰 것" : string.Empty)}{(duel.IsPerfect ? ", 완벽" : string.Empty)}";
					manager.Hud.Notice(duel.Fish.Name, text, Art.IconOf(Catalog.Get<GoodsDef>(duel.Fish.Goods)));
					break;
				case FishingPhase.Snapped:
					CombatSfx.Play(CombatSfxEvent.FishingMiss, bobber.transform.position);
					Say("실이 끊어졌습니다.");
					break;
				default:
					CombatSfx.Play(CombatSfxEvent.FishingMiss, bobber.transform.position);
					Say("놓쳤습니다.");
					break;
			}
			manager.Hud.ShowDuel(false);
		}
	}
}
