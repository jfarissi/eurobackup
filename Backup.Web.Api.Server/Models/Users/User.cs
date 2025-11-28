//using Backup.Web.Api.Server.Models.Entities;
//using Backup.Web.Api.Server.Models.UserContacts;
using Microsoft.AspNetCore.Identity;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Backup.Web.Api.Server.Models.Rols;

namespace Backup.Web.Api.Server.Models.Users
{
    public class User : IdentityUser<Guid>
    {
        public User()
        {
            Id = Guid.NewGuid();
        }

        public override Guid Id
        {
            get => base.Id;
            set => base.Id = value;
        }

        public override string? UserName
        {
            get => base.Email;
            set => base.Email = value;
        }

        public override string? PhoneNumber
        {
            get => base.PhoneNumber;
            set => base.PhoneNumber = value;
        }

        public string? Name { get; set; }
        public string? FamilyName { get; set; }
        public UserStatus Status { get; set; }
        public  DateTimeOffset CreatedDate 
        { 
            get  ; 
            set  ; 
        }
        public DateTimeOffset UpdatedDate { get; set; }
        public string? PasswordHash { get; set; }
        public Role? Role { internal get; set; }

        public bool IsAdmin { internal get; set; }
        [NotMapped]
        public string? Token { get; set; }

        //[JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        //public IEnumerable<UserContact> UserContacts { internal get; set; }
    }
}
