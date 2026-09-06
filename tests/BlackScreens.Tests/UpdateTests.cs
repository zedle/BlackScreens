using BlackScreens.Updates;
using Xunit;

namespace BlackScreens.Tests;

public sealed class ReleaseVersionTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("1.2.3.0", "1.2.3")]
    [InlineData("1.2.3+f378c09", "1.2.3")]
    [InlineData("1.2.3-beta.1", "1.2.3")]
    [InlineData("V2.0", "2.0.0")]
    [InlineData("  v1.0.0  ", "1.0.0")]
    public void Parse_reads_the_shapes_tags_and_builds_use(string text, string expected)
    {
        Assert.Equal(Version.Parse(expected), ReleaseVersions.TryParse(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("nightly")]
    [InlineData("v")]
    public void Parse_returns_null_for_anything_else(string? text)
    {
        Assert.Null(ReleaseVersions.TryParse(text));
    }

    [Theory]
    [InlineData("1.0.0", "v1.0.1", true)]
    [InlineData("1.0.0", "v1.1.0", true)]
    [InlineData("1.0.0", "v2.0.0", true)]
    [InlineData("1.0.0", "v1.0.0", false)]
    [InlineData("1.0.0+abc", "v1.0.0", false)]
    [InlineData("1.2.0", "v1.1.9", false)]
    [InlineData("1.0.0", "nonsense", false)]
    [InlineData("nonsense", "v1.0.0", false)]
    public void IsNewer_only_moves_forward(string current, string candidate, bool expected)
    {
        Assert.Equal(expected, ReleaseVersions.IsNewer(current, candidate));
    }
}

public sealed class UpdateTargetTests
{
    private static readonly string[] Assets =
    [
        "BlackScreens-1.1.0-setup.exe",
        "BlackScreens-1.1.0-win-x64.exe",
        "BlackScreens-1.1.0-win-x64-runtime.zip"
    ];

    [Fact]
    public void An_installed_copy_takes_the_installer()
    {
        Assert.Equal("BlackScreens-1.1.0-setup.exe", UpdateTarget.PickAsset(InstallKind.Installed, Assets));
    }

    [Fact]
    public void A_portable_copy_takes_the_self_contained_exe()
    {
        // Never the runtime zip: that build cannot start without the .NET runtime installed.
        Assert.Equal("BlackScreens-1.1.0-win-x64.exe", UpdateTarget.PickAsset(InstallKind.Portable, Assets));
    }

    [Fact]
    public void A_release_without_a_usable_asset_is_skipped()
    {
        Assert.Null(UpdateTarget.PickAsset(InstallKind.Installed, ["notes.txt"]));
        Assert.Null(UpdateTarget.PickAsset(InstallKind.Portable, []));
    }

    [Fact]
    public void The_uninstaller_beside_the_exe_marks_an_installed_copy()
    {
        var folder = Path.Combine(Path.GetTempPath(), "bs-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var exe = Path.Combine(folder, "BlackScreens.exe");
            File.WriteAllText(exe, string.Empty);
            Assert.Equal(InstallKind.Portable, UpdateTarget.Detect(exe));

            File.WriteAllText(Path.Combine(folder, "Uninstall.exe"), string.Empty);
            Assert.Equal(InstallKind.Installed, UpdateTarget.Detect(exe));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void An_unknown_location_counts_as_portable()
    {
        Assert.Equal(InstallKind.Portable, UpdateTarget.Detect(null));
        Assert.Equal(InstallKind.Portable, UpdateTarget.Detect(string.Empty));
    }
}

public sealed class UpdateCheckerTests
{
    private static string Release(
        string tag = "v1.1.0",
        bool prerelease = false,
        bool draft = false,
        string assets = """
            {"name": "BlackScreens-1.1.0-setup.exe", "size": 68474629,
             "browser_download_url": "https://github.com/zedle/BlackScreens/releases/download/v1.1.0/BlackScreens-1.1.0-setup.exe"},
            {"name": "BlackScreens-1.1.0-win-x64.exe", "size": 75114664,
             "browser_download_url": "https://github.com/zedle/BlackScreens/releases/download/v1.1.0/BlackScreens-1.1.0-win-x64.exe"}
            """) =>
        $$"""
        {
          "tag_name": "{{tag}}",
          "draft": {{(draft ? "true" : "false")}},
          "prerelease": {{(prerelease ? "true" : "false")}},
          "html_url": "https://github.com/zedle/BlackScreens/releases/tag/{{tag}}",
          "assets": [{{assets}}]
        }
        """;

    [Fact]
    public void A_newer_release_becomes_an_update()
    {
        var update = UpdateChecker.ParseRelease(Release(), "1.0.0", InstallKind.Installed);

        Assert.NotNull(update);
        Assert.Equal(new Version(1, 1, 0), update.Version);
        Assert.Equal("BlackScreens-1.1.0-setup.exe", update.AssetName);
        Assert.Equal(68474629, update.AssetSize);
        Assert.EndsWith("BlackScreens-1.1.0-setup.exe", update.AssetUrl, StringComparison.Ordinal);
        Assert.Equal("https://github.com/zedle/BlackScreens/releases/tag/v1.1.0", update.ReleaseUrl);
    }

    [Fact]
    public void A_portable_copy_is_pointed_at_the_portable_asset()
    {
        var update = UpdateChecker.ParseRelease(Release(), "1.0.0", InstallKind.Portable);

        Assert.NotNull(update);
        Assert.Equal("BlackScreens-1.1.0-win-x64.exe", update.AssetName);
    }

    [Fact]
    public void The_same_or_an_older_release_is_not_an_update()
    {
        Assert.Null(UpdateChecker.ParseRelease(Release("v1.0.0"), "1.0.0", InstallKind.Installed));
        Assert.Null(UpdateChecker.ParseRelease(Release("v0.9.0"), "1.0.0", InstallKind.Installed));
    }

    [Fact]
    public void Drafts_and_prereleases_are_ignored()
    {
        Assert.Null(UpdateChecker.ParseRelease(Release(draft: true), "1.0.0", InstallKind.Installed));
        Assert.Null(UpdateChecker.ParseRelease(Release(prerelease: true), "1.0.0", InstallKind.Installed));
    }

    [Fact]
    public void A_release_with_nothing_to_download_is_ignored()
    {
        Assert.Null(UpdateChecker.ParseRelease(Release(assets: ""), "1.0.0", InstallKind.Installed));
        Assert.Null(UpdateChecker.ParseRelease(
            Release(assets: """{"name": "checksums.txt", "size": 12, "browser_download_url": "https://example.com/c.txt"}"""),
            "1.0.0",
            InstallKind.Installed));
    }

    [Fact]
    public void Rubbish_never_throws()
    {
        Assert.Null(UpdateChecker.ParseRelease("not json", "1.0.0", InstallKind.Installed));
        Assert.Null(UpdateChecker.ParseRelease("[]", "1.0.0", InstallKind.Installed));
        Assert.Null(UpdateChecker.ParseRelease("{}", "1.0.0", InstallKind.Installed));
        Assert.Null(UpdateChecker.ParseRelease(
            """{"tag_name": "v1.1.0", "assets": "nope"}""", "1.0.0", InstallKind.Installed));
    }
}

public sealed class UpdateServiceTests
{
    [Fact]
    public async Task Nothing_happens_on_a_schedule_while_updates_are_off()
    {
        var settings = new AppSettings { AutoUpdate = false };
        var service = new UpdateService(settings, InstallKind.Portable);

        Assert.Equal(UpdateResult.Disabled, await service.CheckAsync(manual: false));
        Assert.Null(settings.LastUpdateCheckUtc);
    }

    [Fact]
    public void A_check_is_due_when_it_has_never_run()
    {
        var service = new UpdateService(new AppSettings(), InstallKind.Portable);

        Assert.True(service.IsCheckDue(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void A_check_is_not_due_again_for_a_day()
    {
        var now = DateTimeOffset.UtcNow;
        var settings = new AppSettings { LastUpdateCheckUtc = now.AddHours(-3) };
        var service = new UpdateService(settings, InstallKind.Portable);

        Assert.False(service.IsCheckDue(now));
        Assert.True(service.IsCheckDue(now.AddHours(22)));
    }

    [Theory]
    [InlineData(UpdateResult.UpToDate, "You are on the latest release.")]
    [InlineData(UpdateResult.Failed, "The update could not be fetched. See the error log.")]
    [InlineData(UpdateResult.Disabled, "Automatic updates are off.")]
    public void Results_read_as_sentences(UpdateResult result, string expected)
    {
        Assert.Equal(expected, UpdateService.Describe(result, null));
    }

    [Fact]
    public void A_ready_update_names_its_version()
    {
        var update = new UpdateInfo(new Version(1, 2, 0), "v1.2.0", "a.exe", "https://example.com/a.exe", 1, "https://example.com");

        Assert.Equal("Version 1.2.0 is ready to install.", UpdateService.Describe(UpdateResult.Ready, update));
    }
}
