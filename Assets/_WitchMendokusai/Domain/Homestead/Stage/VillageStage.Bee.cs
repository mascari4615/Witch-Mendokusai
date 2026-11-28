using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 벌통. 몸은 꾸밈 (RefreshDecor) 이 짓고, 여기는 누르는 자리와 꿀 거두기
	public sealed partial class VillageStage
	{
		// 마을을 새로 지으면 ClearBodies 가 비움. 몸 (outdoor) 이 같이 사라지기 때문
		private readonly Dictionary<int, HomesteadSpot> hiveSpots = new();

		// 밭 칸이 놓인 마을 칸. 벌통이 둘레의 작물 꽃을 셀 때 씀
		private IEnumerable<(int X, int Z)> PlotCells()
		{
			for (int i = 0; i < PLOT_MAX; i++)
			{
				Vector3 position = PlotPosition(i);
				yield return (Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.z));
			}
		}

		private void RefreshHives()
		{
			HashSet<int> seen = new();
			foreach (PlacedPiece hive in Sim.Hives())
			{
				int uid = hive.Uid;
				seen.Add(uid);
				if (hiveSpots.TryGetValue(uid, out HomesteadSpot spot) == false)
				{
					spot = HomesteadSpot.Create(outdoor, CenterOf(hive), "HiveSpot", () => HiveName(uid), () => HiveRole(uid), () => UseHive(uid), () => HiveVerb(uid));
					hiveSpots[uid] = spot;
				}
				spot.transform.localPosition = CenterOf(hive);
			}
			foreach (int uid in new List<int>(hiveSpots.Keys))
			{
				if (seen.Contains(uid))
					continue;
				Destroy(hiveSpots[uid].gameObject);
				hiveSpots.Remove(uid);
			}
		}

		private PlacedPiece HiveOf(int uid) => Sim.State.Village.Find(piece => piece.Uid == uid);

		private string HiveName(int uid) => $"벌통 ({Sim.HoneyOf(HiveOf(uid)).Name})";

		private string HiveRole(int uid)
		{
			PlacedPiece hive = HiveOf(uid);
			return Sim.IsHiveFull(hive) ? "꿀 거두기" : $"{HomesteadText.Minutes(Sim.HiveMinutesLeft(hive))} 뒤에 참";
		}

		private string HiveVerb(int uid) => Sim.IsHiveFull(HiveOf(uid)) ? "거두기" : "보기";

		private void UseHive(int uid)
		{
			PlacedPiece hive = HiveOf(uid);
			if (Sim.IsHiveFull(hive) == false)
			{
				Say($"{HomesteadText.Minutes(Sim.HiveMinutesLeft(hive))} 뒤에 {Sim.HoneyOf(hive).Name}이 찹니다.");
				return;
			}
			if (Report(Sim.TakeHoney(uid, out HoneyDef honey)) == false)
				return;
			UiSfx.Play(UiSfxEvent.Purchase);
			Say($"{honey.Name}과 밀랍을 거뒀습니다.");
			if (decorBodies.TryGetValue(uid, out (string Look, GameObject Body) decor) && decor.Body != null)
				StartCoroutine(Pop(decor.Body.transform));
		}
	}
}
