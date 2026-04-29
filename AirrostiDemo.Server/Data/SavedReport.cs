namespace AirrostiDemo.Server.Data
{
    /// <summary>
    /// EF Core entity that backs the <c>SavedReports</c> table. Each row is
    /// one drug-search snapshot owned by one user.
    /// </summary>
    /// <remarks>
    /// This entity is intentionally simple: rather than normalising the FDA
    /// payload into a child table of reactions, we store the whole response
    /// as a single JSON blob in <see cref="ReportJson"/>. That keeps the
    /// schema flat, makes "view this saved report" a single round-trip, and
    /// matches how the data is consumed (always read whole, never partially).
    /// The trade-off is that we can't query into reactions in SQL — fine for
    /// this demo's use case.
    /// </remarks>
    public class SavedReport
    {
        /// <summary>
        /// Auto-incremented primary key, populated by EF / SQLite on insert.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Foreign key into the AspNet Identity users table — specifically,
        /// the value of <c>AppUser.Id</c> for the user who saved this report.
        /// Indexed in <c>AppDbContext.OnModelCreating</c> so that the
        /// "show me my reports" query stays cheap as the table grows.
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// The drug name the user searched. Hoisted out of the JSON blob into
        /// its own column with a 200-char cap so it can be displayed in lists
        /// without deserialising the full payload.
        /// </summary>
        public string DrugName { get; set; } = string.Empty;

        /// <summary>
        /// The full <see cref="AirrostiDemo.Shared.OpenFda.FdaCountResponse"/>
        /// payload, serialized to JSON. Stored verbatim so we have a true
        /// snapshot of what FDA returned at save time — if FDA later updates
        /// or removes a report the user's saved view is unaffected.
        /// </summary>
        public string ReportJson { get; set; } = string.Empty;

        /// <summary>
        /// UTC timestamp captured at save time. Deliberately a
        /// <see cref="DateTime"/> rather than a <see cref="DateTimeOffset"/>:
        /// SQLite can't <c>ORDER BY</c> a DateTimeOffset, and the My Reports
        /// page sorts by this column.
        /// </summary>
        public DateTime SavedAt { get; set; }
    }
}
