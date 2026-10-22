namespace DragonBoyAccountManager.Core.Telemetry.Scrapers;

public interface ISnapshotScraper
{
    Type SnapshotType { get; }
    object Scrape();
}

public interface ISnapshotScraper<out TSnapshot> : ISnapshotScraper where TSnapshot : class
{
    Type ISnapshotScraper.SnapshotType => typeof(TSnapshot);
    object ISnapshotScraper.Scrape() => Scrape();
    new TSnapshot Scrape();
}
