

using Backup.Web.Api.Server.Models.WordDocumentContents;
using Backup.Web.Api.Server.Models.WordDocumentContents.Exceptions;
using System;
using System.Linq;

namespace Backup.Web.Api.Server.Services.WordDocumentContents
{
    public partial class WordDocumentContentsService
    {
        private void ValidateWordDocumentContentId(Guid WordDocumentContentId)
        {
            if (WordDocumentContentId == Guid.Empty)
            {
                throw new InvalidWordDocumentContentsInputException(
                    parameterName: nameof(WordDocumentContentId),
                    parameterValue: WordDocumentContentId);
            }
        }

        private static void ValidateStorageWordDocumentContent(WordDocumentContent storageWordDocumentContent, Guid WordDocumentContentId)
        {
            if (storageWordDocumentContent == null)
            {
                throw new NotFoundWordDocumentContentsException(WordDocumentContentId);
            }
        }

        private void ValidateWordDocumentContentOnCreate(WordDocumentContent WordDocumentContent)
        {
            ValidateWordDocumentContent(WordDocumentContent);
            ValidateWordDocumentContentId(WordDocumentContent.Id);
            ValidateWordDocumentContentStrings(WordDocumentContent);
        }

        private void ValidateWordDocumentContentOnModify(WordDocumentContent WordDocumentContent)
        {
            ValidateWordDocumentContent(WordDocumentContent);
            ValidateWordDocumentContentId(WordDocumentContent.Id);
            ValidateWordDocumentContentStrings(WordDocumentContent);
        }


        private void ValidateWordDocumentContentStrings(WordDocumentContent WordDocumentContent)
        {
            switch (WordDocumentContent)
            {


                case { } when IsInvalid(WordDocumentContent.Content):
                    throw new InvalidWordDocumentContentsInputException(
                        parameterName: nameof(WordDocumentContent.Content),
                        parameterValue: WordDocumentContent.Content);


            }
        }
        private bool IsDateNotRecent(DateTimeOffset dateTime)
        {
            DateTimeOffset now = this.dateTimeBroker.GetCurrentDateTime();
            int oneMinute = 1;
            TimeSpan difference = now.Subtract(dateTime);

            return Math.Abs(difference.TotalMinutes) > oneMinute;
        }

        private void ValidateWordDocumentContent(WordDocumentContent WordDocumentContent)
        {
            if (WordDocumentContent is null)
            {
                throw new NullWordDocumentContentsException();
            }
        }

        private void ValidateStorageWordDocumentContents(IQueryable<WordDocumentContent> storageWordDocumentContents)
        {
            if (storageWordDocumentContents.Count() == 0)
            {
                this.loggingBroker.LogWarning("No WordDocumentContents found in storage.");
            }
        }

        private static bool IsInvalid(string input) => String.IsNullOrWhiteSpace(input);
        private static bool IsInvalid(Guid input) => input == default;
    }
}
