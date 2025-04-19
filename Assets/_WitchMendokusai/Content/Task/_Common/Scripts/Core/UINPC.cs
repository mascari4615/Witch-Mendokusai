using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	[RequireComponent(typeof(CanvasGroup))]
	public class UINPC : UIPanel
	{
		private CanvasGroup canvasGroup;
		[SerializeField] private GameObject buttonsParent;
		[SerializeField] private UISlot talkOption;

		[SerializeField] private UISlot questOption;
		[SerializeField] private CanvasGroup questEachParent;
		[SerializeField] private UISlot[] questEachOptions;

		[SerializeField] private GameObject optionPrefab;
		[SerializeField] private Transform optionsParent;
		private readonly List<UISlot> options = new();

		public PanelType CurPanelType { get; private set; } = PanelType.None;
		private NPCObject curNPC = null;

		public override void Init()
		{
			canvasGroup = GetComponent<CanvasGroup>();

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
				for (int i = 0; i < (int)PanelType.Count; i++)
				{
					PanelType panelType = (PanelType)i;
					UIPanel panel = UIManager.Instance.PanelUIs[panelType];

					options.Add(Instantiate(optionPrefab, optionsParent).GetComponent<UISlot>());

					options[i].Init();
					options[i].SetSlotIndex(i);
					options[i].SetSlot(panel.PanelIcon, panel.Name, string.Empty);
					options[i].SetClickAction((slot) => { SetPanel(panelType); });
				}
			}

			// 초기화
			SetPanel(PanelType.None);
		}

		private void SetPanel(PanelType newPanelType)
		{
			if (CurPanelType == newPanelType)
				return;

			PanelType originPanelType = CurPanelType;
			CurPanelType = newPanelType;

			// 전 패널 처리
			{
				if (originPanelType == PanelType.None)
				{
					buttonsParent.SetActive(false);
				}
			}

			// 새 패널 처리
			{
				if (CurPanelType == PanelType.None)
				{
					CameraManager.Instance.SetCamera(CameraType.Dialogue);
					buttonsParent.SetActive(true);
				}
				else
				{
					CameraManager.Instance.SetChatCamera();
					UIManager.Instance.SetPanel(CurPanelType, curNPC);
				}
			}
		}

		protected override void OnOpen()
		{
			canvasGroup.SetVisible(false);

			NPC curNPCData = curNPC.UnitData as NPC;
			List<PanelType> panelTypes = curNPCData.GetPanelTypeList();
			for (int i = 0; i < options.Count; i++)
				options[i].gameObject.SetActive(panelTypes.Contains((PanelType)i));

			UpdateQuestButtons();
			questEachParent.gameObject.SetActive(false);

			Talk();
		}

		protected override void OnClose()
		{
			CameraManager.Instance.SetCamera(CameraType.Normal);
		}

		public override void UpdateUI()
		{
		}

		public override void SetNPC(NPCObject npc)
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
			List<QuestSO> questData = curNPCData.QuestData;
			int questCount = questData.Count;

			questOption.gameObject.SetActive(questCount > 0);
			for (int i = 0; i < questEachOptions.Length; i++)
			{
				if (i < questCount && QuestManager.Instance.GetQuestState(questData[i].ID) != QuestState.Completed)
				{
					questEachOptions[i].gameObject.SetActive(true);
					questEachOptions[i].SetSlot(questData[i]);

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
				canvasGroup.SetVisible(true);

				SetPanel(PanelType.None);
				buttonsParent.SetActive(true);
				talkOption.Select();
			});
		}

		private void SelectQuest(int index)
		{
			NPC curNPCData = curNPC.UnitData as NPC;
			QuestSO questData = curNPCData.QuestData[index];

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
			UIManager.Instance.SetPanel(PanelType.None);
		}
		#endregion
	}
}