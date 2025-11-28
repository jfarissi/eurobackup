

using EFxceptions.Models.Exceptions;
using Backup.Web.Api.Server.Models.WordDocuments;
using Backup.Web.Api.Server.Models.WordDocuments.Exceptions;
//using Microsoft.Data.SqlClient;
//using MySql.Data.MySqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Backup.Web.Api.Server.Services.WordDocuments
{
    public partial class WordDocumentService
    {
        private delegate ValueTask<WordDocument> ReturningWordDocumentFunction();
        private delegate IQueryable<WordDocument> ReturningQueryableWordDocumentFunction();

        private async ValueTask<WordDocument> TryCatch(ReturningWordDocumentFunction returningWordDocumentFunction)
        {
            try
            {
                return await returningWordDocumentFunction();
            }
            catch (NullWordDocumentException nullWordDocumentException)
            {
                throw CreateAndLogValidationException(nullWordDocumentException);
            }
            catch (InvalidWordDocumentInputException invalidWordDocumentInputException)
            {
                throw CreateAndLogValidationException(invalidWordDocumentInputException);
            }
            catch (NotFoundWordDocumentException nullWordDocumentException)
            {
                throw CreateAndLogValidationException(nullWordDocumentException);
            }
            catch (SqlException sqlException)
            {
                throw CreateAndLogCriticalDependencyException(sqlException);
            }
            catch (DuplicateKeyException duplicateKeyException)
            {
                var alreadyExistsWordDocumentException =
                    new AlreadyExistsWordDocumentException(duplicateKeyException);

                throw CreateAndLogValidationException(alreadyExistsWordDocumentException);
            }
            catch (DbUpdateConcurrencyException dbUpdateConcurrencyException)
            {
                var lockedWordDocumentException = new LockedWordDocumentException(dbUpdateConcurrencyException);

                throw CreateAndLogDependencyException(lockedWordDocumentException);
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

        private IQueryable<WordDocument> TryCatch(ReturningQueryableWordDocumentFunction returningQueryableWordDocumentFunction)
        {
            try
            {
                return returningQueryableWordDocumentFunction();
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

        private WordDocumentServiceException CreateAndLogServiceException(Exception exception)
        {
            var WordDocumentServiceException = new WordDocumentServiceException(exception);
            this.loggingBroker.LogError(WordDocumentServiceException);

            return WordDocumentServiceException;
        }

        private WordDocumentDependencyException CreateAndLogDependencyException(Exception exception)
        {
            var WordDocumentDependencyException = new WordDocumentDependencyException(exception);
            this.loggingBroker.LogError(WordDocumentDependencyException);

            return WordDocumentDependencyException;
        }

        private WordDocumentDependencyException CreateAndLogCriticalDependencyException(Exception exception)
        {
            var WordDocumentDependencyException = new WordDocumentDependencyException(exception);
            this.loggingBroker.LogCritical(WordDocumentDependencyException);

            return WordDocumentDependencyException;
        }

        private WordDocumentValidationException CreateAndLogValidationException(Exception exception)
        {
            var WordDocumentValidationException = new WordDocumentValidationException(exception);
            this.loggingBroker.LogError(WordDocumentValidationException);

            return WordDocumentValidationException;
        }
    }
}
