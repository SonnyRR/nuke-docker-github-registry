using System;
using System.Linq;
using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities;
using Polly;
using Serilog;
using static Nuke.Common.Tools.Docker.DockerTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.Git.GitTasks;
using static Nuke.Common.Tools.GitHub.GitHubTasks;

class Build : NukeBuild
{
    const string ApiAssemblyName = "MagicEightBall.API";
    const string ContainerRegistry = "ghcr.io";
    const string ImageName = "magic-8-ball-api";

    public static int Main() => Execute<Build>(b => b.Compile);

    [GitRepository]
    readonly GitRepository GitRepository;

    [GitVersion(UpdateBuildNumber = true)]
    readonly GitVersion GitVersion;

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Secret]
    [Parameter("The PAT used in order to push the Docker image to the container registry")]
    readonly string ContainerRegistryPAT;

    [Parameter("The GitHub user account that will be used to push the Docker image to the container registry")]
    readonly string ContainerRegistryUsername;

    [Parameter("The git author username, used for tagging release commits.")]
    readonly string GitAuthorUsername;

    [Parameter("The git author email, used for tagging release commits.")]
    readonly string GitAuthorEmail;

    readonly AbsolutePath ApiProject = RootDirectory / "src" / ApiAssemblyName;

    private bool ContainsPlaceholderCredentialsForGHCR
        => new[] { ContainerRegistryPAT, ContainerRegistryUsername }
            .Any(x => x == "placeholder");

    Target Clean => _ => _
        .Description("Cleans-up .NET build artifacts.")
        .Before(Restore)
        .Executes(() => DotNetClean(c => c.SetProject(ApiProject)));

    Target Restore => _ => _
        .Description("Restores NuGet project dependencies.")
        .Executes(() => DotNetRestore(s => s.SetProjectFile(ApiProject)));

    Target Compile => _ => _
        .Description("Compiles the solution.")
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

            Log.Information("Current semver: {@Version}", GitVersion.FullSemVer);
        });

    [UsedImplicitly]
    Target BuildImage => _ => _
        .Description("Builds the API image with a multi-stage build through a Dockerfile.")
        .DependsOn(Compile)
        .Executes(() =>
        {
            DockerBuild(s => s
                .SetProcessWorkingDirectory(RootDirectory)
                .SetFile(ApiProject / "Dockerfile")
                .SetPath(".")
                .SetTag(ImageName));
        });

    [UsedImplicitly]
    Target PushImage => _ => _
        .Description("Pushes the OCI image to a container registry.")
        .OnlyWhenDynamic(() => !ContainsPlaceholderCredentialsForGHCR)
        .WhenSkipped(DependencyBehavior.Execute)
        .When(ContainsPlaceholderCredentialsForGHCR, t => t.WhenSkipped(DependencyBehavior.Skip))
        .DependsOn(BuildImage)
        .Triggers(TagReleaseCommit)
        .Requires(
            () => ContainerRegistryPAT,
            () => ContainerRegistryUsername)
        .Executes(() =>
        {
            Policy
                .Handle<Exception>()
                .WaitAndRetry(5,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (ex, _, retryCount, _) =>
                    {
                        Log.Warning(ex, "Docker login was unsuccessful");
                        Log.Information("Attempting to login into GitHub Docker image registry. Try #{RetryCount}.", retryCount);
                    })
                .Execute(() => DockerLogin(s => s
                    .SetServer(ContainerRegistry)
                    .SetUsername(ContainerRegistryUsername)
                    .SetPassword(ContainerRegistryPAT)
                    .DisableProcessOutputLogging()));

            var repositoryOwner = GitRepository.GetGitHubOwner();
            var repositoryName = GitRepository.GetGitHubName();
            var targetImageName = $"{ContainerRegistry}/{repositoryOwner.ToLowerInvariant()}/{repositoryName}/{ImageName}";

            var semverImageTag = targetImageName + '-' + GitVersion.FullSemVer.ToLowerInvariant();

            DockerTag(s => s.SetSourceImage(ImageName).SetTargetImage(semverImageTag));
            DockerPush(s => s.SetName(semverImageTag));

            if (GitRepository.IsOnMainOrMasterBranch())
            {
                var latestImageTag = targetImageName + ":latest";
                DockerTag(s => s.SetSourceImage(ImageName).SetTargetImage(latestImageTag));
                DockerPush(s => s.SetName(latestImageTag));
            }
        });

    Target TagReleaseCommit => _ => _
        .Description("Creates a git tag with the current semantic version.")
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
