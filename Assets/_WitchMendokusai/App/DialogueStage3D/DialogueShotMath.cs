using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai.DialogueStage3D
{
	public readonly struct DialogueShotPose
	{
		public Vector3 Position { get; }
		public Quaternion Rotation { get; }
		public float FieldOfView { get; }
		public Vector3 LookPoint { get; }

		public DialogueShotPose(Vector3 position, Vector3 lookPoint, float fieldOfView)
		{
			Position = position;
			LookPoint = lookPoint;
			Rotation = Quaternion.LookRotation(lookPoint - position, Vector3.up);
			FieldOfView = fieldOfView;
		}

		public DialogueShotPose(Vector3 position, Quaternion rotation, float fieldOfView)
		{
			Position = position;
			Rotation = rotation;
			LookPoint = position + rotation * Vector3.forward;
			FieldOfView = fieldOfView;
		}
	}

	// 대화 샷 규칙. front 는 인물들에서 카메라 쪽을 향한 방향 (별도 무대는 -Z, 월드는 대화를 시작할 때 게임 카메라가 있던 쪽).
	// 카메라는 늘 그쪽에만 두어 두 인물을 잇는 선을 넘지 않음 (180도 규칙)
	public static class DialogueShotMath
	{
		// 모두를 담는 성립 샷. 가로 가운데는 발 위치, 높이는 머리
		public static DialogueShotPose Wide(IReadOnlyList<Vector3> feet, float headHeight, Vector3 fallbackCenter, Vector3 front, DialogueCastStageSettings.ShotEntry shot)
		{
			Vector3 forward = FlatUnit(front, Vector3.back);
			Vector3 right = Vector3.Cross(Vector3.up, forward);
			Vector3 center = fallbackCenter;
			float span = 0f;
			if (feet.Count > 0)
			{
				Vector3 sum = Vector3.zero;
				float min = float.MaxValue;
				float max = float.MinValue;
				foreach (Vector3 foot in feet)
				{
					sum += foot;
					float along = Vector3.Dot(foot, right);
					min = Mathf.Min(min, along);
					max = Mathf.Max(max, along);
				}
				center = sum / feet.Count;
				span = max - min;
			}
			center += Vector3.up * headHeight;
			Vector3 position = center + forward * (shot.Distance + span * 0.5f) + Vector3.up * shot.Height;
			return new DialogueShotPose(position, center + Vector3.up * shot.LookHeight, shot.FieldOfView);
		}

		// 어깨 너머 샷. 듣는 사람 머리 뒤로 물러나 카메라 쪽으로 비킨 자리에서 화자를 봄
		public static DialogueShotPose OverShoulder(Vector3 speakerHead, Vector3 listenerHead, Vector3 front, DialogueCastStageSettings.ShotEntry shot, float shoulderSide, float shoulderBack, float minFront)
		{
			Vector3 backward = FlatUnit(listenerHead - speakerHead, Vector3.back);
			Vector3 side = Vector3.Cross(Vector3.up, backward);
			if (Vector3.Dot(side, front) < 0f)
				side = -side;
			Vector3 position = listenerHead + backward * shoulderBack + side * shoulderSide + Vector3.up * shot.Height;
			position = KeepInFront(position, speakerHead, front, minFront);
			return new DialogueShotPose(position, speakerHead + Vector3.up * shot.LookHeight, shot.FieldOfView);
		}

		// 화자 정면 샷. 몸이 카메라 쪽으로 돌아 있어 얼굴이 보임
		public static DialogueShotPose Front(Vector3 speakerHead, Vector3 speakerFacing, Vector3 front, DialogueCastStageSettings.ShotEntry shot, float minFront)
		{
			Vector3 facing = FlatUnit(speakerFacing, front);
			Vector3 position = speakerHead + facing * shot.Distance + Vector3.up * shot.Height;
			position = KeepInFront(position, speakerHead, front, minFront);
			return new DialogueShotPose(position, speakerHead + Vector3.up * shot.LookHeight, shot.FieldOfView);
		}

		// 인물들 가운데에서 지금 카메라를 향한 수평 방향. 월드 대화는 이쪽을 카메라 쪽으로 고정
		public static Vector3 FrontFromCamera(IReadOnlyList<Vector3> feet, Vector3 cameraPosition)
		{
			if (feet.Count == 0)
				return Vector3.back;
			Vector3 center = Vector3.zero;
			foreach (Vector3 foot in feet)
				center += foot;
			center /= feet.Count;
			return FlatUnit(cameraPosition - center, Vector3.back);
		}

		// 듣는 사람이 화자보다 카메라 쪽에 있나. 아니면 어깨 너머 자리가 화자 뒤편이 되어 쓸 수 없음
		public static bool IsInFront(Vector3 listener, Vector3 speaker, Vector3 front) =>
			Vector3.Dot(listener - speaker, FlatUnit(front, Vector3.back)) > 0f;

		// 화자보다 카메라 반대쪽으로 넘어가면 앞쪽으로 되돌림
		private static Vector3 KeepInFront(Vector3 position, Vector3 subject, Vector3 front, float minFront)
		{
			Vector3 forward = FlatUnit(front, Vector3.back);
			float depth = Vector3.Dot(position - subject, forward);
			if (depth < minFront)
				position += forward * (minFront - depth);
			return position;
		}

		private static Vector3 FlatUnit(Vector3 vector, Vector3 fallback)
		{
			Vector3 flat = new Vector3(vector.x, 0f, vector.z);
			return flat.sqrMagnitude > 0.0001f ? flat.normalized : fallback;
		}
	}
}
