using System.IO;
using UnityEngine;

namespace WitchMendokusai.ContentRuntime
{
    public static class SequenceLibraryLocation
    {
        public static string Directory { get { return Path.Combine(Application.persistentDataPath, "Sequences"); } }
    }
}
