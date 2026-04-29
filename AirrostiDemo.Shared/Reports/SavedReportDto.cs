using AirrostiDemo.Shared.OpenFda;

namespace AirrostiDemo.Shared.Reports
{
    /// <summary>
    /// A single saved drug-search snapshot belonging to the currently
    /// authenticated user. This is the projection the Reports controller
    /// returns from <c>GET /api/Reports</c> and the shape the My Reports
    /// page renders.
    /// </summary>
    /// <remarks>
    /// On disk, the FDA payload is stored as a single JSON blob in
    /// <c>SavedReport.ReportJson</c>; this DTO is the deserialized,
    /// strongly-typed view that the UI works with. We deliberately do not
    /// expose the owning <c>UserId</c> — a row's user is implicit (it's the
    /// caller) and shouldn't be round-tripped to the browser.
    /// </remarks>
    public class SavedReportDto
    {
        /// <summary>
        /// The auto-incremented primary key from the SavedReports table.
        /// Useful as a stable identity for future delete / share operations.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The drug name the user originally searched. Hoisted out of the
        /// JSON blob into its own column so it can be indexed / displayed
        /// without deserializing the whole report.
        /// </summary>
        public string DrugName { get; set; } = string.Empty;

        /// <summary>
        /// The UTC timestamp at which the user clicked "Save my results".
        /// Note this is a <see cref="DateTime"/> rather than a
        /// <see cref="DateTimeOffset"/> on purpose: SQLite cannot
        /// <c>ORDER BY</c> a <see cref="DateTimeOffset"/>, so the database
        /// stores naive UTC and the controller re-attaches
        /// <c>DateTimeKind.Utc</c> at projection time.
        /// </summary>
        public DateTime SavedAt { get; set; }

        /// <summary>
        /// The full reaction-count payload that was originally returned by
        /// the FDA proxy when the user ran the search. Re-hydrated from JSON
        /// on every read.
        /// </summary>
        public FdaCountResponse Report { get; set; } = new();
    }
}
