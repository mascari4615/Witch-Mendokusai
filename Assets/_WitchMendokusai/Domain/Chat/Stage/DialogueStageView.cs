using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
	// 대화 연출 2D 어댑터. 조립과 수명, 나머지는 partial 조각
	public partial class DialogueStageView : MonoBehaviour, IDialogueStageHost
	{
		private const string USS_ROOT = "wm-dlg";
		private const string USS_OPEN = USS_ROOT + "--open";
		private const string USS_NO_FX = USS_ROOT + "--no-fx";
		private const string USS_CINEMA = USS_ROOT + "--cinema";
		private const string USS_CHOOSING = USS_ROOT + "--choosing";
		private const string USS_WINDOW_HIDDEN = USS_ROOT + "--window-hidden";

		private UIRoot uiRoot;
		private InputManager inputManager;
		private CameraManager cameraManager;
		private PlayerProvider playerProvider;
		private TimeManager timeManager;

		private DialogueStageSettings settings;
		private DialogueCameraDirector cameraDirector;
		private IDialogueStageControls controls;

		private VisualElement root;
		private VisualElement cast;
		private VisualElement fxLayer;
		private VisualElement flash;

		private readonly Dictionary<string, DialogueStageActorElement> actors = new();
		private readonly HashSet<string> warnedActors = new();

		private DialogueStagePresentation presentation = DialogueStagePresentation.World;
		private DialogueStagePresentation shownPresentation = DialogueStagePresentation.World;
		private string speaker;
		private string highlighted;
		private bool isSessionOpen;
		private bool isClosing;
		private float closeElapsed;
		private bool isSkipping;

		public bool IsHoldingInput => isSessionOpen && (presentation != DialogueStagePresentation.World || isChoosing);

		[Inject]
		public void Construct(UIRoot uiRoot, InputManager inputManager, CameraManager cameraManager, PlayerProvider playerProvider, TimeManager timeManager)
		{
			this.uiRoot = uiRoot;
			this.inputManager = inputManager;
			this.cameraManager = cameraManager;
			this.playerProvider = playerProvider;
			this.timeManager = timeManager;
			DialogueStageBridge.Register(this);
		}

		private void OnDestroy()
		{
			DialogueStageBridge.Clear(this);
			cameraDirector?.Release();
			SetHudHidden(false);
			SetTimePaused(false);
			SetBlurred(false);
			root?.RemoveFromHierarchy();
			ReleaseTextures();
		}

		private void EnsureBuilt()
		{
			if (root != null)
				return;

			settings = DialogueStageSettings.Load();
			cameraDirector = new DialogueCameraDirector(cameraManager, settings, ResolveWorldTarget);
			advancer.CharactersPerSecond = settings.CharactersPerSecond;
			advancer.AutoDelaySeconds = settings.AutoDelaySeconds;
			advancer.AutoSecondsPerCharacter = settings.AutoSecondsPerCharacter;

			TemplateContainer tree = settings.Layout.CloneTree();
			root = tree.Q<VisualElement>(USS_ROOT);
			root.style.display = DisplayStyle.None;
			root.styleSheets.Add(settings.Style);
			root.RemoveFromHierarchy();

			cast = root.Q<VisualElement>("wm-dlg-cast");
			fxLayer = root.Q<VisualElement>("wm-dlg-fx");
			flash = root.Q<VisualElement>("wm-dlg-flash");

			BuildWindow();
			BuildChoices();
			BuildTools();
			BuildFx();
			BuildBackdrop();

			root.RegisterCallback<PointerDownEvent>(OnRootPointerDown);
			uiRoot.WindowsLayer.Add(root);
		}

		public void Begin(IDialogueStageControls stageControls)
		{
			EnsureBuilt();
			controls = stageControls;
			isSessionOpen = true;
			isClosing = false;
			presentation = DialogueStagePresentation.World;
			shownPresentation = DialogueStagePresentation.World;
			speaker = null;
			highlighted = null;
			windowSwitchElapsed = -1f;
			advancer.AutoEnabled = false;
			root.EnableInClassList(USS_NO_FX, UIEffects.Enabled == false);
			root.RemoveFromClassList(USS_CINEMA);
			RefreshAutoButton();
			ClearActors();
			HideChoiceList();
			CloseLog();
			ApplyPresentationClass(DialogueStagePresentation.World);
			RefreshOpenState();
		}

		public void ApplyLine(DialogueStageDelta delta, string speakerName, string text)
		{
			EnsureSessionVisible();
			ApplyDelta(delta);
			if (presentation == DialogueStagePresentation.World)
				return;

			ShowLine(speakerName, text);
			if (isSkipping)
				advancer.Complete();
		}

		public void ApplyCues(DialogueStageDelta delta)
		{
			EnsureSessionVisible();
			ApplyDelta(delta);
		}

		public void Finish()
		{
			if (isSessionOpen == false)
				return;

			isSessionOpen = false;
			controls = null;
			isChoosing = false;
			HideChoiceList();
			CloseLog();
			cameraDirector.Release();
			SetHudHidden(false);
			if (UIEffects.Enabled == false || root.style.display == DisplayStyle.None)
			{
				CloseNow();
				return;
			}
			isClosing = true;
			closeElapsed = 0f;
			root.RemoveFromClassList(USS_OPEN);
			root.pickingMode = PickingMode.Ignore;
			RefreshStageMood();
		}

		public bool TryResolveWorldAnchor(string actorName, out Transform anchor)
		{
			EnsureBuilt();
			anchor = ResolveWorldTarget(actorName);
			return anchor != null;
		}

		private void EnsureSessionVisible()
		{
			EnsureBuilt();
			RefreshOpenState();
		}

		private void CloseNow()
		{
			isClosing = false;
			root.style.display = DisplayStyle.None;
			root.pickingMode = PickingMode.Ignore;
			root.RemoveFromClassList(USS_OPEN);
			ClearActors();
			ClearParticles();
			presentation = DialogueStagePresentation.World;
			shownPresentation = DialogueStagePresentation.World;
			RefreshStageMood();
		}

		// 말풍선 방식은 선택지가 없으면 화면 전체를 비움
		private void RefreshOpenState()
		{
			RefreshStageMood();
			bool needsScreen = presentation != DialogueStagePresentation.World || isChoosing;
			if (needsScreen == false && isClosing == false)
			{
				root.style.display = DisplayStyle.None;
				root.pickingMode = PickingMode.Ignore;
				root.RemoveFromClassList(USS_OPEN);
				SetHudHidden(false);
				return;
			}

			if (needsScreen)
			{
				isClosing = false;
				root.style.display = DisplayStyle.Flex;
				root.AddToClassList(USS_OPEN);
				root.pickingMode = PickingMode.Position;
				SetHudHidden(presentation != DialogueStagePresentation.World);
			}
		}

		private void SetHudHidden(bool isHidden)
		{
			UIHideWhileChatting.Request(this, isHidden);
			if (uiRoot != null && uiRoot.HudLayer != null)
				uiRoot.HudLayer.style.opacity = isHidden ? new StyleFloat(0f) : new StyleFloat(StyleKeyword.Null);
		}

		private Transform ResolveWorldTarget(string actorName)
		{
			if (string.IsNullOrEmpty(actorName))
				return null;

			if (DialogueSpeakerBridge.TryGetAnchor(actorName, out Transform registered))
				return registered;

			DialogueStageSettings.CastEntry entry = settings.FindCast(actorName);
			bool isPlayer = actorName == settings.PlayerActorName || (entry != null && entry.IsPlayerInWorld);
			if (isPlayer)
				return playerProvider.Current != null ? playerProvider.Current.transform : null;

			foreach (NPCObject npc in FindObjectsByType<NPCObject>())
			{
				if (npc.Data != null && npc.Data.Name == actorName)
					return npc.transform;
			}
			return null;
		}

		private void WarnOnce(string actorName, string message)
		{
			if (warnedActors.Add(actorName))
				Debug.LogWarning($"[{nameof(DialogueStageView)}] {message}: {actorName}");
		}

		private void Update()
		{
			if (root == null)
				return;

			float deltaTime = Time.unscaledDeltaTime;
			if (isClosing)
			{
				closeElapsed += deltaTime;
				if (closeElapsed >= settings.CloseSeconds)
					CloseNow();
			}

			if (root.style.display == DisplayStyle.None)
				return;

			TickActors(deltaTime);
			TickFx(deltaTime);
			TickWindow(deltaTime);
			TickBackdrop();

			if (isSessionOpen)
				TickInput(deltaTime);
		}
	}
}
