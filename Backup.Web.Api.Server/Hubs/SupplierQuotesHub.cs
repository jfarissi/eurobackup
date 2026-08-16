using System;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Services.SupplierQuotes;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Backup.Web.Api.Server.Hubs;

[Authorize]
public class SupplierQuotesHub : Hub
{
    public const string HubPath = "/hubs/supplier-quotes";
    public const string QuotesUpdatedEvent = "quotesUpdated";

    private readonly ICompanyContextService companyContext;
    private readonly ISupplierQuoteSubscriptionStore subscriptions;

    public SupplierQuotesHub(
        ICompanyContextService companyContext,
        ISupplierQuoteSubscriptionStore subscriptions)
    {
        this.companyContext = companyContext;
        this.subscriptions = subscriptions;
    }

    public static string ProductGroup(string companyId, int productId) =>
        $"company:{companyId}:product:{productId}";

    public async Task JoinProduct(int productId)
    {
        var companyId = this.companyContext.GetCurrentCompanyId()
            ?? throw new HubException("Société courante introuvable.");
        if (productId <= 0) throw new HubException("productId invalide.");

        await Groups.AddToGroupAsync(Context.ConnectionId, ProductGroup(companyId, productId));
        this.subscriptions.Join(Context.ConnectionId, companyId, productId);
    }

    public async Task LeaveProduct(int productId)
    {
        var companyId = this.companyContext.GetCurrentCompanyId();
        if (string.IsNullOrWhiteSpace(companyId) || productId <= 0) return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ProductGroup(companyId, productId));
        this.subscriptions.Leave(Context.ConnectionId, companyId, productId);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        this.subscriptions.RemoveConnection(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
