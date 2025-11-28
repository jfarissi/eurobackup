

using Backup.Web.Api.Server.Models.WordDocuments;
using Backup.Web.Api.Server.Models.WordDocuments.Exceptions;
using System;
using System.IO;
using System.Linq;

namespace Backup.Web.Api.Server.Services.WordDocuments
{
    public partial class WordDocumentService
    {
        private void ValidateWordDocumentId(Guid WordDocumentId)
        {
            if (WordDocumentId == Guid.Empty)
            {
                throw new InvalidWordDocumentInputException(
                    parameterName: nameof(WordDocumentId),
                    parameterValue: WordDocumentId);
            }
        }

        private static void ValidateStorageWordDocument(WordDocument storageWordDocument, Guid WordDocumentId)
        {
            if (storageWordDocument == null)
            {
                throw new NotFoundWordDocumentException(WordDocumentId);
            }
        }

        private void ValidateWordDocumentOnCreate(WordDocument WordDocument)
        {
            ValidateWordDocument(WordDocument);
            ValidateWordDocumentId(WordDocument.Id);
            ValidateWordDocumentIds(WordDocument);
            ValidateWordDocumentStrings(WordDocument);
            ValidateWordDocumentDates(WordDocument);
            ValidateCreatedSignature(WordDocument);
            ValidateCreatedDateIsRecent(WordDocument);
            //validateFileExiste(WordDocument);
        }

        private void ValidateWordDocumentOnModify(WordDocument WordDocument)
        {
            ValidateWordDocument(WordDocument);
            ValidateWordDocumentId(WordDocument.Id);
            ValidateWordDocumentStrings(WordDocument);
            ValidateWordDocumentDates(WordDocument);
            ValidateWordDocumentIds(WordDocument);
            ValidateDatesAreNotSame(WordDocument);
            ValidateUpdatedDateIsRecent(WordDocument);
            //validateFileExiste(WordDocument);
        }

        public void ValidateAginstStorageWordDocumentOnModify(WordDocument inputWordDocument, WordDocument storageWordDocument)
        {
            switch (inputWordDocument)
            {
                case { } when inputWordDocument.CreatedDate != storageWordDocument.CreatedDate:
                    throw new InvalidWordDocumentInputException(
                        parameterName: nameof(WordDocument.CreatedDate),
                        parameterValue: inputWordDocument.CreatedDate);

                case { } when inputWordDocument.CreatedBy != storageWordDocument.CreatedBy:
                    throw new InvalidWordDocumentInputException(
                        parameterName: nameof(WordDocument.CreatedBy),
                        parameterValue: inputWordDocument.CreatedBy);

                case { } when inputWordDocument.UpdatedDate == storageWordDocument.UpdatedDate:
                    throw new InvalidWordDocumentInputException(
                        parameterName: nameof(WordDocument.UpdatedDate),
                        parameterValue: inputWordDocument.UpdatedDate);
            }
        }

        private void ValidateWordDocumentStrings(WordDocument WordDocument)
        {
            switch (WordDocument)
            {
                case { } when IsInvalid(WordDocument.UserId):
                    throw new InvalidWordDocumentInputException(
                        parameterName: nameof(WordDocument.UserId),
                        parameterValue: WordDocument.UserId);


                case { } when IsInvalid(WordDocument.Name):
                    throw new InvalidWordDocumentInputException(
                        parameterName: nameof(WordDocument.Name),
                        parameterValue: WordDocument.Name);
            }
        }

        private void ValidateDatesAreNotSame(WordDocument WordDocument)
        {
            if (WordDocument.CreatedDate == WordDocument.UpdatedDate)
            {
                throw new InvalidWordDocumentInputException(
                    parameterName: nameof(WordDocument.CreatedDate),
                    parameterValue: WordDocument.CreatedDate);
            }
        }

        private void ValidateCreatedDateIsRecent(WordDocument WordDocument)
        {
            if (IsDateNotRecent(WordDocument.CreatedDate))
            {
                throw new InvalidWordDocumentInputException(
                    parameterName: nameof(WordDocument.CreatedDate),
                    parameterValue: WordDocument.CreatedDate);
            }
        }

        private void ValidateUpdatedDateIsRecent(WordDocument WordDocument)
        {
            if (IsDateNotRecent(WordDocument.UpdatedDate))
            {
                throw new InvalidWordDocumentInputException(
                    parameterName: nameof(WordDocument.UpdatedDate),
                    parameterValue: WordDocument.UpdatedDate);
            }
        }

        private bool IsDateNotRecent(DateTimeOffset dateTime)
        {
            DateTimeOffset now = this.dateTimeBroker.GetCurrentDateTime();
            int oneMinute = 1;
            TimeSpan difference = now.Subtract(dateTime);

            return Math.Abs(difference.TotalMinutes) > oneMinute;
        }

        private void ValidateCreatedSignature(WordDocument WordDocument)
        {
            if (WordDocument.CreatedBy != WordDocument.UpdatedBy)
            {
                throw new InvalidWordDocumentInputException(
                    parameterName: nameof(WordDocument.UpdatedBy),
                    parameterValue: WordDocument.UpdatedBy);
            }
            else if (WordDocument.CreatedDate != WordDocument.UpdatedDate)
            {
                throw new InvalidWordDocumentInputException(
                    parameterName: nameof(WordDocument.UpdatedDate),
                    parameterValue: WordDocument.UpdatedDate);
            }
        }

        private void ValidateWordDocumentDates(WordDocument WordDocument)
        {
            switch (WordDocument)
            {

                case { } when WordDocument.CreatedDate == default:
                    throw new InvalidWordDocumentInputException(
                        parameterName: nameof(WordDocument.CreatedDate),
                        parameterValue: WordDocument.CreatedDate);

                case { } when WordDocument.UpdatedDate == default:
                    throw new InvalidWordDocumentInputException(
                        parameterName: nameof(WordDocument.UpdatedDate),
                        parameterValue: WordDocument.UpdatedDate);
            }
        }

        private void ValidateWordDocumentIds(WordDocument WordDocument)
        {
            switch (WordDocument)
            {
                case { } when IsInvalid(WordDocument.CreatedBy):
                    throw new InvalidWordDocumentInputException(
                        parameterName: nameof(WordDocument.CreatedBy),
                        parameterValue: WordDocument.CreatedBy);

                case { } when IsInvalid(WordDocument.UpdatedBy):
                    throw new InvalidWordDocumentInputException(
                        parameterName: nameof(WordDocument.UpdatedBy),
                        parameterValue: WordDocument.UpdatedBy);
            }
        }

        private void ValidateWordDocument(WordDocument WordDocument)
        {
            if (WordDocument is null)
            {
                throw new NullWordDocumentException();
            }
        }

        private void ValidateStorageWordDocuments(IQueryable<WordDocument> storageWordDocuments)
        {
            if (storageWordDocuments.Count() == 0)
            {
                this.loggingBroker.LogWarning("No WordDocuments found in storage.");
            }
        }

        private void validateFileExiste(WordDocument WordDocument)
        {
            if (File.Exists(WordDocument.Url))
            {
                throw new InvalidWordDocumentInputException(
                    parameterName: nameof(WordDocument.Name),
                    parameterValue: Path.GetFileName(WordDocument.Url));
            }

        }

        private static bool IsInvalid(string input) => String.IsNullOrWhiteSpace(input);
        private static bool IsInvalid(Guid input) => input == default;
    }
}
