namespace AirrostiDemo.Shared.OpenFda
{
    /// <summary>
    /// The full payload returned by <c>GET /api/DrugSideEffects/{drugName}</c>:
    /// the searched drug name plus the ranked list of reaction counts the
    /// server distilled from OpenFDA's response envelope.
    /// </summary>
    /// <remarks>
    /// This is the shape that round-trips end-to-end through the system. The
    /// Blazor client deserializes it for display, and the same instance is
    /// later POSTed back to <c>/api/Reports</c> when the user clicks
    /// "Save my results" — the server then JSON-serializes it into the
    /// SavedReports table for later retrieval.
    /// </remarks>
    public class FdaCountResponse
    {
        /// <summary>
        /// Echo of the drug name that was searched. We keep it on the response
        /// (rather than relying on the URL) so that any consumer holding the
        /// DTO — including a saved report deserialized years later — knows
        /// exactly which drug the result set belongs to.
        /// </summary>
        public string DrugName { get; set; } = string.Empty;

        /// <summary>
        /// The ranked list of reaction terms and their report counts.
        /// Initialized to an empty list so that callers can iterate
        /// unconditionally without null checks; an empty list also represents
        /// the legitimate "drug exists but has no reports" case.
        /// </summary>
        public List<FdaReactionCount> Results { get; set; } = new();
    }
}
