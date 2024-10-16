using System;
using System.Diagnostics.CodeAnalysis;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Polly;
using Serilog;
using static Nuke.Common.Tools.Docker.DockerTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.Git.GitTasks;
using static Nuke.Common.Tools.GitHub.GitHubTasks;

class Build : NukeBuild
{
    const string ApiAssemblyName = "MagicEightBall.API";
    const string GitHubImageRegistry = "docker.pkg.github.com";

    public Build()
    {
        // Redirect output from STDERR to STDOUT.
        DockerLogger = (_, message) => Log.Debug(message);
        GitLogger = (_, message) => Log.Debug(message);
    }

    public static int Main() => Execute<Build>(b => b.Compile);

    [Solution("NukeSandbox.sln", SuppressBuildProjectCheck = true)]
    readonly Solution Solution;

    [GitRepository]
    readonly GitRepository GitRepository;

    [GitVersion(UpdateBuildNumber = true)]
    readonly GitVersion GitVersion;

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("The PAT used in order to push the Docker image to the container registry")]
    readonly string GitHubPersonalAccessToken;

    [Parameter("The GitHub user account that will be used to push the Docker image to the container registry")]
    readonly string GitHubUsername;

    [Parameter("The git author username, used for tagging release commits.")]
    readonly string GitAuthorUsername;

    [Parameter("The git author email, used for tagging release commits.")]
    readonly string GitAuthorEmail;

    [Parameter("The docker image name.")]
    readonly string ImageName = "magic-8-ball-api:dockerfile";

    readonly AbsolutePath ApiProject = RootDirectory / "src" / ApiAssemblyName;

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() => DotNetClean(c => c.SetProject(ApiProject)));

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Clean, Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(ApiProject)
                .SetConfiguration(Configuration)
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion)
                .EnableNoRestore());

            Log.Information("Current semver: {@Version}", GitVersion.MajorMinorPatch);
        });

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    Target BuildApiImageWithBuiltInContainerSupport => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetPublish(c => c
                .SetProject(ApiProject)
                .SetProcessArgumentConfigurator(args => args
                    .Add("--os linux")
                    .Add("--arch x64"))
                .SetConfiguration("Debug")
                .SetPublishProfile("DefaultContainer"));
        });

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    Target BuildApiImageWithDockerfile => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DockerBuild(s => s
                .SetProcessEnvironmentVariable("DOCKER_BUILDKIT", "1")
                .SetProcessWorkingDirectory(RootDirectory)
                .SetFile(ApiProject / "Dockerfile")
                .SetPath(".")
                .SetTag(ImageName));
        });

    Target PushImageToGitHubRegistry => _ => _
        .OnlyWhenDynamic(() => GitRepository.IsOnMainOrMasterBranch())
        .Requires(
            () => GitHubPersonalAccessToken,
            () => GitHubUsername,
            () => ImageName)
        .Triggers(TagReleaseCommit)
        .Executes(() =>
        {
            Policy
                .Handle<Exception>()
                .WaitAndRetry(5,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (ex, _, retryCount, _) =>
                    {
                        Log.Warning(ex, "Docker login was unsuccessful");
                        Log.Information("Attempting to login into GitHub Docker image registry. Try #{RetryCount}", retryCount);
                    })
                .Execute(() => DockerLogin(settings => settings
                    .SetServer(GitHubImageRegistry)
                    .SetUsername(GitHubUsername)
                    .SetPassword(GitHubPersonalAccessToken)
                    .DisableProcessLogOutput()));

            var repositoryOwner = GitRepository.GetGitHubOwner();
            var repositoryName = GitRepository.GetGitHubName();
            var targetImageName =
                $"{GitHubImageRegistry}/{repositoryOwner.ToLowerInvariant()}/{repositoryName}/{ImageName}";

            DockerTag(settings => settings
                .SetSourceImage(ImageName)
                .SetTargetImage(targetImageName));

            var tagWithSemver = targetImageName + '-' + GitVersion.MajorMinorPatch;
            DockerTag(settings => settings
                .SetSourceImage(ImageName)
                .SetTargetImage(tagWithSemver));

            DockerPush(settings => settings.SetName(targetImageName));
            DockerPush(settings => settings.SetName(tagWithSemver));
        });

    Target TagReleaseCommit => _ => _
        .DependsOn(PushImageToGitHubRegistry)
        .OnlyWhenDynamic(() => GitRepository.IsOnMainOrMasterBranch())
        .Requires(
            () => GitAuthorEmail,
            () => GitAuthorUsername)
        .Executes(() =>
        {
            Git($"config user.email \"{GitAuthorEmail}\"");
            Git($"config user.name \"{GitAuthorUsername}\"");

            Git($"tag -a {GitVersion.FullSemVer} -m \"Release: '{GitVersion.FullSemVer}'\"");
            Git("push --follow-tags");
        });
}