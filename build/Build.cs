using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI.AppVeyor;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.CI.TravisCI;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main() => Execute<Build>(x => x.LogoutFromGithubDockerRegistry);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;

    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath TestsDirectory => RootDirectory / "test";
    AbsolutePath TestsProject => RootDirectory / "test/AspNetCoreDevOps.Controllers.Tests/AspNetCoreDevOps.Controllers.Tests.csproj";
    AbsolutePath OutputDirectory => RootDirectory / "output";
    AbsolutePath DockerFilePath => RootDirectory / "dockerfile";

    string tag = "";
    string buildNumber = "";
    string branch = "";
    [Parameter("Github access token for packages")]
    readonly string GitHubAccessToken;
    string repo = "docker.pkg.github.com/iambipinpaul/aspnetcoredevops/aspnetcoredevops";
    string user = "iambipinpaul";

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            TestsDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            EnsureCleanDirectory(OutputDirectory);
        });
    Target Restore => _ => _
    .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(b => b
                .SetProjectFile(Solution));
        });
    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(b => b
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });
    Target CheckDockerVersion => _ => _
      .DependsOn(CheckBranch)
        .Executes(() =>
        {
            DockerTasks.DockerVersion();
        });
    Target BuildDockerImage => _ => _
        .DependsOn(LoginIntoGithubDockerRegistry)
        .DependsOn(DetermineTag)
        .DependsOn(Test)
    .DependsOn(StopPosgreSql)
        .Executes(() =>
        {
            var buildContext = ".";
            if (IsLocalBuild)
            {
                buildContext = "../";
            }
            DockerTasks.DockerBuild(b =>
            b.SetFile(DockerFilePath.ToString())
           .SetPath(buildContext)
           .SetTag($"{repo}:{tag}")
       );
        });
    Target LoginIntoGithubDockerRegistry => _ => _
        .DependsOn(CheckDockerVersion)
        .Executes(() =>
        {
            DockerTasks.DockerLogin(b => b
            .SetServer("docker.pkg.github.com")
            .SetUsername(user)
             .SetPassword(GitHubAccessToken)

            );
        });
    Target StartPosgreSql => _ => _
      .DependsOn(CheckDockerVersion)
      .Executes(() =>
      {
          DockerTasks.DockerRun(b =>
          b
          .SetDetach(true)
          .SetName("test_db")
          .SetPublish("1234:5432")
          .SetEnv("POSTGRES_USER=admin", "POSTGRES_PASSWORD=1q2w3e", "POSTGRES_DB=testdb")
          .SetImage("postgres")
          );
      });
    Target StopPosgreSql => _ => _
    .After(Test)
    .AssuredAfterFailure()
    .Executes(() =>
    {

        DockerTasks.DockerRm(b =>b
          .SetContainers("test_db")
          .SetForce(true));

    });
    Target Test => _ => _
     .DependsOn(Compile)
    .DependsOn(StartPosgreSql)
    .Executes(() =>
       {

           DotNetTest(b => b.SetProjectFile(TestsProject));
       });
    Target DetermineTag => _ => _
      .Executes(() =>
      {
          if (IsServerBuild)
          {
              if (TravisCI.Instance != null)
              {
                  branch = "travis-" + TravisCI.Instance.Branch.ToString();
                  buildNumber = TravisCI.Instance.BuildNumber.ToString();
                  tag = $"{branch}-{buildNumber}";
                  if (TravisCI.Instance.Branch.ToLower() == "master")
                  {
                      tag = "travis-latest";
                  }
              }
              else if (AppVeyor.Instance != null)
              {
                  branch = "appveyor-" + AppVeyor.Instance.RepositoryBranch.ToString();
                  tag = $"{branch}-{buildNumber}";
                  if (AppVeyor.Instance.RepositoryBranch.ToLower() == "master")
                  {
                      tag = "appveyor-latest";
                  }
              }
              else if (AzurePipelines.Instance != null)
              {
                  branch = "azuredevops-" + AzurePipelines.Instance.SourceBranchName.ToString();
                  buildNumber = AzurePipelines.Instance.BuildNumber.ToString();
                  tag = $"{branch}-{buildNumber}";
                  if (AzurePipelines.Instance.SourceBranchName.ToLower() == "master")
                  {
                      tag = "azuredevops-latest";
                  }
              }
              else if (GitHubActions.Instance != null)
              {
                  if (GitRepository.Branch.ToLower() == "master")
                  {
                      tag = "github-latest";
                  }
                  else
                  {
                      tag = $"github-{GitRepository.Branch.Split('/').Last()}-{GitHubActions.Instance.GitHubSha}";
                  }
              }
          }
          else
          {
              Console.WriteLine("Not on server");
              tag = $"not-server-build";
          }
      });
    Target PushDockerImage => _ => _
        .DependsOn(BuildDockerImage)
        .Executes(() =>
        {
            DockerTasks.DockerPush(b =>
                b.SetName($"{repo}:{tag}"));
        });
    Target LogoutFromGithubDockerRegistry => _ => _
        .DependsOn(PushDockerImage)
        .Executes(() =>
        {
            DockerTasks.DockerLogout(b => b
           .SetServer("docker.pkg.github.com")
           );
        });
    Target CheckBranch => _ => _
       .Executes(() =>
       {
           Console.WriteLine(GitRepository.Branch);
       });
}
