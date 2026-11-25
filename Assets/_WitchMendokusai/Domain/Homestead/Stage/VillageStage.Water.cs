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
		private const int POND_WALLS = 44;

		[Header("Water")]
		[SerializeField] private Vector3 streamFrom = new(24f, 0f, -5f);
		[SerializeField] private Vector3 streamTo = new(33f, 0f, 6f);
		[SerializeField] private float streamWidth = 3.6f;
		[SerializeField] private Vector3 streamSpot = new(24.4f, 0f, -1.4f);
		[SerializeField] private float dockHalfWidth = 0.72f;
		[SerializeField] private float dockLength = 5.5f;
		[SerializeField] private float wallHeight = 3f;
		[SerializeField] private float resultSeconds = 1.6f;
		[Tooltip("찌가 떨어지는 자리. 못 가운데와 개울 자리에서")]
		[SerializeField] private Vector3 pondCast = new(0f, -0.16f, 0.6f);
		[SerializeField] private Vector3 streamCast = new(2f, -0.16f, 0.8f);

		private FishingDuel duel;
		private string fishingWater;
		private bool reelHeld;
		private float resultTimer;
		private VillageFishingRig rig;
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
			BuildPondWalls();
			rig = VillageFishingRig.Create(outdoor);
			streamSpotObject = HomesteadSpot.Create(outdoor, streamSpot, "StreamSpot", () => "개울", () => "낚시", () => StartFishing(STREAM)).gameObject;
		}

		// 물가를 따라 선 보이지 않는 벽. 나루가 놓인 쪽은 열고 나루 양옆과 끝을 막음
		private void BuildPondWalls()
		{
			Transform root = new GameObject("PondWalls").transform;
			root.SetParent(outdoor, false);
			PlacedPiece dock = Sim.PieceOf("dock");
			Vector3 foot = CenterOf(dock);
			Vector3 along = FacingOf(dock.Turn) * Vector3.back;
			Vector3 side = Vector3.Cross(Vector3.up, along);
			float radius = pondRadius - 0.2f;
			float span = radius * Mathf.PI * 2f / POND_WALLS;
			for (int i = 0; i < POND_WALLS; i++)
			{
				float angle = i / (float)POND_WALLS * Mathf.PI * 2f;
				Vector3 point = pondCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
				Vector3 offset = point - foot;
				bool isDock = Vector3.Dot(offset, along) is > 0f and < 6f && Mathf.Abs(Vector3.Dot(offset, side)) < dockHalfWidth + span * 0.5f;
				if (isDock == false)
					AddWall(root, point, new Vector3(span + 0.2f, wallHeight, 0.4f), Quaternion.Euler(0f, 90f - angle * Mathf.Rad2Deg, 0f));
			}
			Quaternion facing = Quaternion.LookRotation(along);
			for (int sign = -1; sign <= 1; sign += 2)
				AddWall(root, foot + along * (dockLength * 0.5f + 1.2f) + side * (sign * (dockHalfWidth + 0.2f)), new Vector3(0.4f, wallHeight, dockLength - 2.4f), facing);
			AddWall(root, foot + along * dockLength, new Vector3(dockHalfWidth * 2f + 0.8f, wallHeight, 0.4f), facing);
		}

		private void AddWall(Transform root, Vector3 position, Vector3 size, Quaternion rotation)
		{
			GameObject wall = new("Wall");
			wall.transform.SetParent(root, false);
			wall.transform.SetLocalPositionAndRotation(position + Vector3.up * (size.y * 0.5f), rotation);
			wall.AddComponent<BoxCollider>().size = size;
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
			Vector3 target = water == POND ? pondCenter + pondCast : streamSpot + streamCast;
			rig.Cast(playerProvider.CurrentObject.transform, transform.TransformPoint(target));
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
			if (rig != null)
				rig.Stop();
			if (manager.Hud != null)
				manager.Hud.ShowDuel(false);
		}

		private void StepFishing()
		{
			if (duel == null)
				return;
			rig.Step(duel);
			if (duel.IsOver)
			{
				resultTimer -= Time.deltaTime;
				if (resultTimer <= 0f)
					StopFishing();
				return;
			}
			FishingPhase before = duel.Phase;
			duel.Step(Time.deltaTime, reelHeld);
			manager.Hud.SetDuel(duel);
			if (duel.Phase == FishingPhase.Bite && before == FishingPhase.Waiting)
			{
				CombatSfx.Play(CombatSfxEvent.FishingBite, rig.FloatPosition);
				rig.Splash(rig.FloatPosition);
				uiManager.SpeechBubble.Show(playerProvider.Current.transform, "!", Catalog.Tuning.BiteWindow);
			}
			if (duel.IsOver)
				FinishFishing();
		}

		private void FinishFishing()
		{
			resultTimer = resultSeconds;
			switch (duel.Phase)
			{
				case FishingPhase.Caught:
					rig.Land(Art.IconOf(Catalog.Get<GoodsDef>(duel.Fish.Goods)));
					float size = Sim.Land(duel.Fish, duel.IsPerfect, out bool isRecord);
					UiSfx.Play(duel.IsPerfect ? UiSfxEvent.LevelUp : UiSfxEvent.Purchase);
					string text = $"{size}cm{(isRecord ? ", 가장 큰 것" : string.Empty)}{(duel.IsPerfect ? ", 완벽" : string.Empty)}";
					manager.Hud.Notice(duel.Fish.Name, text, Art.IconOf(Catalog.Get<GoodsDef>(duel.Fish.Goods)));
					break;
				case FishingPhase.Snapped:
					CombatSfx.Play(CombatSfxEvent.FishingMiss, rig.FloatPosition);
					rig.Lose();
					Say("실이 끊어졌습니다.");
					break;
				default:
					CombatSfx.Play(CombatSfxEvent.FishingMiss, rig.FloatPosition);
					rig.Lose();
					Say("놓쳤습니다.");
					break;
			}
			manager.Hud.ShowDuel(false);
		}
	}
}
