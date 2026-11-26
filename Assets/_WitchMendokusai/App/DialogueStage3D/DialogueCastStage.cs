using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using WitchMendokusai.SequenceLibrary;

namespace WitchMendokusai.DialogueStage3D
{
	// 대화 `@무대 3D|월드` 의 구현. 대화 변경분 (등장, 이동, 화자, 몸짓, 샷) 을 3D 인물과 카메라로 표현.
	// 3D 는 게임 월드와 떨어진 별도 무대 (Build.cs), 월드는 플레이하던 자리의 인물과 게임 카메라 (World.cs).
	// 대화 코어는 DialogueCastStageBridge 로만 호출
	public sealed partial class DialogueCastStage : MonoBehaviour, IDialogueCastStage
	{
		private readonly ScreenTakeover takeover = new ScreenTakeover("3D 무대");
		private readonly Dictionary<string, IDialogueCastPerformer> actors = new Dictionary<string, IDialogueCastPerformer>();
		private DialogueCastStageSettings settings;
		private DialogueCastCamera stageCamera;
		private DialogueStageSpace space = DialogueStageSpace.Flat;
		private string speaker;
		private string shotTarget;
		private bool hasEstablished;

		public bool IsShowing => space != DialogueStageSpace.Flat;

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

		public bool TryShow(DialogueStageSpace nextSpace, out string problem)
		{
			try
			{
				Hide();
				if (nextSpace == DialogueStageSpace.Stage)
					ShowStage();
				else if (nextSpace == DialogueStageSpace.World)
					ShowWorld();
				else
					throw new ArgumentException("3D 무대가 아닌 자리: " + nextSpace);
				space = nextSpace;
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

			bool castChanged = space == DialogueStageSpace.Stage ? SyncStageActors(delta) : SyncWorldActors(delta);
			string previousSpeaker = speaker;
			speaker = delta.Speaker != null && actors.ContainsKey(delta.Speaker) ? delta.Speaker : null;
			if (lineText != null && speaker != null)
			{
				foreach (IDialogueCastPerformer actor in actors.Values)
				{
					if (actor.ActorName == speaker) actor.Speak(lineText);
					else actor.StopSpeaking();
				}
			}
			PlayMotions(delta);

			// 샷 바꿈은 컷. 블렌드는 카메라가 두 인물 사이를 지나며 앞사람 몸을 통과할 수 있음
			if (delta.ShotChanged)
			{
				shotTarget = delta.ShotTarget;
				if (shotTarget != null)
				{
					CutTo(ShotOn(shotTarget, delta.Shot));
					return;
				}
			}
			if (shotTarget != null)
				return;

			// 처음과 출연진이 바뀐 때는 성립 샷, 화자가 바뀌면 화자 샷
			if (hasEstablished == false || (castChanged && speaker == null))
			{
				EnterWith(WideShot());
				hasEstablished = true;
			}
			else if (speaker != null && (speaker != previousSpeaker || castChanged))
			{
				CutTo(ShotOn(speaker, DialogueStageShot.Medium));
			}
			else if (speaker == null && previousSpeaker != null)
			{
				CutTo(WideShot());
			}
		}

		public void Hide()
		{
			foreach (IDialogueCastPerformer actor in actors.Values)
			{
				if (actor != null && actor.Root != null)
					actor.Release();
			}
			actors.Clear();
			slots.Clear();
			speaker = null;
			shotTarget = null;
			if (space == DialogueStageSpace.World)
				HideWorld();
			else
				HideStage();
			space = DialogueStageSpace.Flat;
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

		// 첫 샷: 별도 무대는 컷, 월드는 게임 카메라 자리에서 블렌드로 들어옴
		private void EnterWith(DialogueShotPose pose)
		{
			if (space == DialogueStageSpace.World)
				stageCamera.Blend(Deocclude(pose), Settings.WorldEnterSeconds);
			else
				stageCamera.Cut(pose);
		}

		private void CutTo(DialogueShotPose pose)
		{
			stageCamera.Cut(space == DialogueStageSpace.World ? Deocclude(pose) : pose);
		}

		private void PlayMotions(DialogueStageDelta delta)
		{
			foreach (DialogueStageActorMotion motion in delta.Motions)
			{
				if (actors.TryGetValue(motion.Actor, out IDialogueCastPerformer actor) == false)
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
				if (actors.TryGetValue(gesture.Actor, out IDialogueCastPerformer actor))
					PlayGesture(actor, gesture.GestureName);
				else
					Debug.LogError($"[{nameof(DialogueCastStage)}] @동작 {gesture.Actor} {gesture.GestureName}: 무대에 없는 인물. 먼저 @등장");
			}
		}

		private void PlayGesture(IDialogueCastPerformer actor, string gestureName)
		{
			AnimationClip clip = Settings.FindGesture(gestureName);
			if (clip == null)
			{
				Debug.LogError($"[{nameof(DialogueCastStage)}] 모르는 동작: \"{gestureName}\". 있는 이름: {string.Join(", ", GestureNames)}");
				return;
			}
			actor.PlayGesture(clip);
		}

		private DialogueShotPose WideShot()
		{
			List<Vector3> feet = new List<Vector3>();
			float headSum = 0f;
			foreach (IDialogueCastPerformer actor in actors.Values)
			{
				feet.Add(actor.Feet);
				headSum += actor.HeadPosition.y - actor.Feet.y;
			}
			float headHeight = feet.Count > 0 ? headSum / feet.Count : Settings.ActorHeight;
			DialogueShotPose pose = DialogueShotMath.Wide(feet, headHeight, Settings.StageOrigin, CameraSide(), Settings.Wide);
			// 월드는 인물이 카메라 쪽 앞뒤로 서기도 해서, 눈높이면 앞사람이 뒷사람을 가림. 같은 곳을 위에서 내려다봄
			if (space == DialogueStageSpace.World)
				pose = new DialogueShotPose(pose.Position + Vector3.up * Settings.WorldWideRaise, pose.LookPoint, pose.FieldOfView);
			return pose;
		}

		// 미디엄은 가장 가까운 상대의 어깨 너머, 클로즈는 정면, 상대가 없으면 정면. 대상이 무대에 없으면 성립 샷
		private DialogueShotPose ShotOn(string actorName, DialogueStageShot shot)
		{
			if (actors.TryGetValue(actorName, out IDialogueCastPerformer subject) == false || shot == DialogueStageShot.Wide)
				return WideShot();
			DialogueCastStageSettings.ShotEntry entry = Settings.FindShot(shot);
			IDialogueCastPerformer listener = null;
			float nearest = float.MaxValue;
			foreach (IDialogueCastPerformer other in actors.Values)
			{
				if (other == subject)
					continue;
				float distance = (other.Feet - subject.Feet).sqrMagnitude;
				if (distance < nearest)
				{
					nearest = distance;
					listener = other;
				}
			}
			// 월드는 인물이 앞뒤로 서 있기도 해서, 듣는 사람이 카메라 쪽에 있을 때만 어깨 너머. 스프라이트는 몸 방향 대신 카메라 쪽에서 정면
			bool canOverShoulder = listener != null && (space == DialogueStageSpace.Stage || DialogueShotMath.IsInFront(listener.Feet, subject.Feet, CameraSide()));
			if (shot == DialogueStageShot.Medium && canOverShoulder)
				return DialogueShotMath.OverShoulder(subject.HeadPosition, listener.HeadPosition, CameraSide(), entry, Settings.ShoulderSide, Settings.ShoulderBack, Settings.MinCameraFront);
			Vector3 facing = space == DialogueStageSpace.World ? CameraSide() : subject.Facing;
			return DialogueShotMath.Front(subject.HeadPosition, facing, CameraSide(), entry, Settings.MinCameraFront);
		}

		private Vector3 CameraSide() => space == DialogueStageSpace.World ? worldFront : Vector3.back;
	}
}
