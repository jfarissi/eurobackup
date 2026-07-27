using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial interface IStorageBroker
    {
        IQueryable<StoreChatQuote> SelectAllStoreChatQuotes();
        ValueTask<StoreChatQuote> InsertStoreChatQuoteAsync(StoreChatQuote quote);

        IQueryable<StoreChatOrder> SelectAllStoreChatOrders();
        ValueTask<StoreChatOrder> InsertStoreChatOrderAsync(StoreChatOrder order);
        ValueTask<StoreChatOrder> UpdateStoreChatOrderAsync(StoreChatOrder order);

        IQueryable<StoreChatTurn> SelectAllStoreChatTurns();
        ValueTask<StoreChatTurn> InsertStoreChatTurnAsync(StoreChatTurn turn);
        ValueTask<StoreChatTurn> UpdateStoreChatTurnAsync(StoreChatTurn turn);
    }

    public partial class StorageBroker
    {
        public DbSet<StoreChatQuote> StoreChatQuotes { get; set; } = null!;
        public DbSet<StoreChatOrder> StoreChatOrders { get; set; } = null!;
        public DbSet<StoreChatTurn> StoreChatTurns { get; set; } = null!;

        public IQueryable<StoreChatQuote> SelectAllStoreChatQuotes() => this.StoreChatQuotes.AsQueryable();

        public async ValueTask<StoreChatQuote> InsertStoreChatQuoteAsync(StoreChatQuote quote)
        {
            var entry = await this.StoreChatQuotes.AddAsync(quote);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<StoreChatOrder> SelectAllStoreChatOrders() => this.StoreChatOrders.AsQueryable();

        public async ValueTask<StoreChatOrder> InsertStoreChatOrderAsync(StoreChatOrder order)
        {
            var entry = await this.StoreChatOrders.AddAsync(order);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<StoreChatOrder> UpdateStoreChatOrderAsync(StoreChatOrder order)
        {
            var entry = this.StoreChatOrders.Update(order);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<StoreChatTurn> SelectAllStoreChatTurns() => this.StoreChatTurns.AsQueryable();

        public async ValueTask<StoreChatTurn> InsertStoreChatTurnAsync(StoreChatTurn turn)
        {
            var entry = await this.StoreChatTurns.AddAsync(turn);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<StoreChatTurn> UpdateStoreChatTurnAsync(StoreChatTurn turn)
        {
            var entry = this.StoreChatTurns.Update(turn);
            await this.SaveChangesAsync();
            return entry.Entity;
        }
    }
}
