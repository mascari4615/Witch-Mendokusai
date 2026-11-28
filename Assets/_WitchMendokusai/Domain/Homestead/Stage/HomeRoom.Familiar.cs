using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 욘의 집 사역마 자리. 깨운 사역마가 제자리 둘레를 거닐고, 온 알 하나가 그 곁에 놓임
	// - 알: 깨우기. 처음 보일 때 한 번 알림
	// - 사역마: 데려가는 것이 아니면 데려가기, 데려가는 것이면 하루 한 번 쓰다듬기 (반가워할 뿐)
	// - 모습은 임시: 양 그림에 색, 알은 달걀 아이콘에 색. 누구이고 어떤 모습인지는 사용자 결정 전
	public sealed partial class HomeRoom
	{
		private const string EGG_SEEN = "familiar_egg:";
		private const string TEMP_FAMILIAR_FRAMES = "sheep";
		private const string TEMP_EGG_ICON = "egg";

		[Header("Familiar")]
		[Tooltip("사역마 자리. 가구 칸 가운데 (FurnitureArea) 에서 떨어진 자리")]
		[SerializeField] private Vector3 familiarArea = new(1.4f, 0f, -1.2f);
		[Tooltip("사역마끼리 옆으로 벌린 간격 (m)")]
		[SerializeField] private float familiarSpacing = 0.7f;
		[Tooltip("사역마 그림 높이 (m)")]
		[SerializeField] private float familiarHeight = 0.45f;
		[Tooltip("알 그림 높이 (m)")]
		[SerializeField] private float eggHeight = 0.35f;
		[Tooltip("알이 들썩이는 높이 (m)")]
		[SerializeField] private float eggBob = 0.03f;
		[Tooltip("제자리에서 거니는 반경 (m)")]
		[SerializeField] private float familiarWander = 0.25f;
		[Tooltip("임시 색. 사역마 자료 순서대로")]
		[SerializeField] private Color[] familiarTints = { new(0.55f, 0.85f, 0.5f), new(1f, 0.6f, 0.35f), new(1f, 0.92f, 0.45f) };

		private readonly Dictionary<string, VillageWalker> familiars = new();
		private Transform familiarRoot;
		private GameObject eggObject;
		private string shownEgg = string.Empty;

		private void BuildFamiliars()
		{
			familiarRoot = new GameObject("Familiars").transform;
			familiarRoot.SetParent(house.FurnitureArea, false);
			familiarRoot.localPosition = familiarArea;
		}

		private void RefreshFamiliars()
		{
			// 세이브를 다시 읽으면 깬 목록이 줄 수 있음. 없어진 것은 치움
			foreach (string shown in new List<string>(familiars.Keys))
			{
				if (Sim.IsHatched(shown))
					continue;
				Destroy(familiars[shown].gameObject);
				familiars.Remove(shown);
			}
			foreach (FamiliarState state in Sim.State.Familiars)
			{
				if (familiars.ContainsKey(state.Id))
					continue;
				FamiliarDef familiar = Sim.Catalog.Get<FamiliarDef>(state.Id);
				VillageWalker walker = VillageWalker.CreateAnimal(familiarRoot, SlotOf(familiar), HomesteadArt.Instance.FramesOf(TEMP_FAMILIAR_FRAMES, 0), familiarWander);
				walker.transform.localScale = Vector3.one * (familiarHeight / walker.GetComponentInChildren<SpriteRenderer>().sprite.bounds.size.y);
				Tint(walker.gameObject, familiar);
				HomesteadSpot.Attach(walker.gameObject, () => familiar.Name, () => FamiliarRole(familiar), () => UseFamiliar(familiar, walker), () => FamiliarVerb(familiar));
				familiars.Add(state.Id, walker);
			}
			RefreshEgg();
		}

		private void RefreshEgg()
		{
			FamiliarDef egg = Sim.NextEgg();
			string wanted = egg == null ? string.Empty : egg.Id;
			if (wanted == shownEgg)
				return;
			shownEgg = wanted;
			if (eggObject != null)
				Destroy(eggObject);
			eggObject = null;
			if (egg == null)
				return;
			GoodsDef eggLook = Sim.Catalog.Get<GoodsDef>(TEMP_EGG_ICON);
			HomesteadSprite sprite = HomesteadSprite.Create(familiarRoot, SlotOf(egg), HomesteadArt.Instance.IconOf(eggLook), eggHeight, eggBob);
			eggObject = sprite.gameObject;
			Tint(eggObject, egg);
			HomesteadSpot.Attach(eggObject, () => "알", () => "무엇이 나올까", () => HatchEgg(egg), () => "깨우기");
			if (Sim.State.Seen.Contains(EGG_SEEN + egg.Id))
				return;
			Sim.State.Seen.Add(EGG_SEEN + egg.Id);
			manager.Save();
			manager.Hud.Notice("집에 알이 왔습니다", "사역마 자리에서 깨울 수 있습니다", HomesteadArt.Instance.IconOf(eggLook));
		}

		// 자료 순서대로 옆으로 한 칸씩. 알도 그 사역마가 설 자리에 놓임
		private Vector3 SlotOf(FamiliarDef familiar)
		{
			int index = Sim.Catalog.Familiars.IndexOf(familiar);
			float middle = (Sim.Catalog.Familiars.Count - 1) * 0.5f;
			return new Vector3((index - middle) * familiarSpacing, 0f, 0f);
		}

		private void Tint(GameObject target, FamiliarDef familiar)
		{
			int index = Sim.Catalog.Familiars.IndexOf(familiar);
			Color tint = familiarTints[index % familiarTints.Length];
			foreach (SpriteRenderer spriteRenderer in target.GetComponentsInChildren<SpriteRenderer>(true))
				spriteRenderer.color = tint;
		}

		private string FamiliarRole(FamiliarDef familiar)
		{
			if (Sim.IsTakenAlong(familiar.Id) == false)
				return $"던전 줍기 반경 +{familiar.Pickup}%";
			return $"데려가는 사역마, 줍기 반경 +{familiar.Pickup}%";
		}

		private string FamiliarVerb(FamiliarDef familiar)
		{
			if (Sim.IsTakenAlong(familiar.Id) == false)
				return "데려가기";
			return Sim.CanPetFamiliar(familiar.Id) ? "쓰다듬기" : string.Empty;
		}

		private void UseFamiliar(FamiliarDef familiar, VillageWalker walker)
		{
			if (Sim.IsTakenAlong(familiar.Id) == false)
			{
				if (Sim.TakeAlong(familiar.Id).Ok == false)
				{
					UiSfx.Play(UiSfxEvent.Blocked);
					return;
				}
				manager.Save();
				UiSfx.Play(UiSfxEvent.Confirm);
				uiManager.SpeechBubble.Show(walker.transform, $"다음 던전에 {familiar.Name} 이 따라옵니다.");
				walker.Hop();
				return;
			}
			if (Sim.PetFamiliar(familiar.Id).Ok == false)
			{
				UiSfx.Play(UiSfxEvent.Blocked);
				uiManager.SpeechBubble.Show(walker.transform, "오늘은 이미 쓰다듬었습니다.");
				return;
			}
			manager.Save();
			UiSfx.Play(UiSfxEvent.Confirm);
			walker.Hop();
		}

		private void HatchEgg(FamiliarDef egg)
		{
			if (Sim.Hatch(egg.Id).Ok == false)
			{
				UiSfx.Play(UiSfxEvent.Blocked);
				return;
			}
			manager.Save();
			UiSfx.Play(UiSfxEvent.LevelUp);
			manager.Hud.Notice($"{egg.Name} 이 깨어났습니다", Sim.IsTakenAlong(egg.Id) ? $"던전에 따라와 줍기 반경 +{egg.Pickup}%" : "곁을 쓰다듬거나 데려갈 수 있습니다", HomesteadArt.Instance.IconOf(Sim.Catalog.Get<GoodsDef>(TEMP_EGG_ICON)));
		}
	}
}
