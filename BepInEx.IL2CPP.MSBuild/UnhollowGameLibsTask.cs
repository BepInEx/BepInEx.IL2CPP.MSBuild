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

                const string unhollowerTool = "Il2CppAssemblyUnhollower.Lib";
                const string unhollowerBaseLib = "Il2CppAssemblyUnhollower.BaseLib";
                const string unhollowerBaseLibLegacy = "Il2CppAssemblyUnhollower.BaseLib.Legacy";
                const string cecil = "Mono.Cecil";
                const string iced = "Iced";

                if (id is unhollowerTool or unhollowerBaseLib or unhollowerBaseLibLegacy or cecil or iced)
                {
                    var dllPath = reference.ItemSpec;

#if NET472
                    // Workaround Visual Studio still using old .net framework for whatever reason. WHY MICROSOFT, WHY?
                    switch (id)
                    {
                        case unhollowerTool:
                        case unhollowerBaseLib:
                        case unhollowerBaseLibLegacy:
                            dllPath = dllPath.Replace("netstandard2.1", "net472");
                            break;

                        case cecil:
                            dllPath = dllPath.Replace("netstandard2.0", "net40");
                            break;

                        case iced:
                            dllPath = dllPath.Replace("netstandard2.1", "net45").Replace("netstandard2.0", "net45");
                            break;
                    }
#endif

                    assemblies.Add(reference.GetMetadata("Filename"), dllPath);
                }
            }

            if (!assemblies.Any())
            {
                Log.LogError("No Il2CppAssemblyUnhollower found, make sure you referenced BepInEx");
                return false;
            }

            var unhollowerVersion = Reference.Single(x => x.GetMetadata("NuGetPackageId") == "Il2CppAssemblyUnhollower.Lib").GetMetadata("NuGetPackageVersion");

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

            var proxyAssemblyGenerator = new ProxyAssemblyGenerator(Log);

            foreach (var gameLibsPackage in Unhollow.Select(taskItem => new GameLibsPackage(taskItem)))
            {
                var path = await proxyAssemblyGenerator.GenerateAsync(gameLibsPackage, unhollowerVersion);

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
