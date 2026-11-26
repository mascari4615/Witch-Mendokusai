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
	}

	// 대화 샷 규칙. 카메라는 늘 무대 앞쪽 (-Z) 에 두어 두 인물을 잇는 선을 넘지 않음 (180도 규칙)
	public static class DialogueShotMath
	{
		// 모두를 담는 성립 샷. 가로 가운데는 발 위치, 높이는 머리
		public static DialogueShotPose Wide(IReadOnlyList<Vector3> feet, float headHeight, Vector3 stageOrigin, DialogueCastStageSettings.ShotEntry shot)
		{
			Vector3 center = stageOrigin;
			float span = 0f;
			if (feet.Count > 0)
			{
				Vector3 sum = Vector3.zero;
				float minX = float.MaxValue;
				float maxX = float.MinValue;
				foreach (Vector3 foot in feet)
				{
					sum += foot;
					minX = Mathf.Min(minX, foot.x);
					maxX = Mathf.Max(maxX, foot.x);
				}
				center = sum / feet.Count;
				span = maxX - minX;
			}
			center += Vector3.up * headHeight;
			Vector3 position = center + new Vector3(0f, shot.Height, -(shot.Distance + span * 0.5f));
			return new DialogueShotPose(position, center + Vector3.up * shot.LookHeight, shot.FieldOfView);
		}

		// 어깨 너머 샷. 듣는 사람 머리 뒤로 물러나 옆으로 비킨 자리에서 화자를 봄
		public static DialogueShotPose OverShoulder(Vector3 speakerHead, Vector3 listenerHead, DialogueCastStageSettings.ShotEntry shot, float shoulderSide, float shoulderBack, float minFront)
		{
			Vector3 backward = Flat(listenerHead - speakerHead);
			backward = backward.sqrMagnitude > 0.0001f ? backward.normalized : Vector3.back;
			Vector3 side = Vector3.Cross(Vector3.up, backward);
			if (side.z > 0f)
				side = -side;
			Vector3 position = listenerHead + backward * shoulderBack + side * shoulderSide + Vector3.up * shot.Height;
			position = KeepInFront(position, speakerHead, minFront);
			return new DialogueShotPose(position, speakerHead + Vector3.up * shot.LookHeight, shot.FieldOfView);
		}

		// 화자 정면 샷. 몸이 카메라 쪽으로 돌아 있어 얼굴이 보임
		public static DialogueShotPose Front(Vector3 speakerHead, Vector3 speakerFacing, DialogueCastStageSettings.ShotEntry shot, float minFront)
		{
			Vector3 facing = Flat(speakerFacing);
			facing = facing.sqrMagnitude > 0.0001f ? facing.normalized : Vector3.back;
			Vector3 position = speakerHead + facing * shot.Distance + Vector3.up * shot.Height;
			position = KeepInFront(position, speakerHead, minFront);
			return new DialogueShotPose(position, speakerHead + Vector3.up * shot.LookHeight, shot.FieldOfView);
		}

		// 화자보다 무대 뒤로 넘어가면 앞쪽으로 되돌림
		private static Vector3 KeepInFront(Vector3 position, Vector3 subject, float minFront)
		{
			if (position.z > subject.z - minFront)
				position.z = subject.z - minFront;
			return position;
		}

		private static Vector3 Flat(Vector3 vector) => new Vector3(vector.x, 0f, vector.z);
	}
}
