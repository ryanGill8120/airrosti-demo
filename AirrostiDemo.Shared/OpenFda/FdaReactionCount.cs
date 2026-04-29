namespace AirrostiDemo.Shared.OpenFda
{
    /// <summary>
    /// Represents a single adverse-reaction term and the number of times it
    /// appeared in OpenFDA reports for a given drug. This DTO is one row in the
    /// table the user sees on the Drug Search page — for example, the term
    /// "NAUSEA" with a count of 12,431 reports.
    /// </summary>
    /// <remarks>
    /// This type lives in the Shared class library because both the Web API
    /// (which builds it from the upstream OpenFDA "count" envelope) and the
    /// Blazor WebAssembly client (which renders it) need to agree on the wire
    /// format. Keeping the DTO in one place avoids the classic dual-model
    /// drift bug where client and server fall out of sync.
    /// </remarks>
    public class FdaReactionCount
    {
        /// <summary>
        /// The MedDRA preferred term for the reaction (e.g. "NAUSEA",
        /// "DIZZINESS"). OpenFDA returns these in upper case; we surface them
        /// to the UI verbatim.
        /// </summary>
        public string Term { get; set; } = string.Empty;

        /// <summary>
        /// The number of distinct adverse-event reports in OpenFDA that listed
        /// this reaction term for the searched drug. Used both as a sortable
        /// numeric value and as the basis for the relative-frequency bar in
        /// the UI.
        /// </summary>
        public int Count { get; set; }
    }
}
