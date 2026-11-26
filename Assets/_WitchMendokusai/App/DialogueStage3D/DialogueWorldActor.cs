using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace WitchMendokusai.DialogueStage3D
{
	// 월드 대화의 인물 하나. 이미 월드에 있는 캐릭터에 잠깐 붙음.
	// Humanoid 는 원래 애니메이터 컨트롤러 위에 말하기와 몸짓 클립을 덧입히고, 스프라이트는 그림 위치로 들썩임과 흔들림.
	// 대화 중 게임 시간이 멈춰도 돌도록 실시간 시계. 끝나면 원래대로
	public sealed class DialogueWorldActor : MonoBehaviour, IDialogueCastPerformer
	{
		private const int CONTROLLER_INPUT = 0;
		private const int OVERLAY_INPUT = 1;

		private DialogueCastStageSettings settings;
		private Animator animator;
		private Transform head;
		private Transform hips;
		private readonly List<SpriteRenderer> spriteParts = new List<SpriteRenderer>();
		private Transform sprite;
		private Vector3 spriteRest;
		private PlayableGraph graph;
		private AnimationMixerPlayable mixer;
		private AnimationClipPlayable overlay;
		private bool isOverlayGesture;
		private float overlayElapsed;
		private float overlayLength;
		private float talkRemaining;
		private float nodElapsed = -1f;
		private float bobElapsed = -1f;
		private float shakeElapsed = -1f;
		private Quaternion turnFrom;
		private Quaternion turnTo;
		private float turnElapsed = -1f;

		public string ActorName { get; private set; }
		public Transform Root => transform;
		public Vector3 Feet => transform.position;
		public Vector3 Facing => transform.forward;
		public bool IsHumanoid => graph.IsValid();

		public Vector3 HeadPosition
		{
			get
			{
				if (head != null)
					return head.position;
				Bounds bounds = VisualBounds();
				return new Vector3(bounds.center.x, bounds.max.y - bounds.size.y * settings.SpriteHeadFromTop, bounds.center.z);
			}
		}

		public void Initialize(string actorName, DialogueCastStageSettings stageSettings)
		{
			ActorName = actorName;
			settings = stageSettings;
			animator = GetComponentInChildren<Animator>();
			bool isHumanoid = animator != null && animator.avatar != null && animator.avatar.isHuman && animator.runtimeAnimatorController != null;
			if (isHumanoid)
			{
				head = animator.GetBoneTransform(HumanBodyBones.Head);
				hips = animator.GetBoneTransform(HumanBodyBones.Hips);
				graph = PlayableGraph.Create("DialogueWorldActor " + actorName);
				graph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
				AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "World cast", animator);
				mixer = AnimationMixerPlayable.Create(graph, 2);
				graph.Connect(AnimatorControllerPlayable.Create(graph, animator.runtimeAnimatorController), 0, mixer, CONTROLLER_INPUT);
				mixer.SetInputWeight(CONTROLLER_INPUT, 1f);
				output.SetSourcePlayable(mixer);
				graph.Play();
				return;
			}
			SpriteRenderer tallest = null;
			foreach (SpriteRenderer spriteRenderer in GetComponentsInChildren<SpriteRenderer>())
			{
				if (IsStandingSprite(spriteRenderer))
					spriteParts.Add(spriteRenderer);
				if (spriteRenderer.enabled && spriteRenderer.sprite != null && (tallest == null || spriteRenderer.bounds.size.y > tallest.bounds.size.y))
					tallest = spriteRenderer;
			}
			// 기준보다 작은 인물 (슬라임 같은) 은 켜진 그림 중 가장 큰 것 하나로
			if (spriteParts.Count == 0 && tallest != null)
				spriteParts.Add(tallest);
			sprite = CommonParent(spriteParts);
			if (sprite != null)
				spriteRest = sprite.localPosition;
		}

		// 서 있는 그림만 인물로 셈. 꺼진 렌더러 (플레이어 HeadAnchor) 와 바닥에 눕힌 그림자, 표식은 제외
		private bool IsStandingSprite(SpriteRenderer spriteRenderer) =>
			spriteRenderer.enabled && spriteRenderer.sprite != null && spriteRenderer.bounds.size.y >= settings.SpriteMinStandingHeight;

		// 몸과 머리가 따로인 그림 (플레이어) 도 같이 움직이게 공통 부모를 흔듦. 인물 뿌리 자신이면 이동을 건드리지 않게 비움
		private Transform CommonParent(List<SpriteRenderer> parts)
		{
			if (parts.Count == 0)
				return null;
			Transform common = parts[0].transform;
			foreach (SpriteRenderer part in parts)
			{
				while (common != null && part.transform.IsChildOf(common) == false)
					common = common.parent;
			}
			// 인물 뿌리이거나 그 밖 (뿌리에 그림이 붙은 경우) 이면 흔들지 않음
			bool insideActor = common != null && common != transform && common.IsChildOf(transform);
			return insideActor ? common : null;
		}

		public void Speak(string lineText)
		{
			int length = lineText == null ? 0 : lineText.Length;
			talkRemaining = Mathf.Clamp(length * settings.TalkSecondsPerCharacter, settings.TalkMinSeconds, settings.TalkMaxSeconds);
			if (IsHumanoid && isOverlayGesture == false && settings.TalkClip != null)
				SetOverlay(settings.TalkClip, false);
		}

		public void StopSpeaking() => talkRemaining = 0f;

		public void Nod() => nodElapsed = 0f;

		public void Bob() => bobElapsed = 0f;

		// 스프라이트는 동작 클립이 없어 흔들림으로
		public void PlayGesture(AnimationClip clip)
		{
			if (IsHumanoid == false)
			{
				shakeElapsed = 0f;
				return;
			}
			SetOverlay(clip, true);
		}

		public void TurnToward(Vector3 point)
		{
			Vector3 toward = point - transform.position;
			toward.y = 0f;
			if (toward.sqrMagnitude < 0.0001f || IsHumanoid == false)
				return;
			turnFrom = transform.rotation;
			turnTo = Quaternion.LookRotation(toward, Vector3.up);
			turnElapsed = 0f;
		}

		public void Release()
		{
			if (graph.IsValid())
				graph.Destroy();
			if (sprite != null)
				sprite.localPosition = spriteRest;
			Destroy(this);
		}

		private void SetOverlay(AnimationClip clip, bool isGesture)
		{
			if (overlay.IsValid())
			{
				graph.Disconnect(mixer, OVERLAY_INPUT);
				overlay.Destroy();
			}
			overlay = AnimationClipPlayable.Create(graph, clip);
			overlay.SetTime(0);
			graph.Connect(overlay, 0, mixer, OVERLAY_INPUT);
			isOverlayGesture = isGesture;
			overlayElapsed = 0f;
			overlayLength = clip.length;
		}

		private void Update()
		{
			float deltaTime = Time.unscaledDeltaTime;
			talkRemaining = Mathf.Max(0f, talkRemaining - deltaTime);
			if (IsHumanoid)
				UpdateOverlay(deltaTime);
			if (turnElapsed >= 0f)
			{
				turnElapsed += deltaTime;
				float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(turnElapsed / Mathf.Max(0.01f, settings.MoveSeconds)));
				transform.rotation = Quaternion.Slerp(turnFrom, turnTo, progress);
				if (progress >= 1f)
					turnElapsed = -1f;
			}
		}

		private void UpdateOverlay(float deltaTime)
		{
			float fade = Mathf.Max(0.01f, settings.CrossFadeSeconds);
			float weight = 0f;
			if (overlay.IsValid())
			{
				overlayElapsed += deltaTime;
				float fadeIn = Mathf.Clamp01(overlayElapsed / fade);
				bool isDone = isOverlayGesture ? overlayElapsed >= overlayLength : talkRemaining <= 0f;
				float fadeOut = isOverlayGesture ? Mathf.Clamp01((overlayLength - overlayElapsed) / fade) : (isDone ? 0f : 1f);
				weight = Mathf.Min(fadeIn, fadeOut);
				if (isDone)
				{
					graph.Disconnect(mixer, OVERLAY_INPUT);
					overlay.Destroy();
					isOverlayGesture = false;
					weight = 0f;
				}
			}
			mixer.SetInputWeight(CONTROLLER_INPUT, 1f - weight);
			mixer.SetInputWeight(OVERLAY_INPUT, weight);
		}

		// 애니메이션이 뼈를 쓴 뒤에 얹음
		private void LateUpdate()
		{
			float deltaTime = Time.unscaledDeltaTime;
			Vector3 spriteOffset = Vector3.zero;
			if (nodElapsed >= 0f)
			{
				nodElapsed += deltaTime;
				float progress = nodElapsed / Mathf.Max(0.01f, settings.NodSeconds);
				float wave = Mathf.Abs(Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI * 2f));
				if (head != null) head.localRotation *= Quaternion.Euler(wave * settings.NodDegrees, 0f, 0f);
				else spriteOffset += Vector3.down * wave * settings.BobHeight * 0.5f;
				if (progress >= 1f) nodElapsed = -1f;
			}
			if (bobElapsed >= 0f)
			{
				bobElapsed += deltaTime;
				float progress = bobElapsed / Mathf.Max(0.01f, settings.BobSeconds);
				float lift = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI) * settings.BobHeight;
				if (hips != null) hips.position += Vector3.up * lift;
				else spriteOffset += Vector3.up * lift;
				if (progress >= 1f) bobElapsed = -1f;
			}
			if (shakeElapsed >= 0f)
			{
				shakeElapsed += deltaTime;
				float progress = shakeElapsed / Mathf.Max(0.01f, settings.BobSeconds);
				spriteOffset += Vector3.right * Mathf.Sin(progress * Mathf.PI * 6f) * settings.BobHeight * (1f - Mathf.Clamp01(progress));
				if (progress >= 1f) shakeElapsed = -1f;
			}
			if (sprite != null)
				sprite.localPosition = spriteRest + spriteOffset;
		}

		// 스프라이트 인물은 서 있는 그림들로만 잼 (아래에 이름표, 아이콘 같은 다른 렌더러가 있음)
		private Bounds VisualBounds()
		{
			if (spriteParts.Count > 0)
			{
				Bounds sprites = spriteParts[0].bounds;
				foreach (SpriteRenderer part in spriteParts)
					sprites.Encapsulate(part.bounds);
				return sprites;
			}
			Renderer[] renderers = GetComponentsInChildren<Renderer>();
			if (renderers.Length == 0)
				return new Bounds(transform.position + Vector3.up * settings.ActorHeight * 0.5f, Vector3.up * settings.ActorHeight);
			Bounds bounds = renderers[0].bounds;
			foreach (Renderer renderer in renderers)
				bounds.Encapsulate(renderer.bounds);
			return bounds;
		}

		private void OnDestroy()
		{
			if (graph.IsValid())
				graph.Destroy();
		}
	}
}
