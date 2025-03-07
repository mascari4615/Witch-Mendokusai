using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UINPC : UIPanel
	{
		private CanvasGroup canvasGroup;
		[SerializeField] private GameObject buttonsParent;
		[SerializeField] private UISlot talkOption;

		[SerializeField] private UISlot questOption;
		[SerializeField] private CanvasGroup questEachParent;
		[SerializeField] private UISlot[] questEachOptions;

		[SerializeField] private UISlot[] options;

		public NPCType CurPanelType { get; private set; }
		private readonly Dictionary<NPCType, UINPCPanel> panelUIs = new();
		private NPCObject curNPC;

		public override void Init()
		{
			canvasGroup = GetComponent<CanvasGroup>();

			// 패널 초기화
			{
				panelUIs[NPCType.Shop] = FindFirstObjectByType<UIShop>(FindObjectsInactive.Include);
				panelUIs[NPCType.DungeonEntrance] = FindFirstObjectByType<UIDungeonEntrance>(FindObjectsInactive.Include);
				panelUIs[NPCType.Pot] = FindFirstObjectByType<UIPot>(FindObjectsInactive.Include);
				panelUIs[NPCType.Anvil] = FindFirstObjectByType<UIAnvil>(FindObjectsInactive.Include);
				panelUIs[NPCType.Furnace] = FindFirstObjectByType<UIFurnace>(FindObjectsInactive.Include);
				panelUIs[NPCType.CraftingTable] = FindFirstObjectByType<UICraftingTable>(FindObjectsInactive.Include);

				foreach (UIPanel uiPanel in panelUIs.Values)
				{
					uiPanel.Init();
					uiPanel.SetActive(false);
				}
			}

			// 버튼 초기화
			{
				// 대화 버튼
				talkOption.Init();
				talkOption.SetClickAction((slot) => { Talk(); });

				// 퀘스트 버튼
				questOption.Init();
				questOption.SetClickAction((_) => { questEachParent.gameObject.SetActive(true); questEachOptions[0].Select(); });
				questOption.SetSelectAction((_) =>
				{
					if (questEachParent.gameObject.activeSelf)
						questEachParent.gameObject.SetActive(false);
				});
				questEachParent.gameObject.SetActive(false);
				for (int i = 0; i < questEachOptions.Length; i++)
				{
					questEachOptions[i].SetSlotIndex(i);
					questEachOptions[i].Init();
					questEachOptions[i].SetClickAction((slot) => { SelectQuest(slot.Index); });
				}

				// NPC 버튼
				for (int i = 0; i < (int)NPCType.Count; i++)
				{
					NPCType npcType = (NPCType)i;
					UIPanel panel = panelUIs[npcType];

					options[i].SetSlotIndex(i);
					options[i].SetSlot(panel.PanelIcon, panel.Name, string.Empty);
					options[i].Init();
					options[i].SetClickAction((slot) => { SetPanel(npcType); });
				}
			}

			// 초기화
			SetPanel(NPCType.None);
		}

		private void SetPanel(NPCType newPanelType)
		{
			if (CurPanelType == newPanelType)
				return;

			NPCType originPanelType = CurPanelType;
			CurPanelType = newPanelType;

			// 전 패널 처리
			{
				if (originPanelType == NPCType.None)
				{
					buttonsParent.SetActive(false);
				}
				else
				{
					panelUIs[originPanelType].SetActive(false);
				}
			}

			// 새 패널 처리
			{
				if (CurPanelType == NPCType.None)
				{
					CameraManager.Instance.SetCamera(CameraType.Dialogue);
					buttonsParent.SetActive(true);
				}
				else
				{
					CameraManager.Instance.SetChatCamera();
					panelUIs[CurPanelType].SetActive(true);
					panelUIs[CurPanelType].SetNPC(curNPC);
					panelUIs[CurPanelType].UpdateUI();
				}
			}
		}

		public override void OnOpen()
		{
			canvasGroup.alpha = 0;
			canvasGroup.interactable = false;
			canvasGroup.blocksRaycasts = false;

			NPC curNPCData = curNPC.UnitData as NPC;
			List<NPCType> npcTypes = curNPCData.GetNPCTypeList();
			for (int i = 0; i < options.Length; i++)
				options[i].gameObject.SetActive(npcTypes.Contains((NPCType)i));

			UpdateQuestButtons();
			questEachParent.gameObject.SetActive(false);

			Talk();
		}

		public override void OnClose()
		{
			CameraManager.Instance.SetCamera(CameraType.Normal);
		}

		public override void UpdateUI()
		{
		}

		public void SetNPC(NPCObject npc)
		{
			curNPC = npc;
		}

		private void UpdateQuestButtons()
		{
			Navigation navigation;

			navigation = questOption.Selectable.navigation;
			navigation.selectOnRight = questEachOptions[0].Selectable;
			questOption.Selectable.navigation = navigation;

			NPC curNPCData = curNPC.UnitData as NPC;
			List<QuestSO> questDatas = curNPCData.QuestDatas;
			int questCount = questDatas.Count;

			questOption.gameObject.SetActive(questCount > 0);
			for (int i = 0; i < questEachOptions.Length; i++)
			{
				if (i < questCount && QuestManager.Instance.GetQuestState(questDatas[i].ID) != QuestState.Completed)
				{
					questEachOptions[i].gameObject.SetActive(true);
					questEachOptions[i].SetSlot(questDatas[i]);

					navigation = questEachOptions[i].Selectable.navigation;
					navigation.selectOnLeft = questOption.Selectable;
					navigation.selectOnUp = i > 0 ? questEachOptions[i - 1].Selectable : null;
					navigation.selectOnDown = i < questCount - 1 ? questEachOptions[i + 1].Selectable : null;
					questEachOptions[i].Selectable.navigation = navigation;
				}
				else
				{
					questEachOptions[i].gameObject.SetActive(false);
				}
			}
		}

		#region Button Actions
		public void Talk()
		{
			buttonsParent.SetActive(false);
			UIManager.Instance.Chat.StartChat(curNPC, () =>
			{
				canvasGroup.alpha = 1;
				canvasGroup.interactable = true;
				canvasGroup.blocksRaycasts = true;

				SetPanel(NPCType.None);
				buttonsParent.SetActive(true);
				talkOption.Select();
			});
		}

		private void SelectQuest(int index)
		{
			NPC curNPCData = curNPC.UnitData as NPC;
			QuestSO questData = curNPCData.QuestDatas[index];

			QuestState state = QuestManager.Instance.GetQuestState(questData.ID);
			switch (state)
			{
				case QuestState.Locked:
					QuestManager.Instance.UnlockQuest(questData);
					QuestManager.Instance.AddQuest(new RuntimeQuest(questData));
					break;
				case QuestState.Unlocked:
					// TODO: 퀘스트 진행 중 대사 출력
					break;
				case QuestState.Completed:
				default:
					Debug.LogError($"Invalid Quest State : {state}");
					break;
			}

			// HACK:
			Invoke(nameof(Close), .1f);
		}

		public void Close()
		{
			UIManager.Instance.SetOverlay(MPanelType.None);
		}
		#endregion
	}
}