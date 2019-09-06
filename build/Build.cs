using System;
using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using Nuke.Docker;
using Nuke.Common.BuildServers;

[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main() => Execute<Build>(x => x.LogoutFromDockerHub);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;
    [GitVersion] readonly GitVersion GitVersion;

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
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
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
        .DependsOn(LoginIntoDockerHub)
        .DependsOn(DetermineTag)
        .DependsOn(Test)
        .Executes(() =>
        {

            DockerTasks.DockerBuild(b =>
           b.SetFile(DockerFilePath.ToString())
            .SetPath(".")
           .SetTag($"{repo}:{tag}")
       );
        });

    Target LoginIntoDockerHub => _ => _
        .DependsOn(CheckDockerVersion)
        .Executes(() =>
        {
            DockerTasks.DockerLogin(l => l
            .SetServer("docker.pkg.github.com")
            .SetUsername(user)
                .SetPassword(GitHubAccessToken)

            );
        });

    Target StartPosgreSql => _ => _
      .DependsOn(CheckDockerVersion)
      .Executes(() =>
      {
          DockerTasks.DockerRun(l =>
          l
          .SetDetach(true)
          .SetName("travis_db")
          .SetPublish("1234:5432")
          .SetEnv("POSTGRES_USER=admin", "POSTGRES_PASSWORD=1q2w3e", "POSTGRES_DB=travisdb")
          .SetImage("postgres")
          );
      });


    Target Test => _ => _
       .DependsOn(Compile)
       .DependsOn(StartPosgreSql)
       .Executes(() =>
       {

           DotNetTest(l => l.SetProjectFile(TestsProject));
       });

    Target DetermineTag => _ => _
      .Executes(() =>
      {
          if (IsServerBuild)
          {
              if (Travis.Instance != null)
              {
                  branch ="travis-" + Travis.Instance.Branch.ToString();
                  buildNumber = Travis.Instance.BuildNumber.ToString();
                  var pullId = Travis.Instance.PullRequest;
                  if (pullId.ToLower() != "false")
                  {
                      branch = $"travis-Pull-{pullId}";
                  }
                  tag = $"{branch}-{buildNumber}";
                  if (Travis.Instance.Branch.ToLower() == "master")
                  {

                      tag = "travis-latest";
                  }
              }
              if (AppVeyor.Instance != null)
              {
                  branch = "appveyor-"+AppVeyor.Instance.RepositoryBranch.ToString();
                  buildNumber = AppVeyor.Instance.BuildNumber.ToString();
                  try
                  {
                      var pullId = AppVeyor.Instance.PullRequestNumber.ToString();
                      if (pullId.ToLower() != "0")
                      {
                          branch = $"appveyor-Pull-{pullId}";
                      }
                  }
                  catch (Exception)
                  {

                    //  throw;
                  }
                
                  tag = $"{branch}-{buildNumber}";
                  if (AppVeyor.Instance.RepositoryBranch.ToLower() == "master")
                  {

                      tag = "appveyor-latest";
                     
                  }

              }

              if (TeamServices.Instance != null)
              {
                  branch = "azuredevops-"+TeamServices.Instance.SourceBranchName.ToString();
                  buildNumber = TeamServices.Instance.BuildNumber.ToString();
                  var pullId = TeamServices.Instance.PullRequestId.ToString();
                  if (pullId.ToLower() != "null")
                  {
                      branch = $"azuredevops-Pull-{pullId}";
                  }
                  tag = $"{branch}-{buildNumber}";
                  if (TeamServices.Instance.SourceBranchName.ToLower() == "master")
                  {

                      tag = "azuredevops-latest";
                  }
              }

              if (TeamServices.Instance==null && AppVeyor.Instance == null && Travis.Instance == null)
              {
                  branch = GitRepository.Branch;

                  if (GitRepository.Branch.ToLower() == "master")
                  {

                      tag = "github-latest";
                  }
                  else
                  {
                      tag = $"github-{branch}";
                  }
              }
          }        
      });

    Target PushDockerImage => _ => _
        .DependsOn(BuildDockerImage)
        .Executes(() =>
        {
            DockerTasks.DockerPush(p =>
                p.SetName($"{repo}:{tag}"));
        });

    Target LogoutFromDockerHub => _ => _
        .DependsOn(PushDockerImage)
        .Executes(() =>
        {
            DockerTasks.DockerLogout(l => l
           .SetServer("docker.pkg.github.com")
           );

        });
    Target CheckBranch => _ => _
       .Executes(() =>
       {
           Console.WriteLine(GitRepository.Branch);
       });


}
