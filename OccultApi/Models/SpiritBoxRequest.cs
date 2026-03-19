namespace OccultApi.Models
{
    public record SpiritBoxRequest
    {
        public required string Prompt { get; init; }
    }
}
