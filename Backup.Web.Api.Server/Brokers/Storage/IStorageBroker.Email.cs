using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Entities.Email;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial interface IStorageBroker
    {
        IQueryable<CompanyEmailSettings> SelectAllCompanyEmailSettings();
        IQueryable<EmailMessage> SelectAllEmailMessages();
        ValueTask<CompanyEmailSettings?> SelectCompanyEmailSettingsByCompanyIdAsync(string companyId);
        ValueTask<CompanyEmailSettings> UpsertCompanyEmailSettingsAsync(CompanyEmailSettings settings);
        ValueTask<EmailMessage> InsertEmailMessageAsync(EmailMessage message);
        ValueTask<EmailMessage> UpdateEmailMessageAsync(EmailMessage message);
        ValueTask<EmailMessage?> SelectEmailMessageByIdAsync(long id);
        ValueTask<List<EmailMessage>> SelectPendingEmailMessagesAsync(int batchSize, DateTime nowUtc);
    }
}
