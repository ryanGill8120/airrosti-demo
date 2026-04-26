namespace AirrostiDemo.Server.Data
{
    public class SavedReport
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string DrugName { get; set; } = string.Empty;

        public string ReportJson { get; set; } = string.Empty;

        public DateTime SavedAt { get; set; }
    }
}
