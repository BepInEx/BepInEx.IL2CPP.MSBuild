using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BepInEx.IL2CPP.MSBuild.Shared;
using Il2CppInterop.Common;
using Il2CppInterop.Generator;
using Il2CppInterop.Generator.MetadataAccess;
using Il2CppInterop.Generator.Runners;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Task = System.Threading.Tasks.Task;

namespace BepInEx.IL2CPP.MSBuild.Runner
{
    public class Il2CppInteropManager
    {
        private readonly TaskLoggingHelper _logger;

        public Il2CppInteropManager(TaskLoggingHelper logger)
        {
            _logger = logger;
        }

        public async Task<string> GenerateAsync(GameLibsPackage gameLibsPackage, string il2CppInteropVersion)
        {
            var outputDirectory = Path.Combine(Context.CachePath, "game-libs", gameLibsPackage.Id, gameLibsPackage.Version, "interop", il2CppInteropVersion);

            var hashPath = Path.Combine(outputDirectory, "hash.txt");
            var hash = ComputeHash(gameLibsPackage.DummyDirectory, gameLibsPackage.UnityVersion);

            if (File.Exists(hashPath) && File.ReadAllText(hashPath) == hash)
            {
                _logger.LogMessage(MessageImportance.High, $"Reused {gameLibsPackage.Id} interop assemblies from cache");
                return outputDirectory;
            }

            _logger.LogMessage(MessageImportance.High, $"Generating interop assemblies for {gameLibsPackage.Id} (version: {gameLibsPackage.Version}, unity version: {gameLibsPackage.UnityVersion}, il2cppinterop version: {il2CppInteropVersion})");

            await RunIl2CppInteropGenerator(gameLibsPackage, outputDirectory);

            File.WriteAllText(hashPath, hash);

            return outputDirectory;
        }

        private async Task RunIl2CppInteropGenerator(GameLibsPackage gameLibsPackage, string outputDirectory)
        {
            var sourceFiles = Directory.GetFiles(gameLibsPackage.DummyDirectory, "*.dll");
            using var source = new CecilMetadataAccess(sourceFiles);
            Il2CppInteropGenerator.Create(new GeneratorOptions
                {
                    Source = (List<AssemblyDefinition>)source.Assemblies,
                    OutputDir = outputDirectory,
                    UnityBaseLibsDir = await GetUnityLibsAsync(gameLibsPackage.UnityVersion),
                })
                .AddLogger(new TaskLogger(_logger))
                .AddInteropAssemblyGenerator()
                .Run();
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

            static void HashFile(ICryptoTransform hash, string file)
            {
                const int defaultCopyBufferSize = 81920;
                using var fs = File.OpenRead(file);
                var buffer = new byte[defaultCopyBufferSize];
                int read;
                while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                    hash.TransformBlock(buffer, 0, read, buffer, 0);
            }

            static void HashString(ICryptoTransform hash, string str)
            {
                var buffer = Encoding.UTF8.GetBytes(str);
                hash.TransformBlock(buffer, 0, buffer.Length, buffer, 0);
            }

            foreach (var file in Directory.EnumerateFiles(dummyDirectory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                HashString(md5, Path.GetFileName(file));
                HashFile(md5, file);
            }

            HashString(md5, typeof(InteropAssemblyGenerator).Assembly.GetName().Version.ToString());
            HashString(md5, unityVersion);

            md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            return ByteArrayToString(md5.Hash);
        }

        private static async Task<string> GetUnityLibsAsync(string unityVersion)
        {
            var unityBaseLibsDirectory = Path.Combine(Context.CachePath, "unity-libs", unityVersion);

            Directory.CreateDirectory(unityBaseLibsDirectory);

            var etagPath = Path.Combine(unityBaseLibsDirectory, "etag");

            using var responseMessage = await DownloadUtility.DownloadAsync($"https://unity.bepinex.dev/libraries/{unityVersion}.zip", etagPath);

            if (responseMessage == null)
            {
                return unityBaseLibsDirectory;
            }

            foreach (var file in Directory.EnumerateFiles(unityBaseLibsDirectory, "*.dll"))
            {
                File.Delete(file);
            }

            using var zipStream = await responseMessage.Content.ReadAsStreamAsync();
            using var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            zipArchive.ExtractToDirectory(unityBaseLibsDirectory);

            return unityBaseLibsDirectory;
        }
    }
}
