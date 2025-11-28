




using Backup.Web.Api.Server.Models.Roles;
using Backup.Web.Api.Server.Models.Roles.Exceptions;
using System;
using System.Linq;
using Backup.Web.Api.Server.Models.Rols;

namespace Backup.Web.Api.Server.Services.Roles
{
    public partial class RoleService
    {
        private void ValidateRoleOnCreate(Role Role)
        {
            ValidateRoleIsNull(Role);
            ValidateRoleIdIsNull(Role.Id);
            ValidateRoleFields(Role);
            //ValidateInvalidAuditFields(Role);
            //ValidateAuditFieldsDataOnCreate(Role);
            //ValidateCreatedDateIsRecent(Role);
        }

        private void ValidateRoleOnModify(Role Role)
        {
            ValidateRoleIsNull(Role);
            ValidateRoleIdIsNull(Role.Id);
            ValidateRoleFields(Role);
            //ValidateInvalidAuditFields(Role);
        }

        private static void ValidateStorageRole(Role storageRole, Guid RoleId)
        {
            if (storageRole == null)
            {
                throw new NotFoundRoleException(RoleId);
            }
        }

        //private void ValidateCreatedDateIsRecent(Role Role)
        //{
        //    if (IsDateNotRecent(Role.CreatedDate))
        //    {
        //        throw new InvalidRoleException(
        //            parameterName: nameof(Role.CreatedDate),
        //            parameterValue: Role.CreatedDate);
        //    }
        //}

        //private void ValidateAuditFieldsDataOnCreate(Role Role)
        //{
        //    switch (Role)
        //    {
        //        case { } when Role.UpdatedDate != Role.CreatedDate:
        //            throw new InvalidRoleException(
        //            parameterName: nameof(Role.UpdatedDate),
        //            parameterValue: Role.UpdatedDate);
        //    }
        //}

        //private void ValidateInvalidAuditFields(Role Role)
        //{
        //    switch (Role)
        //    {
        //        case { } when IsInvalid(Role.CreatedDate):
        //            throw new InvalidRoleException(
        //            parameterName: nameof(Role.CreatedDate),
        //            parameterValue: Role.CreatedDate);
        //        case { } when IsInvalid(Role.UpdatedDate):
        //            throw new InvalidRoleException(
        //            parameterName: nameof(Role.UpdatedDate),
        //            parameterValue: Role.UpdatedDate);
        //    }
        //}
        private void ValidateRoleEmail(string email)
        {
            if (IsInvalid(email))
            {
                throw new InvalidRoleException(
                parameterName: nameof(email),
                parameterValue: email);
            }
        }
        private void ValidateRoleFields(Role Role)
        {
            if (IsInvalid(Role.Name))
            {
                throw new InvalidRoleException(
                    parameterName: nameof(Role.Name),
                    parameterValue: Role.Name);
            }

        }

        private void ValidateRoleIdIsNull(Guid RoleId)
        {
            if (RoleId == default)
            {
                throw new InvalidRoleException(
                    parameterName: nameof(Role.Id),
                    parameterValue: RoleId);
            }
        }

        private void ValidateRoleIsNull(Role Role)
        {
            if (Role is null)
            {
                throw new NullRoleException();
            }
        }

        private static bool IsInvalid(string input) => String.IsNullOrWhiteSpace(input);
        private static bool IsInvalid(DateTimeOffset input) => input == default;

        private bool IsDateNotRecent(DateTimeOffset dateTime)
        {
            DateTimeOffset now = this.dateTimeBroker.GetCurrentDateTime();
            int oneMinute = 1;
            TimeSpan difference = now.Subtract(dateTime);

            return Math.Abs(difference.TotalMinutes) >= oneMinute;
        }

        private void ValidateStorageRoles(IQueryable<Role> storageRoles)
        {
            if (storageRoles.Count() == 0)
            {
                this.loggingBroker.LogWarning("No Roles found in storage.");
            }
        }
    }
}
