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
		[SerializeField] private Vector3 streamFrom = new(24f, 0f, -5f);
		[SerializeField] private Vector3 streamTo = new(33f, 0f, 6f);
		[SerializeField] private float streamWidth = 3.6f;
		[SerializeField] private Vector3 streamSpot = new(24.4f, 0f, -1.4f);
		[SerializeField] private float resultSeconds = 1.6f;
		[SerializeField] private float floatHeight = 0.5f;
		[SerializeField] private float lineWidth = 0.02f;
		[SerializeField] private float lineSag = 0.5f;
		[SerializeField] private Vector3 rodTip = new(0.4f, 0.9f, 0f);

		private FishingDuel duel;
		private string fishingWater;
		private bool reelHeld;
		private float resultTimer;
		private HomesteadSprite bobber;
		private LineRenderer fishingLine;
		private GameObject streamSpotObject;

		private void BuildWater()
		{
			ground.BuildWaterSurface(outdoor, Art.Water);
			foreach ((string key, float angle, float offset, float yaw) in shoreDressing)
			{
				Vector3 foot = ground.ShorePoint(pondCenter, angle * Mathf.Deg2Rad, offset);
				Models.Build(key, outdoor, foot, yaw);
			}
			foreach ((Vector2 center, float radius) in StreamPools())
			{
				GameObject bank = new("StreamBlock");
				bank.transform.SetParent(outdoor, false);
				bank.transform.localPosition = new Vector3(center.x, 0f, center.y);
				CapsuleCollider wall = bank.AddComponent<CapsuleCollider>();
				wall.radius = radius - 0.5f;
				wall.height = 4f;
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

		// 물가 돌과 갈대는 몇 무더기로만. 둘레를 다 두르지 않음
		private static readonly (string Key, float Angle, float Offset, float Yaw)[] shoreDressing =
		{
			("BankStones", 62f, -0.2f, 20f), ("BankStones", 232f, -0.3f, 200f), ("BankStones", 318f, 0f, 120f),
			("Reeds", 84f, -0.5f, 0f), ("Reeds", 100f, -0.4f, 70f), ("Reeds", 250f, -0.5f, 30f), ("Reeds", 336f, -0.4f, 140f),
			("LilyPads", 300f, -2.6f, 0f), ("LilyPads", 72f, -2.4f, 80f),
		};

		private void MarkWater()
		{
			ground.AddWater(new Vector2(pondCenter.x, pondCenter.z), pondRadius + 0.8f);
			foreach ((Vector2 center, float radius) in StreamPools())
				ground.AddWater(center, radius);
		}

		private IEnumerable<(Vector2 Center, float Radius)> StreamPools()
		{
			Vector3 along = streamTo - streamFrom;
			int steps = Mathf.CeilToInt(along.magnitude / (streamWidth * 0.25f));
			for (int i = 0; i <= steps; i++)
			{
				float ratio = i / (float)steps;
				Vector3 side = new Vector3(-along.z, 0f, along.x).normalized * Mathf.Sin(ratio * 5f) * 0.8f;
				Vector3 point = streamFrom + along * ratio + side;
				yield return (new Vector2(point.x, point.z), streamWidth * 0.5f);
			}
		}

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
