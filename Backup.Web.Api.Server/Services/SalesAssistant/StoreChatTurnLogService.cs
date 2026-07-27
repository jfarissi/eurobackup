using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Services.StoreChat;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    public interface IStoreChatTurnLogService
    {
        Task<Guid?> LogTurnAsync(
            StoreChatMessageRequest request,
            StoreChatResponseDto response,
            StoreChatSession? session,
            CancellationToken ct = default);

        Task<bool> SetReviewAsync(Guid turnId, string status, string? note, CancellationToken ct = default, string? source = null);

        Task<IReadOnlyList<StoreChatTurnDto>> ListRecentAsync(
            int take = 50,
            string? reviewStatus = null,
            bool? isCorrected = null,
            CancellationToken ct = default);
    }

    public sealed class StoreChatTurnLogService : IStoreChatTurnLogService
    {
        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };
        private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "ok", "bad", "fixed"
        };

        private readonly IStorageBroker _storage;
        private readonly ILogger<StoreChatTurnLogService> _logger;

        public StoreChatTurnLogService(IStorageBroker storage, ILogger<StoreChatTurnLogService> logger)
        {
            _storage = storage;
            _logger = logger;
        }

        public async Task<Guid?> LogTurnAsync(
            StoreChatMessageRequest request,
            StoreChatResponseDto response,
            StoreChatSession? session,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(response.SessionId))
                return null;

            var userText = request.Text?.Trim();
            var reply = response.ReplyText?.Trim();
            if (string.IsNullOrWhiteSpace(userText) && string.IsNullOrWhiteSpace(reply)
                && string.IsNullOrWhiteSpace(request.ClientIntent))
                return null;

            try
            {
                var productsJson = response.Products is { Count: > 0 }
                    ? JsonSerializer.Serialize(
                        response.Products.Select(p => new
                        {
                            p.ProductId,
                            p.Name,
                            p.Price,
                            p.SuggestedQuantity
                        }),
                        JsonOpts)
                    : null;

                var domainId = response.ActiveProjectDomainId ?? session?.ActiveProjectDomainId;
                var turn = new StoreChatTurn
                {
                    SessionId = response.SessionId,
                    SalesProjectId = response.SalesProjectId ?? session?.ActiveSalesProjectId,
                    PreferredLanguage = session?.PreferredLanguage ?? request.Language,
                    DomainId = domainId,
                    ClientIntent = string.IsNullOrWhiteSpace(request.ClientIntent)
                        ? null
                        : request.ClientIntent.Trim(),
                    ActionType = response.ActionType,
                    UserText = userText,
                    ReplyText = reply,
                    ProductsJson = productsJson,
                    CreatedAt = DateTime.UtcNow
                };

                if (TryAutoFlagBad(turn, response, out var autoNote))
                {
                    turn.ReviewStatus = "bad";
                    turn.ReviewSource = "auto";
                    turn.ReviewNote = autoNote;
                }

                var saved = await _storage.InsertStoreChatTurnAsync(turn);
                return saved.Id;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "StoreChat turn log failed session={SessionId}", response.SessionId);
                return null;
            }
        }

        public async Task<bool> SetReviewAsync(
            Guid turnId,
            string status,
            string? note,
            CancellationToken ct = default,
            string? source = null)
        {
            if (turnId == Guid.Empty || string.IsNullOrWhiteSpace(status))
                return false;

            var normalized = status.Trim().ToLowerInvariant();
            if (!AllowedStatuses.Contains(normalized))
                return false;

            try
            {
                var turn = await _storage.SelectAllStoreChatTurns()
                    .FirstOrDefaultAsync(t => t.Id == turnId, ct);
                if (turn == null)
                    return false;

                turn.ReviewStatus = normalized;
                if (!string.IsNullOrWhiteSpace(note))
                    turn.ReviewNote = note.Trim();

                turn.ReviewSource = string.IsNullOrWhiteSpace(source)
                    ? "manual"
                    : source.Trim().ToLowerInvariant();

                if (normalized == "fixed")
                {
                    turn.IsCorrected = true;
                    turn.CorrectedAt = DateTime.UtcNow;
                }
                else if (normalized == "bad" || normalized == "ok")
                {
                    turn.IsCorrected = false;
                    turn.CorrectedAt = null;
                }

                await _storage.UpdateStoreChatTurnAsync(turn);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "StoreChat turn review failed id={TurnId}", turnId);
                return false;
            }
        }

        public async Task<IReadOnlyList<StoreChatTurnDto>> ListRecentAsync(
            int take = 50,
            string? reviewStatus = null,
            bool? isCorrected = null,
            CancellationToken ct = default)
        {
            take = Math.Clamp(take, 1, 200);
            try
            {
                var q = _storage.SelectAllStoreChatTurns().AsNoTracking();
                if (!string.IsNullOrWhiteSpace(reviewStatus))
                {
                    var status = reviewStatus.Trim().ToLowerInvariant();
                    q = q.Where(t => t.ReviewStatus == status);
                }

                if (isCorrected is bool corrected)
                    q = q.Where(t => t.IsCorrected == corrected);

                return await q
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(take)
                    .Select(t => new StoreChatTurnDto
                    {
                        Id = t.Id,
                        SessionId = t.SessionId,
                        SalesProjectId = t.SalesProjectId,
                        PreferredLanguage = t.PreferredLanguage,
                        DomainId = t.DomainId,
                        ClientIntent = t.ClientIntent,
                        ActionType = t.ActionType,
                        UserText = t.UserText,
                        ReplyText = t.ReplyText,
                        ProductsJson = t.ProductsJson,
                        ReviewStatus = t.ReviewStatus,
                        ReviewNote = t.ReviewNote,
                        ReviewSource = t.ReviewSource,
                        IsCorrected = t.IsCorrected,
                        CorrectedAt = t.CorrectedAt,
                        CreatedAt = t.CreatedAt
                    })
                    .ToListAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "StoreChat turn list failed");
                return Array.Empty<StoreChatTurnDto>();
            }
        }

        private static bool TryAutoFlagBad(StoreChatTurn turn, StoreChatResponseDto response, out string note)
        {
            note = "";
            var reply = turn.ReplyText ?? "";
            var domain = (turn.DomainId ?? "").ToLowerInvariant();
            var intent = (turn.ClientIntent ?? "").ToLowerInvariant();
            var action = (turn.ActionType ?? "").ToLowerInvariant();

            if (action is "cart_updated" or "workflow_denied")
                return false;

            if (reply.Contains("lang_mismatch", StringComparison.OrdinalIgnoreCase)
                || reply.Contains("interfacetaal", StringComparison.OrdinalIgnoreCase)
                || reply.Contains("langue de l’interface", StringComparison.OrdinalIgnoreCase)
                || reply.Contains("langue de l'interface", StringComparison.OrdinalIgnoreCase))
            {
                note = "auto: language mismatch warning";
                return true;
            }

            var gardenHints = new[]
            {
                "boordsteen", "omheining", "grind…", "grind...", "bordure", "clôture", "cloture", "gravier…"
            };
            var nonGarden = domain is "roofing" or "painting" or "tiling" or "wall_construction"
                or "electrical" or "plumbing";
            if (nonGarden && gardenHints.Any(h => reply.Contains(h, StringComparison.OrdinalIgnoreCase)))
            {
                note = "auto: garden hint in non-garden domain";
                return true;
            }

            var productCount = response.Products?.Count ?? 0;
            if (productCount == 0
                && !string.IsNullOrWhiteSpace(domain)
                && action is "product_list" or "none"
                && intent is "moreproducts" or "projectnextstep" or "wallnextstep")
            {
                note = "auto: empty product list on next/more in domain";
                return true;
            }

            return false;
        }
    }
}
