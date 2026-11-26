using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace WitchMendokusai.DialogueStage3D
{
	// 3D 무대의 배우 하나. 대기와 말하기를 섞고 한 번짜리 동작을 위에 얹음. 끄덕임과 들썩임은 뼈를 코드로 움직임.
	// 대화 중 게임 시간이 멈춰도 돌도록 실시간 시계
	public sealed class DialogueCastActor : MonoBehaviour
	{
		private const int IDLE_INPUT = 0;
		private const int TALK_INPUT = 1;
		private const int BASE_INPUT = 0;
		private const int GESTURE_INPUT = 1;

		private DialogueCastStageSettings settings;
		private Animator animator;
		private Transform head;
		private Transform hips;
		private PlayableGraph graph;
		private AnimationMixerPlayable baseMixer;
		private AnimationMixerPlayable rootMixer;
		private AnimationClipPlayable gesture;
		private float talkRemaining;
		private float talkWeight;
		private float gestureElapsed;
		private float gestureLength;
		private float nodElapsed = -1f;
		private float bobElapsed = -1f;
		private Vector3 moveFrom;
		private Vector3 moveTo;
		private Quaternion turnFrom;
		private Quaternion turnTo;
		private float moveElapsed = -1f;

		public string ActorName { get; private set; }
		public Vector3 HeadPosition => head != null ? head.position : transform.position + Vector3.up * 1.4f;
		public Vector3 Facing => transform.forward;

		public void Initialize(string actorName, DialogueCastStageSettings stageSettings)
		{
			ActorName = actorName;
			settings = stageSettings;
			animator = GetComponentInChildren<Animator>();
			if (animator == null || animator.avatar == null || animator.avatar.isHuman == false)
				throw new System.InvalidOperationException($"배우 {actorName} 의 프리팹에 Humanoid 아바타가 있는 Animator 가 없다");
			animator.applyRootMotion = false;
			animator.runtimeAnimatorController = null;
			head = animator.GetBoneTransform(HumanBodyBones.Head);
			hips = animator.GetBoneTransform(HumanBodyBones.Hips);

			graph = PlayableGraph.Create("DialogueCastActor " + actorName);
			graph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
			AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Cast", animator);
			baseMixer = AnimationMixerPlayable.Create(graph, 2);
			graph.Connect(AnimationClipPlayable.Create(graph, settings.IdleClip), 0, baseMixer, IDLE_INPUT);
			graph.Connect(AnimationClipPlayable.Create(graph, settings.TalkClip), 0, baseMixer, TALK_INPUT);
			baseMixer.SetInputWeight(IDLE_INPUT, 1f);
			rootMixer = AnimationMixerPlayable.Create(graph, 2);
			graph.Connect(baseMixer, 0, rootMixer, BASE_INPUT);
			rootMixer.SetInputWeight(BASE_INPUT, 1f);
			output.SetSourcePlayable(rootMixer);
			graph.Play();
		}

		public void Place(Vector3 position, Quaternion rotation)
		{
			transform.SetPositionAndRotation(position, rotation);
			moveElapsed = -1f;
		}

		public void MoveTo(Vector3 position, Quaternion rotation)
		{
			if ((position - transform.position).sqrMagnitude < 0.0001f && Quaternion.Angle(rotation, transform.rotation) < 0.5f)
				return;
			moveFrom = transform.position;
			turnFrom = transform.rotation;
			moveTo = position;
			turnTo = rotation;
			moveElapsed = 0f;
		}

		public void Speak(string lineText)
		{
			int length = lineText == null ? 0 : lineText.Length;
			talkRemaining = Mathf.Clamp(length * settings.TalkSecondsPerCharacter, settings.TalkMinSeconds, settings.TalkMaxSeconds);
		}

		public void StopSpeaking() => talkRemaining = 0f;

		public void Nod() => nodElapsed = 0f;

		public void Bob() => bobElapsed = 0f;

		public void PlayGesture(AnimationClip clip)
		{
			if (gesture.IsValid())
			{
				graph.Disconnect(rootMixer, GESTURE_INPUT);
				gesture.Destroy();
			}
			gesture = AnimationClipPlayable.Create(graph, clip);
			gesture.SetTime(0);
			graph.Connect(gesture, 0, rootMixer, GESTURE_INPUT);
			gestureElapsed = 0f;
			gestureLength = clip.length;
		}

		private void Update()
		{
			if (graph.IsValid() == false)
				return;
			float deltaTime = Time.unscaledDeltaTime;
			float fade = Mathf.Max(0.01f, settings.CrossFadeSeconds);

			talkRemaining = Mathf.Max(0f, talkRemaining - deltaTime);
			talkWeight = Mathf.MoveTowards(talkWeight, talkRemaining > 0f ? 1f : 0f, deltaTime / fade);
			baseMixer.SetInputWeight(IDLE_INPUT, 1f - talkWeight);
			baseMixer.SetInputWeight(TALK_INPUT, talkWeight);

			float gestureWeight = 0f;
			if (gesture.IsValid())
			{
				gestureElapsed += deltaTime;
				float fadeIn = Mathf.Clamp01(gestureElapsed / fade);
				float fadeOut = Mathf.Clamp01((gestureLength - gestureElapsed) / fade);
				gestureWeight = Mathf.Min(fadeIn, fadeOut);
				if (gestureElapsed >= gestureLength)
				{
					graph.Disconnect(rootMixer, GESTURE_INPUT);
					gesture.Destroy();
					gestureWeight = 0f;
				}
			}
			rootMixer.SetInputWeight(BASE_INPUT, 1f - gestureWeight);
			rootMixer.SetInputWeight(GESTURE_INPUT, gestureWeight);

			if (moveElapsed >= 0f)
			{
				moveElapsed += deltaTime;
				float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(moveElapsed / Mathf.Max(0.01f, settings.MoveSeconds)));
				transform.SetPositionAndRotation(Vector3.Lerp(moveFrom, moveTo, progress), Quaternion.Slerp(turnFrom, turnTo, progress));
				if (progress >= 1f)
					moveElapsed = -1f;
			}
		}

		// 애니메이션이 뼈를 쓴 뒤에 얹음
		private void LateUpdate()
		{
			float deltaTime = Time.unscaledDeltaTime;
			if (nodElapsed >= 0f && head != null)
			{
				nodElapsed += deltaTime;
				float progress = nodElapsed / Mathf.Max(0.01f, settings.NodSeconds);
				// 두 번 끄덕임
				float pitch = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI * 2f) * settings.NodDegrees;
				head.localRotation *= Quaternion.Euler(Mathf.Abs(pitch), 0f, 0f);
				if (progress >= 1f)
					nodElapsed = -1f;
			}
			if (bobElapsed >= 0f && hips != null)
			{
				bobElapsed += deltaTime;
				float progress = bobElapsed / Mathf.Max(0.01f, settings.BobSeconds);
				hips.position += Vector3.up * Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI) * settings.BobHeight;
				if (progress >= 1f)
					bobElapsed = -1f;
			}
		}

		private void OnDestroy()
		{
			if (graph.IsValid())
				graph.Destroy();
		}
	}
}
