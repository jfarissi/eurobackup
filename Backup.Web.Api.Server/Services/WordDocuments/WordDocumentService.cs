using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Brokers.DateTimes;
using Backup.Web.Api.Server.Brokers.Loggings;
using Backup.Web.Api.Server.Models.WordDocuments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models;

namespace Backup.Web.Api.Server.Services.WordDocuments
{
    public partial class WordDocumentService : IWordDocumentService
    {
        private readonly IStorageBroker storageBroker;
        private readonly ILoggingBroker loggingBroker;
        private readonly IDateTimeBroker dateTimeBroker;

        public WordDocumentService(
            IStorageBroker storageBroker,
            ILoggingBroker loggingBroker,
            IDateTimeBroker dateTimeBroker)
        {
            this.storageBroker = storageBroker;
            this.loggingBroker = loggingBroker;
            this.dateTimeBroker = dateTimeBroker;
        }

        public ValueTask<WordDocument> RegisterWordDocumentAsync(WordDocument WordDocument) =>
        
        TryCatch(async () =>
        {
            ValidateWordDocumentOnCreate(WordDocument);

            return await this.storageBroker.InsertWordDocumentAsync(WordDocument);
        });

        public ValueTask<WordDocument> RetrieveWordDocumentByIdAsync(Guid WordDocumentId) =>
        TryCatch(async () =>
        {
            ValidateWordDocumentId(WordDocumentId);
            WordDocument storageWordDocument = await this.storageBroker.SelectWordDocumentByIdAsync(WordDocumentId);
            ValidateStorageWordDocument(storageWordDocument, WordDocumentId);

            return storageWordDocument;
        });

        public ValueTask<WordDocument> ModifyWordDocumentAsync(WordDocument WordDocument) =>
        TryCatch(async () =>
        {
            ValidateWordDocumentOnModify(WordDocument);

            WordDocument maybeWordDocument =
                await this.storageBroker.SelectWordDocumentByIdAsync(WordDocument.Id);

            ValidateStorageWordDocument(maybeWordDocument, WordDocument.Id);
            ValidateAginstStorageWordDocumentOnModify(inputWordDocument: WordDocument, storageWordDocument: maybeWordDocument);

            return await this.storageBroker.UpdateWordDocumentAsync(WordDocument);
        });

        public ValueTask<WordDocument> DeleteWordDocumentAsync(Guid WordDocumentId) =>
        TryCatch(async () =>
        {
            ValidateWordDocumentId(WordDocumentId);

            WordDocument maybeWordDocument =
                await this.storageBroker.SelectWordDocumentByIdAsync(WordDocumentId);

            ValidateStorageWordDocument(maybeWordDocument, WordDocumentId);

            return await this.storageBroker.DeleteWordDocumentAsync(maybeWordDocument);
        });

        public IQueryable<WordDocument> RetrieveAllWordDocuments() =>
        TryCatch(() =>
        {
            IQueryable<WordDocument> storageWordDocuments = this.storageBroker.SelectAllWordDocuments();
            ValidateStorageWordDocuments(storageWordDocuments);

            return storageWordDocuments;
        });

        public IQueryable<WordDocument> RetrieveAllWordDocuments(Pager movieQuery) =>
        TryCatch(() =>
        {
            IQueryable<WordDocument> storageWordDocuments = this.storageBroker.SelectAllWordDocuments(movieQuery);
            ValidateStorageWordDocuments(storageWordDocuments);

            return storageWordDocuments;
        });
    }
}
