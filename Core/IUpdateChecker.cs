namespace Whiteboard.Core;

/// <summary>新しい版が公開されているかを確認する。</summary>
public interface IUpdateChecker
{
    /// <summary>
    /// 今の版より新しい版があれば返す。無い場合や、オフラインなどで確認できなかった場合は null（例外は投げない）。
    /// </summary>
    Task<AvailableUpdate?> CheckAsync(CancellationToken cancellationToken = default);
}

/// <param name="Version">新しい版のバージョン。</param>
/// <param name="ReleasePage">ダウンロードできるページ（GitHub のリリースのページ）。</param>
public sealed record AvailableUpdate(Version Version, Uri ReleasePage)
{
    public string DisplayVersion => ReleaseVersion.ToDisplayString(Version);
}
