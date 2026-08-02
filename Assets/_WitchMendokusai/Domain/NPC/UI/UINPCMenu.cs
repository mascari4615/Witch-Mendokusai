using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace WitchMendokusai
{
	[RequireComponent(typeof(CanvasGroup))]
	public class UINPCMenu : UIPanel
	{
		public NPCPanelType CurPanelType { get; private set; } = NPCPanelType.None;

		[SerializeField] private GameObject buttonsParent;
		[SerializeField] private UISlot talkOption;

		[SerializeField] private UISlot questOption;
		[SerializeField] private CanvasGroup questEachParent;
		[SerializeField] private UISlot[] questEachOptions;

		[SerializeField] private GameObject optionPrefab;
		[SerializeField] private Transform optionsParent;
		private readonly List<UISlot> options = new();

		private CanvasGroup canvasGroup;
		private NPCObject curNPC = null;

		private UIManager uiManager;
		private CameraManager cameraManager;
		private ChatManager chatManager;
		private QuestManager questManager;

		public override bool IsFullscreen => true;

		[Inject]
		public void Construct(UIManager uiManager, CameraManager cameraManager, ChatManager chatManager, QuestManager questManager)
		{
			this.uiManager = uiManager;
			this.cameraManager = cameraManager;
			this.chatManager = chatManager;
			this.questManager = questManager;
		}

		protected override void OnInit()
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
				for (int i = 0; i < (int)NPCPanelType.Count; i++)
				{
					NPCPanelType panelType = (NPCPanelType)i;
					IUIPanel panel = uiManager.NPC.Panels[panelType];

					UISlot newSlot = Instantiate(optionPrefab, optionsParent).GetComponent<UISlot>();
					newSlot.Init();
					newSlot.SetSlotIndex((int)panelType);
					newSlot.SetSlot(panel.PanelIcon, panel.Name, string.Empty);
					newSlot.SetClickAction((slot) => { SetPanel(panelType); });

					options.Add(newSlot);
				}
			}

			// 초기화
			SetPanel(NPCPanelType.None);
		}

		private void SetPanel(NPCPanelType newPanelType)
		{
			if (CurPanelType == newPanelType)
				return;

			NPCPanelType originPanelType = CurPanelType;
			CurPanelType = newPanelType;

			// 전 패널 처리
			{
				if (originPanelType == NPCPanelType.None)
				{
					buttonsParent.SetActive(false);
				}
			}

			// 새 패널 처리
			{
				if (CurPanelType == NPCPanelType.None)
				{
					// NPC 버튼들 띄우기 (선택지)
					buttonsParent.SetActive(true);
				}
				else
				{
					// NPC UI 띄우기
					uiManager.NPC.SetPanel(CurPanelType, curNPC);
				}
			}
		}

		protected override void OnOpen()
		{
			canvasGroup.SetVisible(false);

			NPC curNPCData = curNPC.UnitData as NPC;
			List<NPCPanelType> panelTypes = curNPCData.GetPanelTypeList();
			for (int i = 0; i < options.Count; i++)
				options[i].gameObject.SetActive(panelTypes.Contains((NPCPanelType)options[i].Index)); // Index는 PanelType의 인덱스와 동일

			UpdateQuestButtons();
			questEachParent.gameObject.SetActive(false);

			cameraManager.SetUICameraMode(UICameraMode.NPC, true);
			cameraManager.SetSelecting(isSelecting: false, shouldAnimate: false);

			// 대화 불가 시 바로 선택 모드로 전환
			bool canTalk = chatManager.TryGetChatData(curNPC.UnitData.ID.ToString(), out _);
			talkOption.gameObject.SetActive(canTalk);

			if (canTalk)
			{
				Talk();
			}
			else
			{
				Debug.LogWarning($"ChatData not found: {curNPC.UnitData.ID}");
				OnChatFinish();
			}
		}

		protected override void OnClose()
		{
			cameraManager.SetUICameraMode(UICameraMode.NPC, false);
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
				if (i < questCount && questManager.GetQuestState(questData[i].ID) != QuestState.Completed)
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
			cameraManager.SetSelecting(false);

			uiManager.Chat.StartChat(curNPC, OnChatFinish);
		}

		private void OnChatFinish()
		{
			canvasGroup.SetVisible(true);

			SetPanel(NPCPanelType.None);
			buttonsParent.SetActive(true);
			talkOption.Select();

			cameraManager.SetSelecting(true);
		}

		private void SelectQuest(int index)
		{
			NPC curNPCData = curNPC.UnitData as NPC;
			QuestSO questData = curNPCData.QuestData[index];

			QuestState state = questManager.GetQuestState(questData.ID);
			switch (state)
			{
				case QuestState.Locked:
					questManager.UnlockQuest(questData);
					questManager.AddQuest(RuntimeQuestFactory.FromQuestSO(questData, questManager.CreateCriteriaContext()));
					break;
				case QuestState.Unlocked:
					// TODO: 퀘스트 진행 중 대사 출력
					break;
				case QuestState.Completed:
				default:
					Debug.LogError($"Invalid Quest State : {state}");
					break;
			}

			Invoke(nameof(Close), .1f);
		}

		public void Close()
		{
			SetPanel(NPCPanelType.None);
		}
		#endregion
	}
}