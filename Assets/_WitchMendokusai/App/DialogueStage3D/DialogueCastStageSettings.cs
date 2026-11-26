using System;
using UnityEngine;

namespace WitchMendokusai.DialogueStage3D
{
	// 3D 배우 무대의 배우, 동작, 카메라 샷, 무대 값. 대본 낱말과 클립의 짝도 여기 (사람과 AI 가 같은 자리에서 고침)
	[CreateAssetMenu(fileName = "DialogueCastStageSettings", menuName = "WM/Narrative/DialogueCastStageSettings")]
	public sealed class DialogueCastStageSettings : ScriptableObject
	{
		public const string RESOURCE_NAME = "DialogueCastStageSettings";

		[Serializable]
		public sealed class CastEntry
		{
			[field: SerializeField] public string Name { get; private set; }
			[field: SerializeField] public GameObject Prefab { get; private set; }
			[field: SerializeField] public Material[] Materials { get; private set; }
			[field: SerializeField, Tooltip("공통 키에 곱하는 비율. 1 이면 ActorHeight")] public float HeightScale { get; private set; } = 1f;
		}

		[Serializable]
		public sealed class GestureEntry
		{
			[field: SerializeField] public string Name { get; private set; }
			[field: SerializeField] public AnimationClip Clip { get; private set; }
		}

		[Serializable]
		public sealed class ShotEntry
		{
			[field: SerializeField, Tooltip("대상 머리에서 카메라까지 거리 (m)")] public float Distance { get; private set; } = 2.4f;
			[field: SerializeField, Tooltip("카메라 높이, 머리 기준 (m)")] public float Height { get; private set; } = 0f;
			[field: SerializeField, Tooltip("바라보는 점 높이, 머리 기준 (m)")] public float LookHeight { get; private set; } = -0.15f;
			[field: SerializeField] public float FieldOfView { get; private set; } = 30f;
		}

		[field: Header("배우")]
		[field: SerializeField] public CastEntry[] Cast { get; private set; } = new CastEntry[0];
		[field: SerializeField, Tooltip("Cast 에 없는 인물")] public GameObject FallbackPrefab { get; private set; }
		[field: SerializeField, Tooltip("모델 원래 크기와 상관없이 맞추는 키 (m)")] public float ActorHeight { get; private set; } = 1.45f;
		[field: SerializeField, Tooltip("자리 사이 거리 (m). 왼쪽끝부터 오른쪽끝까지 다섯 자리")] public float SlotSpacing { get; private set; } = 1.1f;
		[field: SerializeField, Tooltip("상대를 보는 몸을 카메라 쪽으로 돌리는 비율 0~1")] public float FacingTowardCamera { get; private set; } = 0.35f;
		[field: SerializeField] public float MoveSeconds { get; private set; } = 0.45f;

		[field: Header("동작")]
		[field: SerializeField] public AnimationClip IdleClip { get; private set; }
		[field: SerializeField] public AnimationClip TalkClip { get; private set; }
		[field: SerializeField] public GestureEntry[] Gestures { get; private set; } = new GestureEntry[0];
		[field: SerializeField, Tooltip("@점프 가 쓰는 동작 이름")] public string HopGesture { get; private set; } = "점프";
		[field: SerializeField, Tooltip("@흔들림 이 쓰는 동작 이름")] public string ShakeGesture { get; private set; } = "놀람";
		[field: SerializeField] public float CrossFadeSeconds { get; private set; } = 0.25f;
		[field: SerializeField] public float TalkSecondsPerCharacter { get; private set; } = 0.07f;
		[field: SerializeField] public float TalkMinSeconds { get; private set; } = 1.2f;
		[field: SerializeField] public float TalkMaxSeconds { get; private set; } = 6f;
		[field: SerializeField] public float NodDegrees { get; private set; } = 14f;
		[field: SerializeField] public float NodSeconds { get; private set; } = 0.55f;
		[field: SerializeField] public float BobHeight { get; private set; } = 0.06f;
		[field: SerializeField] public float BobSeconds { get; private set; } = 0.4f;

		[field: Header("카메라")]
		[field: SerializeField] public ShotEntry Wide { get; private set; } = new ShotEntry();
		[field: SerializeField] public ShotEntry Medium { get; private set; } = new ShotEntry();
		[field: SerializeField] public ShotEntry Close { get; private set; } = new ShotEntry();
		[field: SerializeField, Tooltip("어깨 너머 샷의 옆 비킴 (m)")] public float ShoulderSide { get; private set; } = 0.45f;
		[field: SerializeField, Tooltip("배우가 없을 때 성립 샷이 보는 높이 (m)")] public float EmptyStageHeadHeight { get; private set; } = 1.4f;
		[field: SerializeField, Tooltip("카메라가 화자보다 무대 앞쪽에 있어야 하는 최소 거리 (m)")] public float MinCameraFront { get; private set; } = 0.4f;
		[field: SerializeField] public float BlendSeconds { get; private set; } = 0.35f;
		[field: SerializeField, Tooltip("샷에 머무는 동안 다가가는 속도 (m/s)")] public float PushSpeed { get; private set; } = 0.03f;
		[field: SerializeField] public float PushMax { get; private set; } = 0.25f;
		[field: SerializeField] public float CameraDepth { get; private set; } = 100f;

		[field: Header("무대")]
		[field: SerializeField, Tooltip("게임 월드와 겹치지 않는 자리")] public Vector3 StageOrigin { get; private set; } = new Vector3(0f, -2000f, 0f);
		[field: SerializeField] public int ContentLayer { get; private set; } = 31;
		[field: SerializeField] public Color Background { get; private set; } = new Color(0.62f, 0.7f, 0.78f);
		[field: SerializeField] public Color FloorColor { get; private set; } = new Color(0.82f, 0.8f, 0.74f);
		[field: SerializeField] public float FloorSize { get; private set; } = 3f;
		[field: SerializeField] public Vector3 KeyLightAngles { get; private set; } = new Vector3(35f, 160f, 0f);
		[field: SerializeField] public float KeyLightIntensity { get; private set; } = 1.1f;
		[field: SerializeField] public Color KeyLightColor { get; private set; } = new Color(1f, 0.96f, 0.9f);
		[field: SerializeField] public float FillLightIntensity { get; private set; } = 0.45f;

		public static DialogueCastStageSettings Load()
		{
			DialogueCastStageSettings settings = Resources.Load<DialogueCastStageSettings>(RESOURCE_NAME);
			if (settings == null)
				throw new InvalidOperationException($"{RESOURCE_NAME} not found in Resources");
			return settings;
		}

		public CastEntry FindCast(string actorName)
		{
			foreach (CastEntry entry in Cast)
			{
				if (entry != null && string.Equals(entry.Name, actorName, StringComparison.Ordinal))
					return entry;
			}
			return null;
		}

		public AnimationClip FindGesture(string gestureName)
		{
			foreach (GestureEntry entry in Gestures)
			{
				if (entry != null && string.Equals(entry.Name, gestureName, StringComparison.Ordinal))
					return entry.Clip;
			}
			return null;
		}

		public ShotEntry FindShot(DialogueStageShot shot) => shot switch
		{
			DialogueStageShot.Wide => Wide,
			DialogueStageShot.Close => Close,
			_ => Medium,
		};
	}
}
