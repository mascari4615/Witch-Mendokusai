using UnityEngine;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle
{
	/// <summary>아군 인형, 체력바, 이동과 피격 동작 표시</summary>
	internal sealed class BattleAllyPresenter
	{
		private readonly Transform worldRoot;
		private readonly BattleEntityPresenter.Settings settings;
		private readonly Transform[] dolls;
		private readonly Transform[] barAnchors;
		private readonly Material[] skins;
		private readonly HealthBar[] bars;
		private readonly IdleDollAnimator[] animators;
		private readonly float[] attackLeft;
		private readonly float[] hurtLeft;
		private float clock;

		public BattleAllyPresenter(Transform worldRoot, BattleEntityPresenter.Settings settings)
		{
			this.worldRoot = worldRoot;
			this.settings = settings;

			int seats = IdleSquad.SEAT_COUNT;
			dolls = new Transform[seats];
			barAnchors = new Transform[seats];
			skins = new Material[seats];
			bars = new HealthBar[seats];
			animators = new IdleDollAnimator[seats];
			attackLeft = new float[seats];
			hurtLeft = new float[seats];

			for (int seat = 0; seat < seats; seat++)
			{
				dolls[seat] = Build(seat);
				dolls[seat].gameObject.SetActive(false);
			}
		}

		public void Render(IdleSnapshot snapshot, float delta)
		{
			Dress(snapshot);
			AdvanceMotion(snapshot, delta);
		}

		public void PlayAttack(int seat)
		{
			if (seat >= 0 && seat < attackLeft.Length)
			{
				attackLeft[seat] = settings.LungeSeconds;
				animators[seat]?.PlayAttack();
			}
		}

		public void PlayHit(int seat)
		{
			if (seat >= 0 && seat < hurtLeft.Length)
			{
				hurtLeft[seat] = settings.HurtSeconds;
				animators[seat]?.PlayHit();
			}
		}

		public bool TryGetHead(int seat, out Vector3 position)
		{
			position = Vector3.zero;

			if (seat < 0 || seat >= dolls.Length || dolls[seat] == null)
			{
				return false;
			}

			position = dolls[seat].position + Vector3.up * settings.AllyHeadHeight;
			return true;
		}

		private Transform Build(int seat)
		{
			GameObject doll = new GameObject("Doll" + seat);
			doll.transform.SetParent(worldRoot, false);
			doll.transform.localRotation = Quaternion.LookRotation(Vector3.right);

			GameObject barAnchor = new GameObject("AllyHealthBarAnchor" + seat);
			barAnchor.transform.SetParent(worldRoot, false);
			barAnchors[seat] = barAnchor.transform;

			Material skin = BattleVisualFactory.MakeMaterial(
				seat == 0 ? settings.MyColor : settings.GradeColors[0]);
			skins[seat] = skin;

			if (settings.DollPrefab != null)
			{
				GameObject made = Object.Instantiate(settings.DollPrefab, doll.transform, false);
				made.name = "Model";
				made.transform.localScale = Vector3.one * settings.DollModelScale;
				animators[seat] = made.GetComponentInChildren<IdleDollAnimator>();
				foreach (MeshRenderer part in made.GetComponentsInChildren<MeshRenderer>())
				{
					if (part.name == "Body" || part.name == "Head")
					{
						part.sharedMaterial = skin;
					}
				}
			}
			else
			{
				Mesh round = Geometry.Build(Geometry.Shape.SphereOnce, settings.DollFallbackRadius);

				GameObject body = new GameObject("Body");
				body.transform.SetParent(doll.transform, false);
				body.transform.localPosition = settings.DollBodyPosition;
				body.transform.localScale = settings.DollBodyScale;
				body.AddComponent<MeshFilter>().sharedMesh = round;
				body.AddComponent<MeshRenderer>().sharedMaterial = skin;

				GameObject head = new GameObject("Head");
				head.transform.SetParent(doll.transform, false);
				head.transform.localPosition = settings.DollHeadPosition;
				head.transform.localScale = settings.DollHeadScale;
				head.AddComponent<MeshFilter>().sharedMesh = round;
				head.AddComponent<MeshRenderer>().sharedMaterial = skin;
			}

			bars[seat] = HealthBar.Attach(barAnchor.transform,
				settings.AllyBarHeight, settings.AllyBarWidth, settings.AllyBarThickness,
				settings.BarBackColor, settings.AllyBarColor);
			return doll.transform;
		}

		private void Dress(IdleSnapshot snapshot)
		{
			for (int seat = 0; seat < dolls.Length && seat < snapshot.Seats.Length; seat++)
			{
				IdleSeatView view = snapshot.Seats[seat];

				if (dolls[seat].gameObject.activeSelf != view.Taken)
				{
					dolls[seat].gameObject.SetActive(view.Taken);
					bars[seat].SetVisible(view.Taken);
				}

				if (view.Taken == false)
				{
					continue;
				}

				if (view.HeroId >= 0)
				{
					int grade = Mathf.Clamp((int)view.Grade, 0, settings.GradeColors.Length - 1);
					skins[seat].color = settings.GradeColors[grade];
				}

				if (view.Standing)
				{
					bars[seat].SetFillColor(settings.AllyBarColor);
					bars[seat].SetRatio((float)view.HealthRatio);
					dolls[seat].localRotation = Quaternion.LookRotation(Vector3.right);
					dolls[seat].localScale = Vector3.one;
					animators[seat]?.SetDowned(false);
				}
				else
				{
					bars[seat].SetFillColor(settings.ReviveBarColor);
					bars[seat].SetRatio((float)view.ReviveRatio);
					if (animators[seat] != null)
					{
						// 쓰러짐은 클립이 맡음. 도형 인형만 넘어뜨림
						animators[seat].SetDowned(true);
					}
					else
					{
						dolls[seat].localRotation = Quaternion.Euler(settings.DownedEuler);
						dolls[seat].localScale = settings.DownedScale;
					}
				}

				barAnchors[seat].position = dolls[seat].position;
			}
		}

		private void AdvanceMotion(IdleSnapshot snapshot, float delta)
		{
			clock += delta;

			for (int seat = 0; seat < dolls.Length; seat++)
			{
				attackLeft[seat] = Mathf.Max(0f, attackLeft[seat] - delta);
				hurtLeft[seat] = Mathf.Max(0f, hurtLeft[seat] - delta);

				float attack = settings.LungeSeconds > 0f
					? Mathf.Sin(Mathf.Clamp01(1f - attackLeft[seat] / settings.LungeSeconds) * Mathf.PI)
					: 0f;
				float hurt = settings.HurtSeconds > 0f
					? Mathf.Sin(Mathf.Clamp01(1f - hurtLeft[seat] / settings.HurtSeconds) * Mathf.PI)
					: 0f;

				bool walking = seat < snapshot.Fighters.Length && snapshot.Fighters[seat].Moving;
				if (animators[seat] != null && dolls[seat].gameObject.activeSelf)
				{
					animators[seat].SetMoving(walking);
					animators[seat].Tick(delta);
				}
				float x = seat < snapshot.Fighters.Length ? (float)snapshot.Fighters[seat].X : 0f;
				float y = seat < snapshot.Fighters.Length ? (float)snapshot.Fighters[seat].Y : 0f;
				float bob = walking ? BattleMotion.WalkBob(
						clock, seat, settings.AllyWalkBobHeight,
						settings.AllyWalkBobFrequency, settings.AllyWalkBobPhaseStep) : 0f;

				dolls[seat].localPosition = Vector3.Lerp(
					dolls[seat].localPosition,
					new Vector3(
						x + attack * settings.AllyLungeDistance - hurt * settings.AllyHurtDistance,
						bob,
						y),
					BattleMotion.CatchUp(settings.PositionCatchUp, delta));
			}
		}
	}
}
