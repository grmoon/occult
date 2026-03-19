namespace OccultApi.Models
{
    public record SpiritBoxRequest
    {
        public required string Prompt { get; init; }
        public required SpiritBoxResponseType ResponseType { get; init; }
    }
}
