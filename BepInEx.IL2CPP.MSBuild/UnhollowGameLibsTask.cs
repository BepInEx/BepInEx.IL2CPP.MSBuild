using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace BepInEx.IL2CPP.MSBuild
{
    public class UnhollowGameLibsTask : AsyncTask
    {
        [Required]
        public ITaskItem[] Reference { get; set; }

        [Required]
        public ITaskItem[] Unhollow { get; set; }

        [Output]
        public ITaskItem[] UnhollowedDlls { get; set; }

        public override async Task<bool> ExecuteAsync()
        {
            var assemblies = new Dictionary<string, string>();

            foreach (var reference in Reference)
            {
                var id = reference.GetMetadata("NuGetPackageId");

                if (id is "Il2CppAssemblyUnhollower.Tool" or "Il2CppAssemblyUnhollower.BaseLib" or "Il2CppAssemblyUnhollower.BaseLib.Legacy" or "Mono.Cecil" or "Iced")
                {
                    assemblies.Add(reference.GetMetadata("Filename"), reference.ItemSpec);
                }
            }

            if (!assemblies.Any())
            {
                Log.LogError("No Il2CppAssemblyUnhollower found, make sure you referenced BepInEx");
                return false;
            }

            AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
            {
                var assemblyName = new AssemblyName(args.Name);

                if (assemblies.TryGetValue(assemblyName.Name, out var path))
                {
                    Log.LogMessage("Loading " + path);
                    return Assembly.LoadFrom(path);
                }

                return null;
            };

            var unhollowedDlls = new List<ITaskItem>();

            foreach (var gameLibsPackage in Unhollow.Select(taskItem => new GameLibsPackage(taskItem)))
            {
                Log.LogMessage($"Unhollowing {gameLibsPackage.Id}");

                var path = await ProxyAssemblyGenerator.GenerateAsync(gameLibsPackage);

                foreach (var file in Directory.GetFiles(path, "*.dll"))
                {
                    if (Path.GetFileName(file) == "netstandard.dll")
                    {
                        continue;
                    }

                    var taskItem = new TaskItem(file);
                    taskItem.SetMetadata("PackageId", gameLibsPackage.Id);

                    unhollowedDlls.Add(taskItem);
                }
            }

            UnhollowedDlls = unhollowedDlls.ToArray();

            return true;
        }
    }
}
