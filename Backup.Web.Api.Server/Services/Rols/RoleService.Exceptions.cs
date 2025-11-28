using Backup.Web.Api.Server.Models.Roles.Exceptions;
using Backup.Web.Api.Server.Models.Rols;
using Backup.Web.Api.Server.Models.Users;
using Backup.Web.Api.Server.Models.Users.Exceptions;
using EFxceptions.Models.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Backup.Web.Api.Server.Services.Roles
{
    public partial class RoleService
    {
        private delegate ValueTask<Role> ReturningRoleFunction();
        private delegate ValueTask<bool> ReturningAuthenticateResponseFunction();
        private delegate IQueryable<Role> ReturningQueryableRoleFunction();

        private async ValueTask<bool> TryCatch(ReturningAuthenticateResponseFunction returningRoleFunction)
        {
            try
            {
                return await returningRoleFunction();
            }
            catch (NullRoleException nullRoleException)
            {
                throw CreateAndLogValidationException(nullRoleException);
            }
            catch (InvalidRoleException invalidRoleException)
            {
                throw CreateAndLogValidationException(invalidRoleException);
            }
            catch (NotFoundRoleException nullRoleException)
            {
                throw CreateAndLogValidationException(nullRoleException);
            }
            catch (DuplicateKeyException duplicateKeyException)
            {
                var alreadyExistsRoleException =
                    new AlreadyExistsRoleException(duplicateKeyException);

                throw CreateAndLogValidationException(alreadyExistsRoleException);
            }
            catch (SqlException sqlException)
            {
                throw CreateAndLogCriticalDependencyException(sqlException);
            }
            catch (DbUpdateConcurrencyException dbUpdateConcurrencyException)
            {
                var lockedRoleException = new LockedRoleException(dbUpdateConcurrencyException);

                throw CreateAndLogDependencyException(lockedRoleException);
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
        private async ValueTask<Role> TryCatch(ReturningRoleFunction returningRoleFunction)
        {
            try
            {
                return await returningRoleFunction();
            }
            catch (NullRoleException nullRoleException)
            {
                throw CreateAndLogValidationException(nullRoleException);
            }
            catch (InvalidRoleException invalidRoleException)
            {
                throw CreateAndLogValidationException(invalidRoleException);
            }
            catch (NotFoundRoleException nullRoleException)
            {
                throw CreateAndLogValidationException(nullRoleException);
            }
            catch (DuplicateKeyException duplicateKeyException)
            {
                var alreadyExistsRoleException =
                    new AlreadyExistsRoleException(duplicateKeyException);

                throw CreateAndLogValidationException(alreadyExistsRoleException);
            }
            catch (SqlException sqlException)
            {
                throw CreateAndLogCriticalDependencyException(sqlException);
            }
            catch (DbUpdateConcurrencyException dbUpdateConcurrencyException)
            {
                var lockedRoleException = new LockedRoleException(dbUpdateConcurrencyException);

                throw CreateAndLogDependencyException(lockedRoleException);
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
        private IQueryable<Role> TryCatch(ReturningQueryableRoleFunction returningQueryableRoleFunction)
        {
            try
            {
                return returningQueryableRoleFunction();
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

        private RoleServiceException CreateAndLogServiceException(Exception exception)
        {
            var RoleServiceException = new RoleServiceException(exception);
            this.loggingBroker.LogError(RoleServiceException);

            return RoleServiceException;
        }

        private RoleDependencyException CreateAndLogDependencyException(Exception exception)
        {
            var RoleDependencyException = new RoleDependencyException(exception);
            this.loggingBroker.LogError(RoleDependencyException);

            return RoleDependencyException;
        }

        private RoleDependencyException CreateAndLogCriticalDependencyException(Exception exception)
        {
            var RoleDependencyException = new RoleDependencyException(exception);
            this.loggingBroker.LogCritical(RoleDependencyException);

            return RoleDependencyException;
        }

        private Exception CreateAndLogValidationException(Exception exception)
        {
            var RoleValidationException = new RoleValidationException(exception);
            this.loggingBroker.LogError(RoleValidationException);

            return RoleValidationException;
        }
    }
}
