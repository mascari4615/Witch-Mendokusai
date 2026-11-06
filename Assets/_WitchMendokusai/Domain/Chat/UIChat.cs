using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Cinemachine;
using FMODUnity;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;
using static WitchMendokusai.SOHelper;
using DG.Tweening;

namespace WitchMendokusai
{
	public class UIChat : MonoBehaviour
	{
		[SerializeField] private Image unitImage;
		[SerializeField] private TextMeshProUGUI unitName;
		[SerializeField] private TextMeshProUGUI lineText;
		[SerializeField] private CanvasGroup chatCanvasGroup;
		[SerializeField] private CanvasGroup bubbleCanvasGroup;

		[Header("Panel")]
		[SerializeField] private SurvivalUITheme theme;
		[SerializeField] private SurvivalUIKind dollKind = SurvivalUIKind.Count;
		[SerializeField] private SurvivalUIKind npcKind = SurvivalUIKind.Speed;
		[SerializeField] private RectTransform panelRoot;
		[SerializeField] private GameObject portraitRoot;
		[SerializeField] private Image portraitDiamond;
		[SerializeField] private Image portraitLine;
		[SerializeField] private RectTransform nameBlock;
		[SerializeField] private Image nameBand;
		[SerializeField] private GameObject nextMark;
		[SerializeField] private UIDialogueChoiceList choiceList;
		[SerializeField] private float nameXWithPortrait = 246f;
		[SerializeField] private float nameXWithoutPortrait = 38f;
		[SerializeField] private float lineXWithPortrait = 256f;
		[SerializeField] private float lineXWithoutPortrait = 65f;
		[SerializeField] private float panelRiseOffset = 26f;
		[SerializeField] private float panelRiseDuration = 0.2f;
		[SerializeField] private float speakerSlideOffset = 16f;
		[SerializeField] private float speakerSlideDuration = 0.14f;
		[SerializeField] private float portraitSquashDuration = 0.09f;

		[Header("Chat Feel")]
		// 한 글자씩 찍히는 간격. 대화의 말투·호흡을 정하는 값이라 눈으로 맞춰야 한다.
		[SerializeField] private float typingCharDelay = 0.05f;
		// 말풍선이 화면 가장자리에서 유지할 여백.
		[SerializeField] private float bubbleScreenPadding = 30f;
		// 말풍선을 대상 머리 위로 얼마나 띄울지.
		[SerializeField] private float bubbleVerticalOffset = 40f;

		private NPCObject curNPC;
		private int unitID;
		private int lastSpeakerID;
		private Action endAction;
		private Vector2 panelOrigin;

		private ChatManager chatManager;
		private CameraManager cameraManager;
		private DataManager dataManager;
		private InputManager inputManager;
		private PlayerProvider playerProvider;

		public static bool IsChatting { get; private set; } = false;

		[Inject]
		public void Construct(ChatManager chatManager, CameraManager cameraManager, DataManager dataManager, InputManager inputManager, PlayerProvider playerProvider)
		{
			this.chatManager = chatManager;
			this.cameraManager = cameraManager;
			this.dataManager = dataManager;
			this.inputManager = inputManager;
			this.playerProvider = playerProvider;
		}

		private void Start()
		{
			chatCanvasGroup.alpha = 0;
			bubbleCanvasGroup.alpha = 0;
			panelOrigin = panelRoot.anchoredPosition;
			nextMark.SetActive(false);
			choiceList.Hide();
		}

		// 선택지 표시. 프롬프트와 화자 이름이 없으면 해당 칸 비움
		public void PresentChoices(string speakerName, string prompt, IReadOnlyList<string> labels, Action<int> submit)
		{
			IsChatting = true;
			OpenPanel();
			portraitRoot.SetActive(false);
			nameBlock.gameObject.SetActive(string.IsNullOrEmpty(speakerName) == false);
			nameBlock.anchoredPosition = new Vector2(nameXWithoutPortrait, nameBlock.anchoredPosition.y);
			SetLineLeft(lineXWithoutPortrait);
			nameBand.color = theme.Style(npcKind).Dark;
			unitName.text = speakerName;
			lineText.text = prompt;
			nextMark.SetActive(false);
			choiceList.Show(labels, index =>
			{
				choiceList.Hide();
				ClosePanel();
				IsChatting = false;
				submit(index);
			});
		}

		private void OpenPanel()
		{
			chatCanvasGroup.DOKill();
			chatCanvasGroup.DOFade(1, panelRiseDuration).SetUpdate(true);
			panelRoot.DOKill();
			if (UIEffects.Enabled)
			{
				panelRoot.anchoredPosition = panelOrigin - new Vector2(0f, panelRiseOffset);
				panelRoot.DOAnchorPos(panelOrigin, panelRiseDuration).SetUpdate(true);
			}
			else
			{
				panelRoot.anchoredPosition = panelOrigin;
			}
		}

		private void ClosePanel()
		{
			chatCanvasGroup.DOKill();
			chatCanvasGroup.DOFade(0, panelRiseDuration).SetUpdate(true);
		}

		private void SetLineLeft(float left)
		{
			RectTransform lineRect = lineText.rectTransform;
			lineRect.offsetMin = new Vector2(left, lineRect.offsetMin.y);
		}

		private void ApplySpeaker(Unit unit, bool isDoll)
		{
			SurvivalUITheme.KindStyle style = theme.Style(isDoll ? dollKind : npcKind);
			bool hasPortrait = unit.Sprite != null;
			portraitRoot.SetActive(hasPortrait);
			portraitDiamond.color = style.Dark;
			portraitLine.color = style.Main;
			nameBand.color = style.Dark;
			nameBlock.gameObject.SetActive(true);
			SetLineLeft(hasPortrait ? lineXWithPortrait : lineXWithoutPortrait);
			float nameX = hasPortrait ? nameXWithPortrait : nameXWithoutPortrait;
			nameBlock.DOKill();
			unitImage.transform.DOKill();
			unitImage.transform.localScale = Vector3.one;
			bool hasChanged = lastSpeakerID != unit.ID;
			lastSpeakerID = unit.ID;
			if (hasChanged && UIEffects.Enabled)
			{
				nameBlock.anchoredPosition = new Vector2(nameX - speakerSlideOffset, nameBlock.anchoredPosition.y);
				nameBlock.DOAnchorPosX(nameX, speakerSlideDuration).SetUpdate(true);
				unitImage.transform.DOScaleY(0.9f, portraitSquashDuration).SetLoops(2, LoopType.Yoyo).SetUpdate(true);
			}
			else
			{
				nameBlock.anchoredPosition = new Vector2(nameX, nameBlock.anchoredPosition.y);
			}
		}

		public void StartChat(NPCObject npc, Action onChatFinished = null)
		{
			if (chatManager.TryGetChatData(npc.UnitData.ID.ToString(), out List<LineData> curChatData) == false)
			{
				Debug.LogWarning($"ChatData not found: {npc.UnitData.ID}");
				onChatFinished?.Invoke();
				return;
			}

			cameraManager.SetUICameraMode(UICameraMode.NPC, true);
			cameraManager.SetNPC(npc.transform);

			curNPC = npc;
			endAction = onChatFinished;

			IsChatting = true;

			StartCoroutine(ChatLoop(curChatData));
		}

		private IEnumerator ChatLoop(List<LineData> curChatData)
		{
			StartCoroutine(BubbleLoop());

			OpenPanel();
			bubbleCanvasGroup.DOFade(1, 0.2f);

			unitImage.color = Color.clear;
			unitName.text = string.Empty;
			lineText.text = string.Empty;
			nextMark.SetActive(false);
			lastSpeakerID = DataSO.NONE_ID;

			yield return null;

			unitImage.color = Color.white;

			foreach (LineData lineData in curChatData)
			{
				Unit unit = null;

				if (lineData.unitID == 0)
					unit = Get<Doll>(dataManager.CurDollID);
				else if (lineData.unitID == -1)
					unit = curNPC.Data;

				unitID = lineData.unitID;
				unitImage.sprite = unit.Sprite;
				ApplySpeaker(unit, lineData.unitID == 0);
				unitName.text = unit.Name;
				nextMark.SetActive(false);

				Coroutine coroutine = StartCoroutine(PrintLine(lineData));

				do yield return null;
				while (lineText.text != lineData.line && inputManager.IsAnyKeyPressedThisFrame == false);

				StopCoroutine(coroutine);
				lineText.text = lineData.line;
				nextMark.SetActive(true);

				do yield return null;
				while (inputManager.IsAnyKeyPressedThisFrame == false);
			}

			IsChatting = false;

			ClosePanel();
			bubbleCanvasGroup.DOFade(0, 0.2f);

			StopAllCoroutines();

			endAction?.Invoke();
		}

		private IEnumerator PrintLine(LineData lineData)
		{
			WaitForSecondsRealtime wait = new(typingCharDelay);
			StringBuilder s = new();

			s.Clear();
			foreach (char c in lineData.line)
			{
				s.Append(c);
				lineText.text = s.ToString();
				if (c != ' ')
					RuntimeManager.PlayOneShot("event:/SFX/Equip");
				yield return wait;
			}
		}

		public IEnumerator BubbleLoop()
		{
			RectTransform bubbleRectTransform = bubbleCanvasGroup.GetComponent<RectTransform>();
			float bubbleWidth = bubbleRectTransform.sizeDelta.x;

			while (true)
			{
				Vector3 targetPos = unitID == 0 ?
					playerProvider.Current.transform.position :
					curNPC.transform.position;
				bubbleCanvasGroup.transform.position = GetVec(targetPos + Vector3.up);

				yield return null;
			}

			Vector3 GetVec(Vector3 worldPos)
			{
				float bubbleHeight = bubbleRectTransform.sizeDelta.y;
				Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

				return new Vector3(
					Mathf.Clamp(screenPos.x, bubbleWidth / 2 + bubbleScreenPadding, Screen.width - bubbleWidth / 2 - bubbleScreenPadding),
					Mathf.Clamp(screenPos.y + bubbleVerticalOffset, bubbleScreenPadding, Screen.height - bubbleHeight - bubbleScreenPadding), 0);
			}
		}
	}
}
