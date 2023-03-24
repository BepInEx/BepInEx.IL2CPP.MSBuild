using System;
using System.Reflection;
using System.Threading.Tasks;
using BepInEx.IL2CPP.MSBuild.Shared;
using Microsoft.Build.Utilities;

namespace BepInEx.IL2CPP.MSBuild;

public sealed class Il2CppInteropManagerWrapper
{
    public delegate Task<string> GenerateAsyncDelegate(GameLibsPackage gameLibsPackage, string il2CppInteropVersion);

    private readonly object _instance;
    private readonly GenerateAsyncDelegate _generateAsync;

    public Il2CppInteropManagerWrapper(TaskLoggingHelper logger)
    {
        var type = Type.GetType("BepInEx.IL2CPP.MSBuild.Runner.Il2CppInteropManager, BepInEx.IL2CPP.MSBuild.Runner", true);
        _instance = Activator.CreateInstance(type, logger);
        _generateAsync = (GenerateAsyncDelegate)type.GetMethod("GenerateAsync", BindingFlags.Public | BindingFlags.Instance)!.CreateDelegate(typeof(GenerateAsyncDelegate), _instance);
    }

    public Task<string> GenerateAsync(GameLibsPackage gameLibsPackage, string il2CppInteropVersion)
    {
        return _generateAsync.Invoke(gameLibsPackage, il2CppInteropVersion);
    }
}
