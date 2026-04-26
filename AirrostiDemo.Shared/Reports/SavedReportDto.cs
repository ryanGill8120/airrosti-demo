using AirrostiDemo.Shared.OpenFda;

namespace AirrostiDemo.Shared.Reports
{
    public class SavedReportDto
    {
        public int Id { get; set; }

        public string DrugName { get; set; } = string.Empty;

        public DateTime SavedAt { get; set; }

        public FdaCountResponse Report { get; set; } = new();
    }
}
