using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Brokers.DateTimes;
using Backup.Web.Api.Server.Brokers.Loggings;
using Backup.Web.Api.Server.Models.WordDocumentContents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace Backup.Web.Api.Server.Services.WordDocumentContents
{
    public partial class WordDocumentContentsService : IWordDocumentContentsService
    {
        private readonly IStorageBroker storageBroker;
        private readonly ILoggingBroker loggingBroker;
        private readonly IDateTimeBroker dateTimeBroker;

        public WordDocumentContentsService(
            IStorageBroker storageBroker,
            ILoggingBroker loggingBroker,
            IDateTimeBroker dateTimeBroker)
        {
            this.storageBroker = storageBroker;
            this.loggingBroker = loggingBroker;
            this.dateTimeBroker = dateTimeBroker;
        }

        public ValueTask<WordDocumentContent> RegisterWordDocumentContentAsync(WordDocumentContent WordDocumentContent) =>
        
        TryCatch(async () =>
        {
            ValidateWordDocumentContentOnCreate(WordDocumentContent);

            return await this.storageBroker.InsertWordDocumentContentsAsync(WordDocumentContent);
        });

        public ValueTask<WordDocumentContent> RetrieveWordDocumentContentByIdAsync(Guid WordDocumentContentId) =>
        TryCatch(async () =>
        {
            ValidateWordDocumentContentId(WordDocumentContentId);
            WordDocumentContent storageWordDocumentContent = await this.storageBroker.SelectWordDocumentContentsByIdAsync(WordDocumentContentId);
            ValidateStorageWordDocumentContent(storageWordDocumentContent, WordDocumentContentId);

            return storageWordDocumentContent;
        });

        public ValueTask<WordDocumentContent> ModifyWordDocumentContentAsync(WordDocumentContent WordDocumentContent) =>
        TryCatch(async () =>
        {
            ValidateWordDocumentContentOnModify(WordDocumentContent);

            WordDocumentContent maybeWordDocumentContent =
                await this.storageBroker.SelectWordDocumentContentsByIdAsync(WordDocumentContent.Id);

            ValidateStorageWordDocumentContent(maybeWordDocumentContent, WordDocumentContent.Id);
            //ValidateAginstStorageWordDocumentContentsOnModify(inputWordDocumentContent: WordDocumentContent, storageWordDocumentContent: maybeWordDocumentContent);

            return await this.storageBroker.UpdateWordDocumentContentsAsync(WordDocumentContent);
        });

        public ValueTask<WordDocumentContent> DeleteWordDocumentContentAsync(Guid WordDocumentContentId) =>
        TryCatch(async () =>
        {
            ValidateWordDocumentContentId(WordDocumentContentId);

            WordDocumentContent maybeWordDocumentContent =
                await this.storageBroker.SelectWordDocumentContentsByIdAsync(WordDocumentContentId);

            ValidateStorageWordDocumentContent(maybeWordDocumentContent, WordDocumentContentId);

            return await this.storageBroker.DeleteWordDocumentContentsAsync(maybeWordDocumentContent);
        });

        public IQueryable<WordDocumentContent> RetrieveAllWordDocumentContents() =>
        TryCatch(() =>
        {
            IQueryable<WordDocumentContent> storageWordDocumentContents = this.storageBroker.SelectAllWordDocumentContents();
            ValidateStorageWordDocumentContents(storageWordDocumentContents);

            return storageWordDocumentContents;
        });
    }
}
