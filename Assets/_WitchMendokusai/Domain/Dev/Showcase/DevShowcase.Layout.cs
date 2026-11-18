using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 진열 격자 배치. 앞쪽으로 구역을 쌓고, 무기 시험대는 뒤쪽
	public sealed partial class DevShowcase
	{
		// 다음 줄을 놓을 앞쪽 거리
		private float cursorForward;

		private void Layout(Vector3 origin)
		{
			root = new GameObject(nameof(DevShowcase));
			GroundGenerator.SuppressScatter = true;
			activeSeconds = 0f;
			maintenanceElapsed = data.MaintenanceSeconds;
			statusElapsed = data.StatusRefreshSeconds;
			cursorForward = data.Origin.y;

			Monster dummy = ResolveDummy();
			LayoutStatusCells(origin, dummy);
			LayoutMonsters(origin);
			LayoutBreakables(origin);
			LayoutDrops(origin);
			LayoutBench(origin, dummy);
		}

		private Monster ResolveDummy()
		{
			if (data.DummyMonster != null)
				return data.DummyMonster;
			List<Monster> monsters = DisplayMonsters();
			if (monsters.Count == 0)
				throw new System.InvalidOperationException("진열할 몬스터 없음");
			return monsters[0];
		}

		private Vector3 CellPosition(Vector3 origin, float right, float forward) => origin + new Vector3(right, 0f, forward);

		// 구역 제목을 놓고, 칸 수만큼 줄을 차지. 칸 하나의 자리를 돌려줌
		private List<Vector3> TakeRows(Vector3 origin, string title, int count)
		{
			List<Vector3> positions = new();
			if (count <= 0)
				return positions;

			AddLabel(CellPosition(origin, data.Origin.x, cursorForward), title, data.SectionLabelScale, data.SectionLabelColor, data.SectionLabelOffset);
			cursorForward += data.SectionTitleGap;
			int rows = (count + data.ColumnsPerRow - 1) / data.ColumnsPerRow;
			for (int index = 0; index < count; index++)
			{
				int row = index / data.ColumnsPerRow;
				int column = index % data.ColumnsPerRow;
				positions.Add(CellPosition(origin, data.Origin.x + column * data.CellSpacing, cursorForward + row * data.RowSpacing));
			}
			cursorForward += rows * data.RowSpacing + data.SectionGap;
			return positions;
		}

		private void LayoutStatusCells(Vector3 origin, Monster dummy)
		{
			List<Vector3> positions = TakeRows(origin, data.StatusSectionTitle, data.StatusCells.Count);
			for (int i = 0; i < positions.Count; i++)
			{
				DevShowcaseStatusCell cellData = data.StatusCells[i];
				monsterCells.Add(new MonsterCell { Monster = dummy, Home = positions[i], Statuses = cellData.Statuses });
				AddLabel(positions[i], cellData.Label, data.LabelScale, data.LabelColor, data.LabelOffset);
			}
		}

		private void LayoutMonsters(Vector3 origin)
		{
			List<Monster> monsters = DisplayMonsters();
			List<Vector3> positions = TakeRows(origin, data.MonsterSectionTitle, monsters.Count);
			for (int i = 0; i < positions.Count; i++)
			{
				Monster monster = monsters[i];
				monsterCells.Add(new MonsterCell { Monster = monster, Home = positions[i] });
				string label = monster.Type == MonsterType.Boss ? monster.Name + data.BossSuffix : monster.Name;
				AddLabel(positions[i], label, data.LabelScale, data.LabelColor, data.LabelOffset);
			}
		}

		// 프리팹에 MonsterObject 가 있는 몬스터만. 전부 보기면 ID 순
		private List<Monster> DisplayMonsters()
		{
			List<Monster> source = new();
			if (data.ShowAllMonsters)
			{
				SOHelper.ForEach<Monster>(monster => source.Add(monster));
				source.Sort((left, right) => left.ID.CompareTo(right.ID));
			}
			else
				source.AddRange(data.Monsters);

			List<Monster> result = new();
			foreach (Monster monster in source)
			{
				if (monster == null || data.HiddenMonsters.Contains(monster))
					continue;
				if (monster.Prefab == null || monster.Prefab.GetComponent<MonsterObject>() == null)
					continue;
				result.Add(monster);
			}
			return result;
		}

		private void LayoutBreakables(Vector3 origin)
		{
			BreakableSettings settings = LoadBreakableSettings();
			int variants = Mathf.Min(data.BreakableVariantsShown, settings.VariantsPerKind);
			if (settings.Kinds.Length == 0 || variants <= 0)
				return;

			// 종류 하나가 한 줄, 모양이 칸
			AddLabel(CellPosition(origin, data.Origin.x, cursorForward), data.BreakableSectionTitle, data.SectionLabelScale, data.SectionLabelColor, data.SectionLabelOffset);
			cursorForward += data.SectionTitleGap;
			for (int kindIndex = 0; kindIndex < settings.Kinds.Length; kindIndex++)
			{
				for (int variantIndex = 0; variantIndex < variants; variantIndex++)
				{
					Vector3 position = CellPosition(origin, data.Origin.x + variantIndex * data.CellSpacing, cursorForward);
					position.y = settings.GroundHeight;
					breakableCells.Add(new BreakableCell { KindIndex = kindIndex, VariantIndex = variantIndex, Position = position });
					AddLabel(position, $"{settings.Kinds[kindIndex].Name} {variantIndex + 1}", data.LabelScale, data.LabelColor, data.LabelOffset);
				}
				cursorForward += data.RowSpacing;
			}
			cursorForward += data.SectionGap;
		}

		private static BreakableSettings LoadBreakableSettings()
		{
			BreakableSettings settings = Resources.Load<BreakableSettings>(nameof(BreakableSettings));
			if (settings == null)
				throw new System.InvalidOperationException($"Resources/{nameof(BreakableSettings)} 없음");
			return settings;
		}

		private void LayoutDrops(Vector3 origin)
		{
			List<Vector3> positions = TakeRows(origin, data.DropSectionTitle, data.Drops.Count);
			for (int i = 0; i < positions.Count; i++)
			{
				dropCells.Add(new DropCell { Data = data.Drops[i], Position = positions[i] });
				AddLabel(positions[i], data.Drops[i].Label, data.LabelScale, data.LabelColor, data.LabelOffset);
			}
		}

		// 뒤쪽. 받침 격자 (맨손이 첫 칸) 를 한 줄 칸 수로 접고, 그 뒤에 허수아비 줄을 받침 폭에 고르게
		private void LayoutBench(Vector3 origin, Monster dummy)
		{
			List<EquipmentData> weapons = BenchWeapons();
			int padCount = weapons.Count + 1;
			int columns = Mathf.Min(padCount, data.ColumnsPerRow);
			int rows = (padCount + data.ColumnsPerRow - 1) / data.ColumnsPerRow;
			float padForward = data.BenchOrigin.y;
			AddLabel(CellPosition(origin, data.BenchOrigin.x, padForward + data.SectionTitleGap), data.WeaponSectionTitle, data.SectionLabelScale, data.SectionLabelColor, data.SectionLabelOffset);

			for (int index = 0; index < padCount; index++)
			{
				int row = index / data.ColumnsPerRow;
				int column = index % data.ColumnsPerRow;
				Vector3 position = CellPosition(origin, data.BenchOrigin.x + column * data.CellSpacing, padForward - row * data.RowSpacing);
				EquipmentData weapon = index == 0 ? null : weapons[index - 1];
				AddPad(position, weapon, weapon == null ? data.BareHandsLabel : weapon.Name);
			}

			float dummyForward = padForward - (rows - 1) * data.RowSpacing - data.BenchDummyDistance;
			float width = (columns - 1) * data.CellSpacing;
			for (int i = 0; i < data.BenchDummyCount; i++)
			{
				float ratio = data.BenchDummyCount == 1 ? 0.5f : (float)i / (data.BenchDummyCount - 1);
				Vector3 home = CellPosition(origin, data.BenchOrigin.x + width * ratio, dummyForward);
				monsterCells.Add(new MonsterCell { Monster = dummy, Home = home });
			}
		}

		// 진열과 시험 탭이 같이 씀. 목록이 비면 동작 오브젝트가 있는 장비 전부. 빈 줄은 건너뜀
		public List<EquipmentData> BenchWeapons()
		{
			List<EquipmentData> weapons = new();
			foreach (EquipmentData weapon in data.Weapons.Count > 0 ? data.Weapons : DevCombatTools.AllWeapons())
				if (weapon != null)
					weapons.Add(weapon);
			return weapons;
		}

		private void AddPad(Vector3 position, EquipmentData weapon, string label)
		{
			padCells.Add(new PadCell { Weapon = weapon, Position = position });

			GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			pad.name = "Pad " + label;
			// 이동을 막지 않게 충돌체 제거
			Object.Destroy(pad.GetComponent<Collider>());
			pad.transform.SetParent(root.transform, false);
			pad.transform.position = position;
			float diameter = data.PadRadius * 2f;
			pad.transform.localScale = new Vector3(diameter, data.PadThickness, diameter);
			if (data.PadMaterial != null)
				pad.GetComponent<MeshRenderer>().sharedMaterial = data.PadMaterial;

			AddLabel(position, label, data.LabelScale, data.LabelColor, data.PadLabelOffset);
		}

		// 글자와 글꼴만 빌리고 LifeLabel 의 빌보드는 뺌. 카메라 자리를 보면 화면 가장자리 글자가 기울어서, 카메라와 같은 회전으로 맞춤 (AlignLabels)
		private void AddLabel(Vector3 position, string text, float scale, Color color, Vector3 offset)
		{
			if (data.LabelPrefab == null)
				throw new System.InvalidOperationException($"{nameof(DevShowcaseSO)}.{nameof(DevShowcaseSO.LabelPrefab)} 미연결");
			LifeLabel label = Object.Instantiate(data.LabelPrefab, root.transform);
			label.name = "Label " + text;
			label.transform.position = position + offset;
			label.transform.localScale = Vector3.one * scale;
			label.SetStaticText(text);
			label.SetColor(color);
			labels.Add(label.transform);
			Object.Destroy(label);
		}
	}
}
