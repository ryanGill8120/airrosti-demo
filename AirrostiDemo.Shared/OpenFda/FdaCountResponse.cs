namespace AirrostiDemo.Shared.OpenFda
{
    public class FdaCountResponse
    {
        public string DrugName { get; set; } = string.Empty;

        public List<FdaReactionCount> Results { get; set; } = new();
    }
}
