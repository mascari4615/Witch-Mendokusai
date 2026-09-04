using UnityEngine;

namespace WitchMendokusai.Idle
{
	/// <summary>
	/// 유닛 <b>머리 위</b> 체력바 — 3D 조각 둘(바탕·채움)로 만든다 (V2, 실조사 반영 2026-08-23).
	///
	/// ★ 왜 화면 UI 가 아니라 여기냐 — 자동전투+카드 개입 계열은 <b>아군도 잡몹도 머리 위</b>에 작은 바를 달고,
	///   상단 대형 바는 <b>보스에게만</b> 준다 (`refs/blue-archive.md` § 2, 공식 스크린샷 관찰).
	///   대열 방치 전투 계열도 부활 대기 게이지가 <b>용병 머리 위</b>다. 체력을 화면 구석에 몰면
	///   「누가 위험한가」가 판에서 안 읽힌다 — 그게 우리가 틀렸던 자리다.
	///
	/// ★ UIToolkit 은 월드 공간을 안 준다. 그래서 <b>납작한 조각</b>을 세워 카메라를 향하게 한다(빌보드).
	/// </summary>
	public sealed class HealthBar : MonoBehaviour
	{
		private Transform pivot;
		private Transform fill;
		private Material fillSkin;
		private Material backSkin;
		private float width;

		/// <summary>바 하나를 만들어 <paramref name="owner"/> 위에 매단다.</summary>
		public static HealthBar Attach(Transform owner, float height, float width, float thickness,
			Color backColor, Color fillColor)
		{
			GameObject root = new GameObject("HealthBar");
			root.transform.SetParent(owner, false);
			root.transform.localPosition = new Vector3(0f, height, 0f);

			HealthBar bar = root.AddComponent<HealthBar>();
			bar.pivot = root.transform;
			bar.width = width;

			GameObject back = GameObject.CreatePrimitive(PrimitiveType.Quad);
			back.name = "Back";
			back.transform.SetParent(root.transform, false);
			back.transform.localScale = new Vector3(width, thickness, 1f);
			bar.backSkin = Skin(back, backColor);

			// 채움은 <b>왼쪽 끝</b>을 축으로 줄어든다 — 가운데서 줄면 「닳는다」로 안 읽힌다.
			GameObject anchor = new GameObject("Fill");
			anchor.transform.SetParent(root.transform, false);
			anchor.transform.localPosition = new Vector3(-width * 0.5f, 0f, -0.01f);

			GameObject front = GameObject.CreatePrimitive(PrimitiveType.Quad);
			front.name = "FillQuad";
			front.transform.SetParent(anchor.transform, false);
			front.transform.localPosition = new Vector3(0.5f, 0f, 0f);
			front.transform.localScale = new Vector3(1f, thickness, 1f);
			bar.fillSkin = Skin(front, fillColor);

			bar.fill = anchor.transform;
			bar.SetRatio(1f);
			return bar;
		}

		/// <summary>0~1 로 채운다.</summary>
		public void SetRatio(float ratio)
		{
			float clamped = Mathf.Clamp01(ratio);
			fill.localScale = new Vector3(width * clamped, 1f, 1f);
			fill.gameObject.SetActive(clamped > 0.001f);
		}

		/// <summary>채움 색을 바꾼다 — 쓰러진 자리의 부활 게이지 같은 다른 뜻일 때.</summary>
		public void SetFillColor(Color color)
		{
			fillSkin.color = color;
		}

		public void SetVisible(bool visible)
		{
			if (pivot.gameObject.activeSelf != visible)
			{
				pivot.gameObject.SetActive(visible);
			}
		}

		/// <summary>카메라를 향한다 — 쿼터뷰라 안 돌리면 옆에서 본 종잇장이 된다.</summary>
		private void LateUpdate()
		{
			Camera watching = Camera.main;
			if (watching == null)
			{
				return;
			}

			pivot.rotation = watching.transform.rotation;
		}

		private static Material Skin(GameObject piece, Color color)
		{
			Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
			if (shader == null)
			{
				shader = Shader.Find("Unlit/Color");
			}
			if (shader == null)
			{
				shader = Shader.Find("Standard");
			}

			Material made = new Material(shader);
			made.hideFlags = HideFlags.DontSave;
			made.color = color;

			// URP Unlit 은 색을 _BaseColor 로 받는다 — color 만 넣으면 흰 판으로 뜬다.
			if (made.HasProperty("_BaseColor"))
			{
				made.SetColor("_BaseColor", color);
			}

			piece.GetComponent<MeshRenderer>().sharedMaterial = made;

			// 그림자는 끈다 — 얇은 판이 땅에 줄무늬를 그린다.
			MeshRenderer renderer = piece.GetComponent<MeshRenderer>();
			renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			renderer.receiveShadows = false;

			Collider hull = piece.GetComponent<Collider>();
			if (Application.isPlaying)
			{
				Object.Destroy(hull);
			}
			else
			{
				Object.DestroyImmediate(hull);
			}
			return made;
		}
	}
}
