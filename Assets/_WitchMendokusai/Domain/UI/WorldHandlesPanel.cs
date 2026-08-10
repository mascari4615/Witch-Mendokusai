using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 게임 창에서 <b>상자를 여닫고 이름을 정하는</b> 최소 손잡이 (TASK-WM-217/218).
	///
	/// ★ 왜 필요한가 (실측 2026-08-10, 게임 창 게이트가 잡음): 상자와 이름은 세계에도 있고 웹 창에도
	///   있는데 <b>게임 창에는 손잡이가 없었다</b> — 게임에서 지은 상자를 게임에서 못 열었고,
	///   게임에서는 자기 이름을 정할 수 없었다. 그러면 「게임 창과 웹 창이 같이 논다」가 거짓이 된다.
	///
	/// 생김새는 <b>네모와 글자</b>뿐이다(그래픽은 나중). 지금 확인해야 할 것은 「되나」뿐이다.
	/// 세계에 안 붙어 있으면 스스로 숨는다 — 혼자 놀 때도 줄은 있으므로 대개 보인다.
	/// </summary>
	public sealed class WorldHandlesPanel : MonoBehaviour
	{
		private const float REFRESH_SECONDS = 0.5f;

		private VisualElement root;
		private Label chestLabel;
		private TextField nameField;
		private float refreshIn;

		/// <summary>스스로 선다 — 씬에 얹어야 켜지는 구조면 그 씬에서만 되는 기능이 된다.</summary>
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void StandUp()
		{
			GameObject holder = new GameObject(nameof(WorldHandlesPanel));
			DontDestroyOnLoad(holder);
			holder.AddComponent<WorldHandlesPanel>();
		}

		private void Update()
		{
			// UI 뿌리는 늦게 생길 수 있다 — 사용 시점에 다시 묻는다(init-order-ok: lazy resolve).
			if (root == null)
			{
				if (TryBuild() == false)
					return;
			}

			bool linked = WorldChestBridge.IsActive || WorldNameBridge.IsActive;
			root.style.display = linked ? DisplayStyle.Flex : DisplayStyle.None;
			if (linked == false)
				return;

			refreshIn -= Time.unscaledDeltaTime;
			if (refreshIn > 0f)
				return;

			refreshIn = REFRESH_SECONDS;
			DrawChest();

			// 세계가 부르는 이름이 정본이다 — 사람이 고쳐 쓰는 중이면 건드리지 않는다.
			if (nameField.focusController?.focusedElement != nameField && WorldNameBridge.IsActive)
				nameField.SetValueWithoutNotify(WorldNameBridge.Channel.MyName);
		}

		private bool TryBuild()
		{
			if (UIRoot.TryGetExistingInstance(out UIRoot ui) == false)
				return false;

			root = new VisualElement { name = nameof(WorldHandlesPanel) };
			root.style.position = Position.Absolute;
			root.style.right = 8f;
			root.style.top = 8f;
			root.style.width = 220f;
			root.style.paddingLeft = 6f;
			root.style.paddingRight = 6f;
			root.style.paddingTop = 6f;
			root.style.paddingBottom = 6f;
			root.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);

			nameField = new TextField("이름") { maxLength = 16 };
			root.Add(nameField);

			Button rename = new Button(() =>
			{
				// 되나 안 되나는 세계가 본다 — 짧거나 길거나 남과 겹치면 거절 이유가 온다.
				if (WorldNameBridge.IsActive)
					WorldNameBridge.Channel.Rename(nameField.value);
			})
			{ text = "이렇게 불러" };

			root.Add(rename);

			chestLabel = new Label("상자: 가까이 가서 열어라");
			chestLabel.style.whiteSpace = WhiteSpace.Normal;
			chestLabel.style.marginTop = 6f;
			root.Add(chestLabel);

			Button open = new Button(() =>
			{
				if (WorldChestBridge.IsActive == false)
					return;

				if (WorldChestBridge.Channel.TryOpenNearby() == false)
					chestLabel.text = "가까운 상자가 없다 — 짓거나 다가가라";
			})
			{ text = "가까운 상자 열기" };

			root.Add(open);

			Button putAll = new Button(() =>
			{
				// 지금 든 것 하나를 넣어 본다 — 무엇이 얼마나 들어갈지는 세계가 정한다.
				if (WorldChestBridge.IsActive && TryFirstCarried(out int itemId))
					WorldChestBridge.Channel.Put(itemId, 1);
			})
			{ text = "하나 넣기" };

			root.Add(putAll);

			Button takeOne = new Button(() =>
			{
				IReadOnlyList<ChestSlot> inside = WorldChestBridge.IsActive
					? WorldChestBridge.Channel.Contents
					: null;

				if (inside != null && inside.Count > 0)
					WorldChestBridge.Channel.Take(inside[0].ItemId, 1);
			})
			{ text = "하나 꺼내기" };

			root.Add(takeOne);

			ui.HudLayer.Add(root);
			return true;
		}

		private void DrawChest()
		{
			if (WorldChestBridge.IsActive == false)
				return;

			IReadOnlyList<ChestSlot> inside = WorldChestBridge.Channel.Contents;
			if (inside.Count == 0)
			{
				chestLabel.text = "상자: 비었거나 아직 안 열었다";
				return;
			}

			System.Text.StringBuilder line = new System.Text.StringBuilder("상자:");
			for (int i = 0; i < inside.Count; i++)
			{
				ItemData item = SOHelper.Get<ItemData>(inside[i].ItemId);
				line.Append(' ')
					.Append(item == null ? "#" + inside[i].ItemId : item.Name)
					.Append('×')
					.Append(inside[i].Amount);
			}

			chestLabel.text = line.ToString();
		}

		/// <summary>가방에서 아무거나 하나 — 넣어 볼 것이 있나 보는 자리다.</summary>
		private static bool TryFirstCarried(out int itemId)
		{
			itemId = 0;
			if (SOManagerBridge.HasInstance == false)
				return false;

			// 세계가 아는 낱말표를 훑어 처음 든 것을 고른다 — 무엇을 넣을지 고르는 화면은 나중이다.
			if (SOManagerBridge.DataSOs.TryGetValue(typeof(ItemData), out Dictionary<int, DataSO> items) == false)
				return false;

			foreach (KeyValuePair<int, DataSO> entry in items)
			{
				if (SOManagerBridge.ItemInventory.CountByID(entry.Key) <= 0)
					continue;

				itemId = entry.Key;
				return true;
			}

			return false;
		}
	}
}
