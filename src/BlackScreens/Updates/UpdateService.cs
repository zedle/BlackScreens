namespace BlackScreens.Updates;

public enum UpdateResult
{
    /// <summary>Automatic updates are off and this was not a manual check.</summary>
    Disabled,

    /// <summary>Checked recently enough that there is no point asking again.</summary>
    TooSoon,

    /// <summary>This is the newest release.</summary>
    UpToDate,

    /// <summary>The check or the download did not work. The reason is in the error log.</summary>
    Failed,

    /// <summary>A newer build has been downloaded and is waiting to be installed.</summary>
    Ready
}

/// <summary>
/// Checks the project's GitHub releases for a newer build and downloads it. Nothing here runs unless
/// the user turned updates on, or pressed the button.
/// </summary>
public sealed class UpdateService
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromHours(24);

    private readonly AppSettings _settings;
    private readonly InstallKind _kind;

    public UpdateService(AppSettings settings)
        : this(settings, UpdateTarget.Detect())
    {
    }

    public UpdateService(AppSettings settings, InstallKind kind)
    {
        _settings = settings;
        _kind = kind;
    }

    /// <summary>The update that has been downloaded and is ready to install, if any.</summary>
    public UpdateInfo? Pending { get; private set; }

    public bool IsBusy { get; private set; }

    /// <summary>True when enough time has passed for a scheduled check.</summary>
    public bool IsCheckDue(DateTimeOffset now) =>
        _settings.LastUpdateCheckUtc is not { } last || now - last >= CheckEvery;

    public async Task<UpdateResult> CheckAsync(bool manual, CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return UpdateResult.TooSoon;
        }

        if (!manual && !_settings.AutoUpdate)
        {
            return UpdateResult.Disabled;
        }

        if (!manual && !IsCheckDue(DateTimeOffset.UtcNow))
        {
            return UpdateResult.TooSoon;
        }

        if (Pending is not null)
        {
            return UpdateResult.Ready;
        }

        IsBusy = true;
        try
        {
            _settings.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
            _settings.Save();

            var update = await UpdateChecker
                .FetchAsync(Ui.AppInfo.Version, _kind, cancellationToken)
                .ConfigureAwait(true);

            if (update is null)
            {
                return UpdateResult.UpToDate;
            }

            var file = UpdateInstaller.PathFor(update);
            if (!await UpdateChecker.DownloadAsync(update, file, cancellationToken).ConfigureAwait(true))
            {
                return UpdateResult.Failed;
            }

            Pending = update;
            return UpdateResult.Ready;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Installs the downloaded update. True means the app is about to be replaced and must exit.</summary>
    public bool Apply()
    {
        if (Pending is not { } update)
        {
            return false;
        }

        return UpdateInstaller.Apply(UpdateInstaller.PathFor(update), _kind);
    }

    /// <summary>Removes the previous build and any half finished downloads.</summary>
    public static void CleanUp() => UpdateInstaller.CleanUp();

    /// <summary>Retries deleting the replaced build, which can still be locked at startup.</summary>
    public static void RemovePreviousBuild() => UpdateInstaller.RemovePreviousBuild();

    public static string Describe(UpdateResult result, UpdateInfo? update) => result switch
    {
        UpdateResult.Ready when update is not null => $"Version {update.Version} is ready to install.",
        UpdateResult.Ready => "An update is ready to install.",
        UpdateResult.UpToDate => "You are on the latest release.",
        UpdateResult.Failed => "The update could not be fetched. See the error log.",
        UpdateResult.Disabled => "Automatic updates are off.",
        _ => string.Empty
    };
}
