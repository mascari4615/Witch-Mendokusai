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
		// 모두를 담는 성립 샷
		public static DialogueShotPose Wide(IReadOnlyList<Vector3> heads, Vector3 stageOrigin, DialogueCastStageSettings.ShotEntry shot, float emptyStageHeadHeight)
		{
			Vector3 center = stageOrigin + Vector3.up * emptyStageHeadHeight;
			float span = 0f;
			if (heads.Count > 0)
			{
				Vector3 sum = Vector3.zero;
				float minX = float.MaxValue;
				float maxX = float.MinValue;
				foreach (Vector3 head in heads)
				{
					sum += head;
					minX = Mathf.Min(minX, head.x);
					maxX = Mathf.Max(maxX, head.x);
				}
				center = sum / heads.Count;
				span = maxX - minX;
			}
			Vector3 position = center + new Vector3(0f, shot.Height, -(shot.Distance + span * 0.5f));
			return new DialogueShotPose(position, center + Vector3.up * shot.LookHeight, shot.FieldOfView);
		}

		// 화자 샷. 듣는 사람이 있으면 그 어깨 너머, 없으면 화자 정면
		public static DialogueShotPose OnSpeaker(Vector3 speakerHead, Vector3 speakerFacing, Vector3? listenerHead, DialogueCastStageSettings.ShotEntry shot, float shoulderSide, float minFront)
		{
			Vector3 look = speakerHead + Vector3.up * shot.LookHeight;
			Vector3 away;
			Vector3 side = Vector3.zero;
			if (listenerHead.HasValue)
			{
				Vector3 towardListener = Flat(listenerHead.Value - speakerHead);
				away = towardListener.sqrMagnitude > 0.0001f ? towardListener.normalized : Vector3.back;
				Vector3 perpendicular = Vector3.Cross(Vector3.up, away);
				if (perpendicular.z > 0f)
					perpendicular = -perpendicular;
				side = perpendicular * shoulderSide;
			}
			else
			{
				Vector3 facing = Flat(speakerFacing);
				away = facing.sqrMagnitude > 0.0001f ? facing.normalized : Vector3.back;
			}
			Vector3 position = speakerHead + away * shot.Distance + side + Vector3.up * shot.Height;
			position = KeepInFront(position, speakerHead, minFront);
			return new DialogueShotPose(position, look, shot.FieldOfView);
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
