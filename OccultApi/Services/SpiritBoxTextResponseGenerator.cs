using Microsoft.Agents.AI;

namespace OccultApi.Services
{
    public class SpiritBoxTextResponseGenerator : ISpiritBoxTextResponseGenerator
    {
        private readonly AIAgent _agent;

        public SpiritBoxTextResponseGenerator(AIAgent agent)
        {
            _agent = agent;
        }

        public async Task<string> RespondAsync(string prompt, CancellationToken cancellationToken = default)
        {
            var session = await _agent.CreateSessionAsync(cancellationToken);
            var response = await _agent.RunAsync(prompt, session, cancellationToken: cancellationToken);

            return response.Text;
        }
    }
}
