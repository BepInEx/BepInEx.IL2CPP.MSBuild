using Microsoft.Build.Framework;

namespace BepInEx.IL2CPP.MSBuild
{
    public class GameLibsPackage
    {
        public string Id { get; }
        public string DummyDirectory { get; }
        public string Version { get; }
        public string UnityVersion { get; }

        public GameLibsPackage(ITaskItem taskItem)
        {
            Id = taskItem.ItemSpec;
            Version = taskItem.GetMetadata("Version");
            DummyDirectory = taskItem.GetMetadata("DummyDirectory");
            UnityVersion = taskItem.GetMetadata("UnityVersion");
        }
    }
}
