using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.Docker.DockerTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.GitHub.GitHubTasks;

class Build : NukeBuild
{
    public Build()
    {
        // Redirect output from STERR to STDOUT.
        DockerLogger = (_, message) => Serilog.Log.Debug(message);
    }
    public static int Main() => Execute<Build>(b => b.Compile);

    [GitRepository]
    readonly GitRepository GitRepository;
    
    [Solution("NukeSandbox.sln", SuppressBuildProjectCheck = true)]
    readonly Solution Solution;
    
    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("The PAT used in order to push the Docker image to the container registry as an owner of the repository")]
    readonly string GitHubPersonalAccessToken;

    [Parameter("The GitHub user account that will be used to push the Docker image to the container registry")]
    readonly string GitHubUsername;

    readonly string GitHubImageRegistry = "docker.pkg.github.com";

    [Parameter("The docker image name.")]
    readonly string ImageName = "magic-8-ball-api:dockerfile";

    static readonly string ApiAssemblyName = "MagicEightBall.API";
    
    readonly AbsolutePath ApiProject = RootDirectory / ApiAssemblyName;

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() => DotNetClean(c => c.SetProject(ApiAssemblyName)));
    
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
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });
    
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
        .Requires(
            () => GitHubPersonalAccessToken,
            () => GitHubUsername,
            () => ImageName)
        .Executes(() =>
        {
            DockerLogin(settings => settings
                .SetServer(GitHubImageRegistry)
                .SetUsername(GitHubUsername)
                .SetPassword(GitHubPersonalAccessToken)
                .DisableProcessLogOutput());

            var (repositoryOwner, repositoryName) = GetGitHubRepositoryInfo(GitRepository);
            var targetImageName =
                $"{GitHubImageRegistry}/{repositoryOwner.ToLowerInvariant()}/{repositoryName}/{ImageName}";

            DockerTag(settings => settings
                .SetSourceImage(ImageName)
                .SetTargetImage(targetImageName));

            DockerPush(settings => settings.SetName(targetImageName));
        });
}