using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.GitHub;
using static Nuke.Common.Tools.Docker.DockerTasks;
using static Nuke.GitHub.GitHubTasks;

class Build : NukeBuild
{
    public static int Main() => Execute<Build>(b => b.PushImageToGitHubRegistry);

    [GitRepository]
    readonly GitRepository GitRepository;

    [Parameter("The PAT used in order to push the Docker image to the container registry as an owner of the repository")]
    readonly string GitHubPersonalAccessToken;

    [Parameter("The GitHub user account that will be used to push the Docker image to the container registry")]
    readonly string GitHubUsername;

    readonly string GitHubImageRegistry = "docker.pkg.github.com";

    readonly string ImageName = "alpine/git";

    Target PushImageToGitHubRegistry => _ => _
        .Requires(
            () => GitHubPersonalAccessToken,
            () => GitHubUsername)
        .Executes(() =>
        {
            DockerPull(c => c.SetName(ImageName));

            DockerLogin(cfg => cfg
                .SetServer(GitHubImageRegistry)
                .SetUsername(GitHubUsername)
                .SetPassword(GitHubPersonalAccessToken)
                .DisableProcessLogOutput());

            var (repositoryOwner, repositoryName) = GetGitHubRepositoryInfo(GitRepository);
            var targetImageName = $"{GitHubImageRegistry}/{repositoryOwner.ToLowerInvariant()}/{repositoryName}/alpine-git:nuke";

            DockerTag(settings => settings
                .SetSourceImage(ImageName)
                .SetTargetImage(targetImageName));

            DockerPush(settings => settings.SetName(targetImageName));
        });

}
