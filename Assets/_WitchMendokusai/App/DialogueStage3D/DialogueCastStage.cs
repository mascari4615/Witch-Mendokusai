using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using WitchMendokusai.SequenceLibrary;

namespace WitchMendokusai.DialogueStage3D
{
	// 대화 `@무대 3D` 의 구현. 대화 변경분 (등장, 이동, 화자, 몸짓, 샷) 을 3D 배우와 카메라로 표현.
	// 게임 월드와 떨어진 자리에 무대를 세우고 화면을 넘겨받음. 대화 코어는 DialogueCastStageBridge 로만 호출
	public sealed partial class DialogueCastStage : MonoBehaviour, IDialogueCastStage
	{
		private static readonly DialogueStageSlot[] SLOT_ORDER =
		{
			DialogueStageSlot.FarLeft, DialogueStageSlot.Left, DialogueStageSlot.Center, DialogueStageSlot.Right, DialogueStageSlot.FarRight,
		};

		private readonly ScreenTakeover takeover = new ScreenTakeover("3D 무대");
		private readonly Dictionary<string, DialogueCastActor> actors = new Dictionary<string, DialogueCastActor>();
		private readonly Dictionary<string, DialogueStageSlot> slots = new Dictionary<string, DialogueStageSlot>();
		private DialogueCastStageSettings settings;
		private GameObject stageRoot;
		private DialogueCastCamera stageCamera;
		private string speaker;
		private string shotTarget;
		private bool hasEstablished;

		public bool IsShowing => stageRoot != null && takeover.IsHeld;

		public IReadOnlyList<string> GestureNames
		{
			get
			{
				List<string> names = new List<string>();
				foreach (DialogueCastStageSettings.GestureEntry entry in Settings.Gestures)
					names.Add(entry.Name);
				return names;
			}
		}

		private DialogueCastStageSettings Settings => settings != null ? settings : settings = DialogueCastStageSettings.Load();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void Create()
		{
			GameObject host = new GameObject("Dialogue cast stage");
			DontDestroyOnLoad(host);
			host.AddComponent<DialogueCastStage>();
		}

		private void Awake()
		{
			DialogueCastStageBridge.Register(this);
			SceneManager.sceneLoaded += OnSceneLoaded;
		}

		public bool TryShow(out string problem)
		{
			try
			{
				takeover.EnsureFree();
				if (stageRoot == null)
					BuildStage();
				takeover.Take();
				hasEstablished = false;
				problem = null;
				return true;
			}
			catch (Exception exception)
			{
				Hide();
				problem = exception.GetType().Name + ": " + exception.Message;
				return false;
			}
		}

		public void Apply(DialogueStageDelta delta, string lineText)
		{
			if (IsShowing == false)
				return;

			bool castChanged = SyncActors(delta);
			string previousSpeaker = speaker;
			speaker = delta.Speaker != null && actors.ContainsKey(delta.Speaker) ? delta.Speaker : null;
			if (lineText != null && speaker != null)
			{
				foreach (DialogueCastActor actor in actors.Values)
				{
					if (actor.ActorName == speaker) actor.Speak(lineText);
					else actor.StopSpeaking();
				}
			}
			PlayMotions(delta);

			if (delta.ShotChanged)
			{
				shotTarget = delta.ShotTarget;
				// 블렌드는 카메라가 두 배우 사이를 지나며 앞사람 몸을 통과할 수 있어 컷
				if (shotTarget != null)
				{
					stageCamera.Cut(ShotOn(shotTarget, delta.Shot));
					return;
				}
			}
			if (shotTarget != null)
				return;

			// 처음과 출연진이 바뀐 때는 성립 샷, 화자가 바뀌면 화자 샷으로 컷
			if (hasEstablished == false || (castChanged && speaker == null))
			{
				stageCamera.Cut(WideShot());
				hasEstablished = true;
			}
			else if (speaker != null && (speaker != previousSpeaker || castChanged))
			{
				stageCamera.Cut(ShotOn(speaker, DialogueStageShot.Medium));
			}
			else if (speaker == null && previousSpeaker != null)
			{
				stageCamera.Cut(WideShot());
			}
		}

		public void Hide()
		{
			foreach (DialogueCastActor actor in actors.Values)
			{
				if (actor != null)
					Destroy(actor.gameObject);
			}
			actors.Clear();
			slots.Clear();
			speaker = null;
			shotTarget = null;
			if (stageRoot != null)
				Destroy(stageRoot);
			stageRoot = null;
			stageCamera = null;
			takeover.Release();
		}

		// 씬 통째 전환이면 넘겨받은 카메라의 주인이 사라지므로 먼저 해제
		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			if (mode != LoadSceneMode.Single || IsShowing == false)
				return;
			Debug.LogWarning($"[{nameof(DialogueCastStage)}] 씬 전환으로 3D 무대를 닫음");
			Hide();
		}

		private void OnDestroy()
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
			DialogueCastStageBridge.Clear(this);
			Hide();
		}

		private void PlayMotions(DialogueStageDelta delta)
		{
			foreach (DialogueStageActorMotion motion in delta.Motions)
			{
				if (actors.TryGetValue(motion.Actor, out DialogueCastActor actor) == false)
					continue;
				switch (motion.Motion)
				{
					case DialogueStageMotion.Nod: actor.Nod(); break;
					case DialogueStageMotion.Bob: actor.Bob(); break;
					case DialogueStageMotion.Hop: PlayGesture(actor, Settings.HopGesture); break;
					case DialogueStageMotion.Shake: PlayGesture(actor, Settings.ShakeGesture); break;
				}
			}
			foreach (DialogueStageActorGesture gesture in delta.Gestures)
			{
				if (actors.TryGetValue(gesture.Actor, out DialogueCastActor actor))
					PlayGesture(actor, gesture.GestureName);
				else
					Debug.LogError($"[{nameof(DialogueCastStage)}] @동작 {gesture.Actor} {gesture.GestureName}: 무대에 없는 인물. 먼저 @등장");
			}
		}

		private void PlayGesture(DialogueCastActor actor, string gestureName)
		{
			AnimationClip clip = Settings.FindGesture(gestureName);
			if (clip == null)
			{
				Debug.LogError($"[{nameof(DialogueCastStage)}] 모르는 동작: \"{gestureName}\". 있는 이름: {string.Join(", ", GestureNames)}");
				return;
			}
			actor.PlayGesture(clip);
		}
	}
}
