namespace Backup.Web.Api.Server.Authorization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Backup.Web.Api.Server.Models.Users;
using Backup.Web.Api.Server.Models.Entities;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuthorizeAttribute : Attribute
{
    private readonly IList<Roles> _roles;

    public AuthorizeAttribute(params Roles[] roles)
    {
        _roles = roles ?? new Roles[] { };
    }

    //public void OnAuthorization(AuthorizationFilterContext context)
    //{
    //    // skip authorization if action is decorated with [AllowAnonymous] attribute
    //    var allowAnonymous = context.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any();
    //    if (allowAnonymous)
    //        return;

    //    // authorization
    //    var user = (User)context.HttpContext.Items["User"];
    //    if (user == null || (_roles.Any() && !_roles.Contains(user.Role)))
    //    {
    //        // not logged in or role not authorized
    //        context.Result = new JsonResult(new { message = "Unauthorized" }) { StatusCode = StatusCodes.Status401Unauthorized };
    //    }
    //}
}