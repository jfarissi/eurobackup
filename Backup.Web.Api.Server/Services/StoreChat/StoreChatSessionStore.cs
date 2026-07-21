using System.Collections.Concurrent;

namespace Backup.Web.Api.Server.Services.StoreChat
{
    public interface IStoreChatSessionStore
    {
        StoreChatSession GetOrCreate(string? sessionId);
        StoreChatSession? Get(string sessionId);
        void Save(StoreChatSession session);
        void Reset(string sessionId);
    }

    public class InMemoryStoreChatSessionStore : IStoreChatSessionStore
    {
        private readonly ConcurrentDictionary<string, StoreChatSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

        public StoreChatSession GetOrCreate(string? sessionId)
        {
            if (!string.IsNullOrWhiteSpace(sessionId) && _sessions.TryGetValue(sessionId.Trim(), out var existing))
            {
                existing.UpdatedAt = DateTime.UtcNow;
                return existing;
            }

            var session = new StoreChatSession();
            _sessions[session.SessionId] = session;
            return session;
        }

        public StoreChatSession? Get(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return null;
            return _sessions.TryGetValue(sessionId.Trim(), out var session) ? session : null;
        }

        public void Save(StoreChatSession session)
        {
            session.UpdatedAt = DateTime.UtcNow;
            _sessions[session.SessionId] = session;
        }

        public void Reset(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return;

            var fresh = new StoreChatSession { SessionId = sessionId.Trim() };
            _sessions[fresh.SessionId] = fresh;
        }
    }
}
