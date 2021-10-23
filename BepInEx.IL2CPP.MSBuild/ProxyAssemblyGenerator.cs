using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AssemblyUnhollower;
using AssemblyUnhollower.MetadataAccess;
using Mono.Cecil;

namespace BepInEx.IL2CPP.MSBuild
{
    public static class ProxyAssemblyGenerator
    {
        private static readonly HttpClient _httpClient = new();

        public static async Task<string> GenerateAsync(GameLibsPackage gameLibsPackage)
        {
            var outputDirectory = Path.Combine(Context.CachePath, "game-libs", gameLibsPackage.Id, gameLibsPackage.Version, "unhollowed");

            var hashPath = Path.Combine(outputDirectory, "hash.txt");
            var hash = ComputeHash(gameLibsPackage.DummyDirectory, gameLibsPackage.UnityVersion);

            if (File.Exists(hashPath) && File.ReadAllText(hashPath) == hash)
            {
                return outputDirectory;
            }

            var sourceFiles = Directory.GetFiles(gameLibsPackage.DummyDirectory, "*.dll");

            using var source = new CecilMetadataAccess(sourceFiles);
            Program.Main(new UnhollowerOptions
            {
                MscorlibPath = await GetMscorlibAsync(),
                Source = (List<AssemblyDefinition>)source.Assemblies,
                OutputDir = outputDirectory,
                UnityBaseLibsDir = await GetUnityLibsAsync(gameLibsPackage.UnityVersion),
                NoCopyUnhollowerLibs = true
            });

            File.WriteAllText(hashPath, hash);

            return outputDirectory;
        }

        private static string ByteArrayToString(IReadOnlyCollection<byte> data)
        {
            var builder = new StringBuilder(data.Count * 2);

            foreach (var b in data)
                builder.AppendFormat("{0:x2}", b);

            return builder.ToString();
        }

        private static string ComputeHash(string dummyDirectory, string unityVersion)
        {
            using var md5 = MD5.Create();

            foreach (var file in Directory.EnumerateFiles(dummyDirectory, "*.dll"))
            {
                var pathBytes = Encoding.UTF8.GetBytes(Path.GetFileName(file));
                md5.TransformBlock(pathBytes, 0, pathBytes.Length, pathBytes, 0);

                var contentBytes = File.ReadAllBytes(file);
                md5.TransformBlock(contentBytes, 0, contentBytes.Length, contentBytes, 0);
            }

            var unityVersionBytes = Encoding.UTF8.GetBytes(unityVersion);
            md5.TransformBlock(unityVersionBytes, 0, unityVersionBytes.Length, unityVersionBytes, 0);

            md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            return ByteArrayToString(md5.Hash);
        }

        private static async Task<string> GetMscorlibAsync()
        {
            var monoVersion = "2021.6.24"; // TODO get this from BepInEx nuget package

            var mscorlibPath = Path.Combine(Context.CachePath, "mono", monoVersion, "mscorlib.dll");
            Directory.GetParent(mscorlibPath)!.Create();

            using var stream = await _httpClient.GetStreamAsync($"https://github.com/BepInEx/mono/releases/download/{monoVersion}/mono-x86.zip");
            using var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);
            zipArchive.GetEntry("mono/Managed/mscorlib.dll").ExtractToFile(mscorlibPath, true);

            return mscorlibPath;
        }

        private static async Task<string> GetUnityLibsAsync(string unityVersion)
        {
            var unityBaseLibsDirectory = Path.Combine(Context.CachePath, "unity-libs", unityVersion);

            Directory.CreateDirectory(unityBaseLibsDirectory);
            foreach (var file in Directory.EnumerateFiles(unityBaseLibsDirectory, "*.dll"))
            {
                File.Delete(file);
            }

            using var httpClient = new HttpClient();
            using var zipStream = await httpClient.GetStreamAsync($"https://unity.bepinex.dev/libraries/{unityVersion}.zip");
            using var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            zipArchive.ExtractToDirectory(unityBaseLibsDirectory);

            return unityBaseLibsDirectory;
        }
    }
}
