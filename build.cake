var target = Argument("target", "Build");

var buildId = EnvironmentVariable("GITHUB_RUN_NUMBER");

var @ref = EnvironmentVariable("GITHUB_REF");
const string prefix = "refs/tags/";
var tag = !string.IsNullOrEmpty(@ref) && @ref.StartsWith(prefix) ? @ref.Substring(prefix.Length) : null;

Task("Build")
    .Does(() =>
{
    var settings = new DotNetCoreBuildSettings
    {
        Configuration = "Release",
        MSBuildSettings = new DotNetCoreMSBuildSettings()
    };

    if (tag != null) 
    {
        settings.MSBuildSettings.Properties["Version"] = new[] { tag };
    }
    else if (buildId != null)
    {
        settings.VersionSuffix = "ci." + buildId;
    }

    DotNetCoreBuild("./BepInEx.IL2CPP.MSBuild", settings);
});

RunTarget(target);
