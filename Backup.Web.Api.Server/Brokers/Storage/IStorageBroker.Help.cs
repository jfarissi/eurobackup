using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Entities;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial interface IStorageBroker
    {
        IQueryable<HelpContent> SelectAllHelpContents();
        ValueTask<HelpContent?> SelectHelpContentByIdAsync(int id);
        ValueTask<HelpContent> InsertHelpContentAsync(HelpContent entity);
        ValueTask<HelpContent> UpdateHelpContentAsync(HelpContent entity);
        ValueTask DeleteHelpContentAsync(HelpContent entity);

        IQueryable<HelpFeedbackEvent> SelectAllHelpFeedbackEvents();
        ValueTask<HelpFeedbackEvent> InsertHelpFeedbackEventAsync(HelpFeedbackEvent entity);

        IQueryable<HelpAnalyticsEvent> SelectAllHelpAnalyticsEvents();
        ValueTask<HelpAnalyticsEvent> InsertHelpAnalyticsEventAsync(HelpAnalyticsEvent entity);
    }
}
