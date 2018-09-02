#addin "Cake.Issues&prerelease"
#addin "Cake.Issues.MsBuild&prerelease"
#addin "Cake.Issues.InspectCode&prerelease"
#addin "Cake.Issues.Reporting&prerelease"
#addin "Cake.Issues.Reporting.Generic&prerelease"
#tool "nuget:?package=MSBuild.Extension.Pack"
#tool "nuget:?package=JetBrains.ReSharper.CommandLineTools"

var target = Argument("target", "Default");

var repoRootFolder = MakeAbsolute(Directory("./"));
var outputFolder = repoRootFolder.Combine("output");
var msBuildXmlFileLoggerLog = outputFolder.CombineWithFilePath("msbuild-xmlfilelogger.log");
var inspectCodeLog = outputFolder.CombineWithFilePath("inspectCode.log");
var issues = new List<IIssue>();

Task("Build")
    .Does(() =>
{
    var solutionFile = repoRootFolder.Combine("src").CombineWithFilePath("ClassLibrary1.sln");

    NuGetRestore(solutionFile);

    var settings =
        new MSBuildSettings()
            .WithTarget("Rebuild");

    // XML File Logger
    settings =
        settings.WithLogger(
            Context.Tools.Resolve("MSBuild.ExtensionPack.Loggers.dll").FullPath,
            "XmlFileLogger",
            string.Format(
                "logfile=\"{0}\";verbosity=Detailed;encoding=UTF-8",
                msBuildXmlFileLoggerLog));

    EnsureDirectoryExists(outputFolder);
    MSBuild(solutionFile, settings);
});

Task("Run-InspectCode")
    .Does(() =>
{
    var settings = new InspectCodeSettings() {
        OutputFile = inspectCodeLog
    };

    InspectCode(repoRootFolder.Combine("src").CombineWithFilePath("ClassLibrary1.sln"), settings);
});

Task("Read-Issues")
    .IsDependentOn("Build")
    .IsDependentOn("Run-InspectCode")
    .Does(() =>
{
    var settings =
        new ReadIssuesSettings(repoRootFolder)
        {
            Format = IssueCommentFormat.Markdown
        };

    // Read issues from log files.
    issues.AddRange(
        ReadIssues(
            new List<IIssueProvider>
            {
                MsBuildIssuesFromFilePath(
                    msBuildXmlFileLoggerLog,
                    MsBuildXmlFileLoggerFormat),
                InspectCodeIssuesFromFilePath(
                    inspectCodeLog)
            },
            settings));

    // Add manual issue.
    issues.Add(
        NewIssue(
            "Something went wrong",
            "MyCakeScript",
            "My Cake Script")
            .InFile("myfile.txt", 42)
            .WithPriority(IssuePriority.Warning)
            .Create()
    );

    Information("{0} issues are found.", issues.Count());
});

Task("Create-Report")
    .IsDependentOn("Read-Issues")
    .Does(() =>
{
    var reportFile = outputFolder.CombineWithFilePath("report.html");

    var settings = 
        GenericIssueReportFormatSettings
            .FromEmbeddedTemplate(GenericIssueReportTemplate.HtmlDxDataGrid)
            .WithOption(HtmlDxDataGridOption.Theme, DevExtremeTheme.MaterialBlueLight);
    // Create HTML report using DevExpress template.
    CreateIssueReport(
        issues,
        GenericIssueReportFormat(settings),
        repoRootFolder,
        outputFolder.CombineWithFilePath("report.html"));

    if (AppVeyor.IsRunningOnAppVeyor)
    {
        AppVeyor.UploadArtifact(reportFile);
    }
});

Task("Default")
    .IsDependentOn("Create-Report");

RunTarget(target);