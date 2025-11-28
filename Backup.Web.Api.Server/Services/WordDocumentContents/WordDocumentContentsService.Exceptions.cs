

using EFxceptions.Models.Exceptions;
using Backup.Web.Api.Server.Models.WordDocumentContents;
using Backup.Web.Api.Server.Models.WordDocumentContents.Exceptions;
//using Microsoft.Data.SqlClient;
//using MySql.Data.MySqlClient;   
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Backup.Web.Api.Server.Services.WordDocumentContents
{
    public partial class WordDocumentContentsService
    {
        private delegate ValueTask<WordDocumentContent> ReturningWordDocumentContentFunction();
        private delegate IQueryable<WordDocumentContent> ReturningQueryableWordDocumentContentFunction();

        private async ValueTask<WordDocumentContent> TryCatch(ReturningWordDocumentContentFunction returningWordDocumentContentFunction)
        {
            try
            {
                return await returningWordDocumentContentFunction();
            }
            catch (NullWordDocumentContentsException nullWordDocumentContentException)
            {
                throw CreateAndLogValidationException(nullWordDocumentContentException);
            }
            catch (InvalidWordDocumentContentsInputException invalidWordDocumentContentInputException)
            {
                throw CreateAndLogValidationException(invalidWordDocumentContentInputException);
            }
            catch (NotFoundWordDocumentContentsException nullWordDocumentContentException)
            {
                throw CreateAndLogValidationException(nullWordDocumentContentException);
            }
            catch (SqlException sqlException)
            {
                throw CreateAndLogCriticalDependencyException(sqlException);
            }
            catch (DuplicateKeyException duplicateKeyException)
            {
                var alreadyExistsWordDocumentContentException =
                    new AlreadyExistsWordDocumentContentsException(duplicateKeyException);

                throw CreateAndLogValidationException(alreadyExistsWordDocumentContentException);
            }
            catch (DbUpdateConcurrencyException dbUpdateConcurrencyException)
            {
                var lockedWordDocumentContentException = new LockedWordDocumentContentsException(dbUpdateConcurrencyException);

                throw CreateAndLogDependencyException(lockedWordDocumentContentException);
            }
            catch (DbUpdateException dbUpdateException)
            {
                throw CreateAndLogDependencyException(dbUpdateException);
            }
            catch (Exception exception)
            {
                throw CreateAndLogServiceException(exception);
            }
        }

        private IQueryable<WordDocumentContent> TryCatch(ReturningQueryableWordDocumentContentFunction returningQueryableWordDocumentContentFunction)
        {
            try
            {
                return returningQueryableWordDocumentContentFunction();
            }
            catch (SqlException sqlException)
            {
                throw CreateAndLogCriticalDependencyException(sqlException);
            }
            catch (DbUpdateException dbUpdateException)
            {
                throw CreateAndLogDependencyException(dbUpdateException);
            }
            catch (Exception exception)
            {
                throw CreateAndLogServiceException(exception);
            }
        }

        private WordDocumentContentsServiceException CreateAndLogServiceException(Exception exception)
        {
            var WordDocumentContentServiceException = new WordDocumentContentsServiceException(exception);
            this.loggingBroker.LogError(WordDocumentContentServiceException);

            return WordDocumentContentServiceException;
        }

        private WordDocumentContentsDependencyException CreateAndLogDependencyException(Exception exception)
        {
            var WordDocumentContentDependencyException = new WordDocumentContentsDependencyException(exception);
            this.loggingBroker.LogError(WordDocumentContentDependencyException);

            return WordDocumentContentDependencyException;
        }

        private WordDocumentContentsDependencyException CreateAndLogCriticalDependencyException(Exception exception)
        {
            var WordDocumentContentDependencyException = new WordDocumentContentsDependencyException(exception);
            this.loggingBroker.LogCritical(WordDocumentContentDependencyException);

            return WordDocumentContentDependencyException;
        }

        private WordDocumentContentsValidationException CreateAndLogValidationException(Exception exception)
        {
            var WordDocumentContentValidationException = new WordDocumentContentsValidationException(exception);
            this.loggingBroker.LogError(WordDocumentContentValidationException);

            return WordDocumentContentValidationException;
        }
    }
}
