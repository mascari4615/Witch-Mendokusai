using System.IO;
using UnityEngine;

namespace WitchMendokusai
{
	public static class UGCPathResolver
	{
		public static string GetProjectRootPath()
		{
			return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
		}

		public static string GetServerUGCRootPath()
		{
			return Path.Combine(GetProjectRootPath(), "ServerData", "UGC");
		}

		public static string GetSamplePath(string fileName)
		{
			return Path.Combine(GetServerUGCRootPath(), "Samples", fileName);
		}

		public static string GetSchemaPath(string fileName)
		{
			return Path.Combine(GetServerUGCRootPath(), "Schemas", fileName);
		}
	}
}
