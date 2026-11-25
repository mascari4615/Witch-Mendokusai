using System.Collections.Generic;
using Newtonsoft.Json;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 자료 글 여럿을 정의 묶음 하나로. 파일은 갈래별로 나뉘고 같은 목록은 이어 붙음
	// 엔진 타입 없음. 시험과 수치 실험이 같은 파일을 그대로 읽음
	public static class HomesteadCatalogJson
	{
		private static readonly JsonSerializerSettings SETTINGS = new()
		{
			MissingMemberHandling = MissingMemberHandling.Error,
		};

		public static HomesteadCatalog Load(IEnumerable<string> texts)
		{
			HomesteadCatalog catalog = new();
			foreach (string text in texts)
			{
				JsonConvert.PopulateObject(text, catalog, SETTINGS);
			}
			return catalog;
		}
	}
}
