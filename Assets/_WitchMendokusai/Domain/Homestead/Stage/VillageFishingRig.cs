using UnityEngine;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 낚시의 모습. 휘는 낚싯대, 줄, 찌, 물결 고리, 낚은 고기가 뛰어오르는 것
	// 값은 FishingDuel 에서 읽기만 함
	public sealed class VillageFishingRig : MonoBehaviour
	{
		private const int LINE_POINTS = 10;
		private const int ROD_POINTS = 6;
		private const int RING_POINTS = 28;
		private const int RING_COUNT = 3;

		[SerializeField] private float floatHeight = 0.34f;
		[SerializeField] private float lineWidth = 0.02f;
		[SerializeField] private float lineSag = 0.5f;
		[Header("Rod")]
		[SerializeField] private Vector3 hand = new(0f, 0.42f, 0f);
		[SerializeField] private float handSide = 0.24f;
		[SerializeField] private float rodLength = 1.5f;
		[SerializeField] private float rodAngle = 48f;
		[Tooltip("팽팽할 때 끝이 처지는 길이 (m)")]
		[SerializeField] private float rodBend = 0.55f;
		[SerializeField] private Vector2 rodWidth = new(0.055f, 0.018f);
		[Header("Float")]
		[SerializeField] private float biteDip = 0.22f;
		[SerializeField] private float duelDip = 0.12f;
		[SerializeField] private float duelSway = 0.5f;
		[SerializeField] private float duelSwaySeconds = 1.1f;
		[SerializeField] private float shakePerSecond = 9f;
		[Header("Ripples")]
		[SerializeField] private Vector2 ringRadius = new(0.1f, 0.85f);
		[SerializeField] private float ringSeconds = 0.9f;
		[SerializeField] private float ringWidth = 0.035f;
		[SerializeField] private float duelRingEvery = 0.45f;
		[SerializeField] private Color ringColor = new(0.92f, 0.97f, 1f, 1f);
		[Header("Catch")]
		[SerializeField] private float leapSeconds = 0.65f;
		[SerializeField] private float leapHeight = 1.5f;
		[SerializeField] private float fishHeight = 0.6f;
		[SerializeField] private float holdHeight = 1.35f;

		private readonly LineRenderer[] rings = new LineRenderer[RING_COUNT];
		private readonly float[] ringTimes = new float[RING_COUNT];
		private readonly Vector3[] ringCenters = new Vector3[RING_COUNT];
		private LineRenderer rod;
		private LineRenderer line;
		private HomesteadSprite bobber;
		private HomesteadSprite fish;
		private Transform angler;
		private Vector3 water;
		private Vector3 aim;
		private float leap = -1f;
		private float ringTimer;
		private Vector3 leapFrom;

		public Vector3 FloatPosition => bobber.transform.position;

		public static VillageFishingRig Create(Transform parent)
		{
			GameObject root = new(nameof(VillageFishingRig));
			root.transform.SetParent(parent, false);
			VillageFishingRig rig = root.AddComponent<VillageFishingRig>();
			HomesteadArt art = HomesteadArt.Instance;
			rig.rod = rig.MakeLine("Rod", art.Wood, ROD_POINTS, 1f);
			rig.rod.widthCurve = AnimationCurve.Linear(0f, rig.rodWidth.x, 1f, rig.rodWidth.y);
			rig.rod.numCapVertices = 2;
			rig.line = rig.MakeLine("Line", art.Cloth, LINE_POINTS, rig.lineWidth);
			for (int i = 0; i < RING_COUNT; i++)
			{
				rig.rings[i] = rig.MakeLine("Ring", art.Cloth, RING_POINTS, rig.ringWidth);
				rig.rings[i].loop = true;
				rig.rings[i].alignment = LineAlignment.TransformZ;
				rig.rings[i].transform.rotation = Quaternion.Euler(90f, 0f, 0f);
				rig.rings[i].startColor = rig.ringColor;
				rig.rings[i].endColor = rig.ringColor;
				rig.ringTimes[i] = -1f;
			}
			rig.bobber = HomesteadSprite.Create(root.transform, Vector3.zero, art.Icon("bobber"), rig.floatHeight);
			rig.fish = HomesteadSprite.Create(root.transform, Vector3.zero, null, rig.fishHeight);
			root.SetActive(false);
			return rig;
		}

		private LineRenderer MakeLine(string objectName, Material material, int points, float width)
		{
			GameObject lineObject = new(objectName);
			lineObject.transform.SetParent(transform, false);
			LineRenderer made = lineObject.AddComponent<LineRenderer>();
			made.sharedMaterial = material;
			made.widthMultiplier = width;
			made.positionCount = points;
			made.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			made.enabled = false;
			return made;
		}

		public void Cast(Transform caster, Vector3 target)
		{
			angler = caster;
			water = target;
			aim = target - caster.position;
			aim.y = 0f;
			aim = aim.normalized;
			leap = -1f;
			ringTimer = 0f;
			gameObject.SetActive(true);
			fish.gameObject.SetActive(false);
			bobber.gameObject.SetActive(true);
			rod.enabled = true;
			line.enabled = true;
			bobber.transform.position = target;
			Splash(target);
		}

		public void Stop() => gameObject.SetActive(false);

		// 고기가 물에서 튀어 머리 위로
		public void Land(Sprite sprite)
		{
			fish.SetSprite(sprite, fishHeight);
			fish.gameObject.SetActive(true);
			bobber.gameObject.SetActive(false);
			line.enabled = false;
			leap = 0f;
			leapFrom = bobber.transform.position;
			Splash(water);
		}

		public void Lose()
		{
			bobber.gameObject.SetActive(false);
			line.enabled = false;
			Splash(water);
		}

		public void Splash(Vector3 center)
		{
			for (int i = 0; i < RING_COUNT; i++)
			{
				ringTimes[i] = -i * ringSeconds * 0.25f;
				ringCenters[i] = center;
				rings[i].enabled = false;
			}
		}

		public void Step(FishingDuel duel)
		{
			if (duel.IsOver == false)
				MoveFloat(duel);
			DrawRod(duel.IsOver ? 0f : duel.Tension);
			if (line.enabled)
				DrawLine(duel.Tension);
			StepRings();
			StepLeap();
		}

		private void MoveFloat(FishingDuel duel)
		{
			Vector3 position = water;
			if (duel.Phase == FishingPhase.Bite)
				position.y -= biteDip * Mathf.Abs(Mathf.Sin(Time.time * shakePerSecond));
			else if (duel.Phase == FishingPhase.Duel)
			{
				// 감긴 만큼 다가오고 고기가 좌우로 끎
				Vector3 side = Vector3.Cross(Vector3.up, aim);
				float strength = duel.IsSurging ? 1.6f : 1f;
				position = Vector3.Lerp(water, water - aim * 1.6f, duel.Progress) + side * (Mathf.Sin(Time.time / duelSwaySeconds * Mathf.PI * 2f * strength) * duelSway);
				position.y -= duelDip + (duel.IsSurging ? biteDip * Mathf.Abs(Mathf.Sin(Time.time * shakePerSecond)) : 0f);
				ringTimer -= Time.deltaTime * strength;
				if (ringTimer <= 0f)
				{
					ringTimer = duelRingEvery;
					Vector3 surface = position;
					surface.y = water.y;
					StartRing(surface);
				}
			}
			else
				position.y += Mathf.Sin(Time.time * 2.4f) * 0.03f;
			bobber.transform.position = position;
		}

		private void StartRing(Vector3 center)
		{
			int oldest = 0;
			for (int i = 1; i < RING_COUNT; i++)
			{
				if (ringTimes[i] > ringTimes[oldest])
					oldest = i;
			}
			ringTimes[oldest] = 0f;
			ringCenters[oldest] = center;
		}

		private Vector3 RodTip(float tension)
		{
			Vector3 grip = angler.position + hand + Vector3.Cross(Vector3.up, aim) * handSide;
			float angle = rodAngle * Mathf.Deg2Rad;
			return grip + (aim * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle)) * rodLength + (aim * 0.4f + Vector3.down) * (rodBend * tension);
		}

		private void DrawRod(float tension)
		{
			Vector3 grip = angler.position + hand + Vector3.Cross(Vector3.up, aim) * handSide;
			Vector3 tip = RodTip(tension);
			Vector3 middle = (grip + RodTip(0f)) * 0.5f;
			for (int i = 0; i < ROD_POINTS; i++)
			{
				float ratio = i / (ROD_POINTS - 1f);
				rod.SetPosition(i, Vector3.Lerp(Vector3.Lerp(grip, middle, ratio), Vector3.Lerp(middle, tip, ratio), ratio));
			}
		}

		// 팽팽할수록 곧게
		private void DrawLine(float tension)
		{
			Vector3 from = RodTip(tension);
			Vector3 to = bobber.transform.position + Vector3.up * (floatHeight * 0.8f);
			float sag = lineSag * (1f - tension);
			for (int i = 0; i < LINE_POINTS; i++)
			{
				float ratio = i / (LINE_POINTS - 1f);
				line.SetPosition(i, Vector3.Lerp(from, to, ratio) + Vector3.down * (Mathf.Sin(ratio * Mathf.PI) * sag));
			}
		}

		private void StepRings()
		{
			for (int i = 0; i < RING_COUNT; i++)
			{
				if (ringTimes[i] >= ringSeconds)
				{
					rings[i].enabled = false;
					continue;
				}
				ringTimes[i] += Time.deltaTime;
				float ratio = ringTimes[i] / ringSeconds;
				rings[i].enabled = ratio is > 0f and < 1f;
				if (rings[i].enabled == false)
					continue;
				float radius = Mathf.Lerp(ringRadius.x, ringRadius.y, 1f - (1f - ratio) * (1f - ratio));
				rings[i].widthMultiplier = ringWidth * (1f - ratio);
				for (int point = 0; point < RING_POINTS; point++)
				{
					float angle = point / (float)RING_POINTS * Mathf.PI * 2f;
					rings[i].SetPosition(point, ringCenters[i] + new Vector3(Mathf.Cos(angle) * radius, 0.02f, Mathf.Sin(angle) * radius));
				}
			}
		}

		private void StepLeap()
		{
			if (leap < 0f)
				return;
			leap = Mathf.Min(1f, leap + Time.deltaTime / leapSeconds);
			Vector3 hold = angler.position + Vector3.up * holdHeight;
			float bounce = leap >= 1f ? Mathf.Sin(Time.time * 4f) * 0.04f : Mathf.Sin(leap * Mathf.PI) * leapHeight;
			fish.transform.position = Vector3.Lerp(leapFrom, hold, leap) + Vector3.up * bounce;
		}
	}
}
