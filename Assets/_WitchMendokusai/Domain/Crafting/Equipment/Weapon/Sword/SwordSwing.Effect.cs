using UnityEngine;

namespace WitchMendokusai
{
	// 검 효과 메시: 매 프레임 단색 사각형을 채우고 남는 사각형은 넓이 0으로 접음
	public partial class SwordSwing
	{
		private Mesh effectMesh;
		private Vector3[] effectVertices;
		private Color[] effectColors;
		private int quadCount;

		// 베기 획 경로, 판정이 정해질 때 한 번 계산
		private Vector3[] strokePoints;
		private float[] strokeDistances;
		private Vector3 strokeBulge;

		private void BuildEffectMesh()
		{
			effectVertices = new Vector3[MAX_QUADS * 4];
			effectColors = new Color[MAX_QUADS * 4];
			int[] triangles = new int[MAX_QUADS * 6];
			for (int i = 0; i < MAX_QUADS; i++)
			{
				int vertex = i * 4;
				int triangle = i * 6;
				triangles[triangle] = vertex;
				triangles[triangle + 1] = vertex + 1;
				triangles[triangle + 2] = vertex + 2;
				triangles[triangle + 3] = vertex + 2;
				triangles[triangle + 4] = vertex + 1;
				triangles[triangle + 5] = vertex + 3;
			}
			effectMesh = new Mesh { name = "Sword swing effect" };
			effectMesh.MarkDynamic();
			effectMesh.vertices = effectVertices;
			effectMesh.colors = effectColors;
			effectMesh.triangles = triangles;
			trailFilter.sharedMesh = effectMesh;
		}

		private void UpdateEffect(float age, Vector3 pivot)
		{
			quadCount = 0;
			switch (style)
			{
				case SwordSwingStyle.FlyingWave:
					AddBladeTrail(age, pivot, BladeTip, 1f);
					AddWave(age, pivot);
					break;
				case SwordSwingStyle.ScaledTrail:
					AddBladeTrail(age, pivot, Mathf.Max(BladeTip, area.OuterRadius), 1.5f);
					break;
				case SwordSwingStyle.FittedStroke:
					AddStroke(age, pivot);
					break;
			}
			for (int i = quadCount * 4; i < effectVertices.Length; i++)
				effectVertices[i] = Vector3.zero;
			effectMesh.vertices = effectVertices;
			effectMesh.colors = effectColors;
		}

		private void AddQuad(Vector3 nearFrom, Vector3 farFrom, Vector3 nearTo, Vector3 farTo, Color color)
		{
			if (quadCount >= MAX_QUADS)
				return;
			int vertex = quadCount * 4;
			effectVertices[vertex] = nearFrom;
			effectVertices[vertex + 1] = farFrom;
			effectVertices[vertex + 2] = nearTo;
			effectVertices[vertex + 3] = farTo;
			for (int i = 0; i < 4; i++)
				effectColors[vertex + i] = color;
			quadCount++;
		}

		// 칼끝이 지나간 자리의 초승달 궤적, 바깥 반경은 고정이고 최근 쪽이 가장 굵고 꼬리로 갈수록 바깥 호로 모임
		// taper 1이면 굵기 고정, 클수록 꼬리가 빨리 가늘어짐
		private void AddBladeTrail(float age, Vector3 pivot, float outer, float taper)
		{
			if (age > swingTime + trailTime)
				return;
			float head = Mathf.Min(age, swingTime);
			float tail = Mathf.Clamp(age - trailTime, 0f, head);
			if (head - tail <= 0f)
				return;
			float fade = age < swingTime ? 1f : 1f - (age - swingTime) / trailTime;
			bool scaled = outer > BladeTip + 0.01f;
			float handInner = Mathf.Max(HiltRadius, HiltRadius + bladeLength * trailInner);
			// 손 쪽 궤적은 몸통 한 겹, 확대 궤적은 바깥 띠, 몸통, 밝은 가장자리 세 겹
			int layers = scaled ? 3 : 1;
			for (int layer = 0; layer < layers; layer++)
			{
				float inner = scaled ? outer * scaledTrailBands[layer] : handInner;
				Color color = Fade(scaled ? (layer == 0 ? glowColor : layer == 1 ? bodyColor : coreColor) : bodyColor, fade);
				for (int i = 0; i < trailSegments; i++)
				{
					float fromFraction = (float)i / trailSegments;
					float toFraction = (float)(i + 1) / trailSegments;
					Vector3 from = Direction(BladeAngle(Mathf.Lerp(tail, head, fromFraction)));
					Vector3 to = Direction(BladeAngle(Mathf.Lerp(tail, head, toFraction)));
					float fromInner = outer - (outer - inner) * Mathf.Pow(fromFraction, taper);
					float toInner = outer - (outer - inner) * Mathf.Pow(toFraction, taper);
					AddQuad(pivot + from * fromInner, pivot + from * outer, pivot + to * toInner, pivot + to * outer, color);
				}
			}
		}

		// 공격 방향으로 볼록한 초승달 검기, 양 끝 거리는 판정 폭, 멈추는 곳은 판정 끝
		private void AddWave(float age, Vector3 pivot)
		{
			float waveAge = age - LaunchTime;
			if (waveAge < 0f || waveAge > waveTime)
				return;
			float progress = waveAge / waveTime;
			float fade = progress < 0.7f ? 1f : 1f - (progress - 0.7f) / 0.3f;
			float width = area.Width > 0.01f ? area.Width : area.OuterRadius * 0.6f;
			float span = width * Mathf.Lerp(waveStartSpan, 1f, EaseOut(progress * 2f, 2f));
			float bend = waveBend * span;
			float thickness = waveThickness * span * (1f - 0.4f * progress);
			float startZ = BladeTip - bend;
			float endZ = Mathf.Max(startZ, area.MaxZ - bend);
			float centerX = area.CenterX;
			for (int echo = waveEchoes; echo >= 1; echo--)
			{
				float lagged = EaseOut(progress - echo * waveEchoLag, 2f);
				AddCrescent(pivot, centerX, Mathf.Lerp(startZ, endZ, lagged), span * (1f - echo * 0.06f), bend, thickness * 0.8f, Fade(glowColor, fade / echo));
			}
			float z = Mathf.Lerp(startZ, endZ, EaseOut(progress, 2f));
			AddCrescent(pivot, centerX, z, span, bend, thickness, Fade(bodyColor, fade));
			AddCrescent(pivot, centerX, z, span, bend, thickness * 0.35f, Fade(coreColor, fade));
		}

		// 앞쪽 볼록한 호와 그보다 뒤로 물러난 호 사이, 가운데가 가장 두껍고 양 끝은 뾰족
		private void AddCrescent(Vector3 pivot, float centerX, float z, float span, float bend, float thickness, Color color)
		{
			Vector3 previousFront = Vector3.zero;
			Vector3 previousBack = Vector3.zero;
			for (int i = 0; i <= waveSegments; i++)
			{
				float s = -1f + 2f * i / waveSegments;
				float curve = 1f - s * s;
				Vector3 front = pivot + new Vector3(centerX + s * span * 0.5f, 0f, z + bend * curve);
				Vector3 back = front - Vector3.forward * (thickness * curve);
				if (i > 0)
					AddQuad(previousBack, previousFront, back, front, color);
				previousFront = front;
				previousBack = back;
			}
		}

		// 판정의 긴 변을 획 길이로, 짧은 변을 휘는 깊이 상한으로 쓰는 2차 곡선 경로
		private void BuildStrokePath()
		{
			int count = strokeSegments + 1;
			if (strokePoints == null || strokePoints.Length != count)
			{
				strokePoints = new Vector3[count];
				strokeDistances = new float[count];
			}
			float width = area.Width > 0.01f ? area.Width : area.OuterRadius * 0.6f;
			float near = Mathf.Max(area.MinZ, handRadius);
			float far = Mathf.Max(near + 0.1f, area.MaxZ);
			float depth = far - near;
			Vector3 start;
			Vector3 end;
			float sagitta;
			if (depth >= width * strokeLongRatio)
			{
				// 길쭉한 판정: 휘두르는 방향으로 비스듬히 앞으로 뻗는 긴 획
				float slant = width * 0.35f * side;
				start = new Vector3(area.CenterX - slant, 0f, near + 0.25f);
				end = new Vector3(area.CenterX + slant, 0f, far - strokeMargin);
				sagitta = Mathf.Min(strokeMaxSagitta, width * 0.3f);
			}
			else
			{
				// 넓고 짧은 판정: 폭을 휘두르는 방향으로 가로지르는 앞쪽 볼록 호
				float middle = near + depth * 0.35f;
				float half = Mathf.Max(0f, width * 0.5f - strokeMargin) * side;
				start = new Vector3(area.CenterX - half, 0f, middle);
				end = new Vector3(area.CenterX + half, 0f, middle);
				sagitta = Mathf.Min(depth * 0.55f, width * 0.35f);
			}
			Vector3 chord = end - start;
			Vector3 normal = Vector3.Cross(Vector3.up, chord).normalized;
			Vector3 middlePoint = (start + end) * 0.5f;
			// 캐릭터에서 먼 쪽으로 볼록
			if (Vector3.Dot(normal, middlePoint) < 0f)
				normal = -normal;
			strokeBulge = normal;
			Vector3 control = middlePoint + normal * (sagitta * 2f);
			float distance = 0f;
			for (int i = 0; i < count; i++)
			{
				float t = (float)i / strokeSegments;
				Vector3 point = (1f - t) * (1f - t) * start + 2f * (1f - t) * t * control + t * t * end;
				if (i > 0)
					distance += Vector3.Distance(point, strokePoints[i - 1]);
				strokePoints[i] = point;
				strokeDistances[i] = distance;
			}
		}

		private Vector3 StrokePoint(float distance)
		{
			int last = strokePoints.Length - 1;
			for (int i = 1; i <= last; i++)
			{
				if (strokeDistances[i] >= distance)
				{
					float span = strokeDistances[i] - strokeDistances[i - 1];
					float t = span > 0f ? (distance - strokeDistances[i - 1]) / span : 0f;
					return Vector3.Lerp(strokePoints[i - 1], strokePoints[i], t);
				}
			}
			return strokePoints[last];
		}

		// 머리가 경로를 지나가며 드러나고 꼬리가 뒤따라 사라지는 획, 끝 모양과 굵기는 크기와 무관하게 고정
		private void AddStroke(float age, Vector3 pivot)
		{
			float strokeAge = age - LaunchTime;
			if (strokeAge < 0f || strokeAge > strokeReveal + strokeFade || strokePoints == null)
				return;
			float length = strokeDistances[strokeDistances.Length - 1];
			float head = length * EaseOut(strokeAge / strokeReveal, 2f);
			float tail = length * EaseOut((strokeAge - strokeFade * 0.6f) / strokeReveal, 2f);
			if (head - tail < 0.02f)
				return;
			float fade = strokeAge < strokeReveal ? 1f : 1f - (strokeAge - strokeReveal) / strokeFade;
			AddStrokeLayer(pivot, head, tail, length, 2.2f, 0f, Fade(glowColor, fade));
			AddStrokeLayer(pivot, head, tail, length, 1f, 0.2f, Fade(bodyColor, fade));
			AddStrokeLayer(pivot, head, tail, length, 0.38f, 0.9f, Fade(coreColor, fade));
		}

		private void AddStrokeLayer(Vector3 pivot, float head, float tail, float length, float scale, float shift, Color color)
		{
			Vector3 previousInner = Vector3.zero;
			Vector3 previousOuter = Vector3.zero;
			for (int i = 0; i <= strokeSegments; i++)
			{
				float distance = Mathf.Lerp(tail, head, (float)i / strokeSegments);
				Vector3 point = StrokePoint(distance);
				Vector3 ahead = StrokePoint(Mathf.Min(length, distance + 0.05f));
				Vector3 behind = StrokePoint(Mathf.Max(0f, distance - 0.05f));
				Vector3 across = Vector3.Cross(Vector3.up, ahead - behind).normalized;
				if (Vector3.Dot(across, strokeBulge) < 0f)
					across = -across;
				float taper = Mathf.Min(1f, (head - distance) / strokeTip, (distance - tail) / (strokeTip * 2.2f), distance / strokeTip * 1.5f, (length - distance) / strokeTip * 1.5f);
				float width = strokeWidth * Mathf.Max(0f, taper) * scale;
				// 밝은 심은 볼록한 날 쪽으로 치우침
				Vector3 center = pivot + point + across * (width * shift);
				Vector3 inner = center - across * (width * 0.5f);
				Vector3 outer = center + across * (width * 0.5f);
				if (i > 0)
					AddQuad(previousInner, previousOuter, inner, outer, color);
				previousInner = inner;
				previousOuter = outer;
			}
		}
	}
}
