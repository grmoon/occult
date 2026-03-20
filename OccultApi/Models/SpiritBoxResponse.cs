namespace OccultApi.Models
{
    public record SpiritBoxResponse
    {
        public required string? Response { get; init; }
        public required string Audio { get; init; }
    }
}
