using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Backup.Web.Api.Server.Services.SupplierQuotes
{
    public interface ISupplierQuoteSubscriptionStore
    {
        void Join(string connectionId, string companyId, int productId);
        void Leave(string connectionId, string companyId, int productId);
        void RemoveConnection(string connectionId);
        IReadOnlyList<(string CompanyId, int ProductId)> ActiveProducts();
    }

    public sealed class SupplierQuoteSubscriptionStore : ISupplierQuoteSubscriptionStore
    {
        private readonly ConcurrentDictionary<string, HashSet<(string CompanyId, int ProductId)>> byConnection = new();
        private readonly ConcurrentDictionary<(string CompanyId, int ProductId), int> refCounts = new();

        public void Join(string connectionId, string companyId, int productId)
        {
            var key = (companyId, productId);
            var set = byConnection.GetOrAdd(connectionId, _ => new HashSet<(string, int)>());
            lock (set)
            {
                if (!set.Add(key)) return;
            }
            refCounts.AddOrUpdate(key, 1, (_, n) => n + 1);
        }

        public void Leave(string connectionId, string companyId, int productId)
        {
            if (!byConnection.TryGetValue(connectionId, out var set)) return;
            var key = (companyId, productId);
            lock (set)
            {
                if (!set.Remove(key)) return;
            }
            Decrement(key);
        }

        public void RemoveConnection(string connectionId)
        {
            if (!byConnection.TryRemove(connectionId, out var set)) return;
            (string CompanyId, int ProductId)[] keys;
            lock (set)
            {
                keys = set.ToArray();
                set.Clear();
            }
            foreach (var key in keys)
                Decrement(key);
        }

        public IReadOnlyList<(string CompanyId, int ProductId)> ActiveProducts() =>
            refCounts.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();

        private void Decrement((string CompanyId, int ProductId) key)
        {
            refCounts.AddOrUpdate(key, 0, (_, n) => Math.Max(0, n - 1));
            if (refCounts.TryGetValue(key, out var n) && n <= 0)
                refCounts.TryRemove(key, out _);
        }
    }
}
