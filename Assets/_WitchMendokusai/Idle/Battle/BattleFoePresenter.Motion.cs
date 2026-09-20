using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle
{
	// BattleFoePresenter.cs 의 Motion 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일에. 매 프레임 움직임. 회전, 번쩍, 쓰러짐
	internal sealed partial class BattleFoePresenter
	{
		/// <summary>죽어서 쓰러지는 중인 적. 시뮬에서는 이미 없음</summary>
		private sealed class Fallen
		{
			public Transform Piece;
			public Transform Model;
			public float Left;
			public float Total;
			public Vector3 Scale;
		}

		private void ClearFallen()
		{
			foreach (Fallen body in fallen)
			{
				if (body.Piece != null) { BattleVisualFactory.Kill(body.Piece.gameObject); }
			}
			fallen.Clear();
		}

		private void AdvanceMotion(float delta)
		{
			clock += delta;

			for (int index = 0; index < foes.Count; index++)
			{
				Foe foe = foes[index];
				foe.Model.Rotate(Vector3.up, settings.FoeSpinDegrees * delta, Space.Self);
				AdvanceFlash(foe, delta);

				Vector3 position = foe.Model.localPosition;
				position.y = BattleMotion.FoeBob(
					clock, index, settings.FoeBobHeight, settings.FoeBobFrequency, settings.FoeBobPhaseStep);
				foe.Model.localPosition = position;
			}
		}

		/// <summary>죽은 적을 쓰러뜨린다. FoeFallSeconds 동안 옆으로 눕고 작아진 뒤 사라짐</summary>
		private void Fall(Foe foe)
		{
			if (settings.FoeFallSeconds <= 0f)
			{
				BattleVisualFactory.Kill(foe.Piece.gameObject);
				return;
			}

			fallen.Add(new Fallen
			{
				Piece = foe.Piece,
				Model = foe.Model,
				Left = settings.FoeFallSeconds,
				Total = settings.FoeFallSeconds,
				Scale = foe.Model.localScale,
			});
		}

		private void AdvanceFallen(float delta)
		{
			for (int at = fallen.Count - 1; at >= 0; at--)
			{
				Fallen body = fallen[at];
				body.Left -= delta;
				if (body.Left <= 0f || body.Piece == null)
				{
					if (body.Piece != null)
					{
						BattleVisualFactory.Kill(body.Piece.gameObject);
					}
					fallen.RemoveAt(at);
					continue;
				}

				float share = 1f - body.Left / body.Total;
				body.Piece.localRotation = Quaternion.Euler(0f, 0f, 90f * Mathf.SmoothStep(0f, 1f, share));
				body.Model.localScale = body.Scale * (1f - share * share);
			}
		}

		private void AdvanceFlash(Foe foe, float delta)
		{
			if (foe.FlashLeft <= 0f)
			{
				return;
			}

			foe.FlashLeft -= delta;
			float share = Mathf.Clamp01(foe.FlashLeft / settings.FoeFlashSeconds);
			Color made = Color.Lerp(foe.RestColor, Color.white, share * settings.FoeFlashWhiten);

			foe.Skin.color = made;
			if (foe.Skin.HasProperty("_BaseColor"))
			{
				foe.Skin.SetColor("_BaseColor", made);
			}
		}
	}
}

