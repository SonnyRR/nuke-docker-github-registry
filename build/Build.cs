using JetBrains.Annotations;
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
using System;
using static Nuke.Common.Tools.Docker.DockerTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.Git.GitTasks;
using static Nuke.Common.Tools.GitHub.GitHubTasks;

class Build : NukeBuild
{
	const string ApiAssemblyName = "MagicEightBall.API";
	const string ContainerRegistry = "ghcr.io";
	const string Image = "magic-8-ball-api";
	const string BuiltInImageTag = Image + ":built-in";
	const string DockerfileImageTag = Image + ":dockerfile";

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
	readonly string ContainerRegistryPAT;

	[Parameter("The GitHub user account that will be used to push the Docker image to the container registry")]
	readonly string ContainerRegistryUsername;

	[Parameter("The git author username, used for tagging release commits.")]
	readonly string GitAuthorUsername;

	[Parameter("The git author email, used for tagging release commits.")]
	readonly string GitAuthorEmail;

	readonly AbsolutePath ApiProject = RootDirectory / "src" / ApiAssemblyName;

	Target Clean => _ => _
		.Before(Restore)
		.Executes(() => DotNetClean(c => c.SetProject(ApiProject)));

	Target Restore => _ => _
		.Executes(() => DotNetRestore(s => s.SetProjectFile(Solution)));

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

	[UsedImplicitly]
	Target BuildApiImageWithBuiltInContainerSupport => _ => _
		.Description("Builds the API image with the built-in container support from dotnet.")
		.DependsOn(Compile)
		.Executes(() =>
		{
			DotNetPublish(c => c
				.SetProject(ApiProject)
				.SetOperatingSystem("linux")
				.SetArchitecture("x64")
				.SetConfiguration("Debug")
				.SetPublishProfile("DefaultContainer"));
		});

	[UsedImplicitly]
	Target BuildApiImageWithDockerfile => _ => _
		.Description("Builds the API image with a multi-stage build through a Dockerfile.")
		.DependsOn(Compile)
		.Executes(() =>
		{
			DockerBuild(s => s
				.SetProcessEnvironmentVariable("DOCKER_BUILDKIT", "1")
				.SetProcessWorkingDirectory(RootDirectory)
				.SetFile(ApiProject / "Dockerfile")
				.SetPath(".")
				.SetTag(DockerfileImageTag));
		});

	[UsedImplicitly]
	Target PushImagesToContainerRegistry => _ => _
		.Description("Pushes built OCI images to a container registry.")
		.OnlyWhenDynamic(() =>
			GitRepository.IsOnMainOrMasterBranch() ||
			GitRepository.IsOnFeatureBranch() ||
			GitRepository.IsOnDevelopBranch())
		.WhenSkipped(DependencyBehavior.Skip)
		.DependsOn(BuildApiImageWithBuiltInContainerSupport, BuildApiImageWithDockerfile)
		.Triggers(TagReleaseCommit)
		.Requires(
			() => ContainerRegistryPAT,
			() => ContainerRegistryUsername)
		.Executes(() =>
		{
			PublishImage(DockerfileImageTag);
			PublishImage(BuiltInImageTag);
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


	/// <summary>
	/// Publishes an OCI image to a given container registry.
	/// </summary>
	/// <param name="imageName">The image tag, that needs to be pushed.</param>
	private void PublishImage(string imageName)
	{
		ArgumentNullException.ThrowIfNullOrWhiteSpace(imageName);

		Policy
			.Handle<Exception>()
			.WaitAndRetry(5,
				retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
				(ex, _, retryCount, _) =>
				{
					Log.Warning(ex, "Docker login was unsuccessful");
					Log.Information("Attempting to login into GitHub Docker image registry. Try #{RetryCount}", retryCount);
				})
			.Execute(() => DockerLogin(s => s
				.SetServer(ContainerRegistry)
				.SetUsername(ContainerRegistryUsername)
				.SetPassword(ContainerRegistryPAT)
				.DisableProcessOutputLogging()));

		var repositoryOwner = GitRepository.GetGitHubOwner();
		var repositoryName = GitRepository.GetGitHubName();
		var targetImageName =
			$"{ContainerRegistry}/{repositoryOwner.ToLowerInvariant()}/{repositoryName}/{imageName}";

		DockerTag(s => s
			.SetSourceImage(imageName)
			.SetTargetImage(targetImageName));

		var tagWithSemver = targetImageName + '-' + GitVersion.MajorMinorPatch;
		DockerTag(s => s
			.SetSourceImage(imageName)
			.SetTargetImage(tagWithSemver));

		DockerPush(s => s.SetName(targetImageName));

		if (GitRepository.IsOnMainOrMasterBranch())
		{
			DockerTag(s => s
				.SetSourceImage(imageName)
				.SetTargetImage(targetImageName));

			DockerPush(s => s.SetName(tagWithSemver));
		}
	}
}