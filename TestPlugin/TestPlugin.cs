using BepInEx;
using BepInEx.IL2CPP;

namespace TestPlugin
{
    [BepInAutoPlugin("dev.bepinex.TestPlugin")]
    public partial class TestPlugin : BasePlugin
    {
        public override void Load()
        {
            Log.LogInfo(typeof(PlayerControl));
        }
    }
}
