using System.Threading.Tasks;
using Task = Microsoft.Build.Utilities.Task;

namespace BepInEx.IL2CPP.MSBuild
{
    public abstract class AsyncTask : Task
    {
        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        public abstract Task<bool> ExecuteAsync();
    }
}
