using System.Reflection;
using backend.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Tests;

public class MediaStudioAuthorizationTests
{
    [Fact]
    public void MediaStudioActions_InheritTheAdminOnlyPolicy()
    {
        var controllerAuthorization = typeof(AdminController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(controllerAuthorization);
        Assert.Equal("AdminOnly", controllerAuthorization.Policy);

        var mediaStudioActions = typeof(AdminController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>()
                .Any(attribute => attribute.Template?.Contains("media-studio", StringComparison.Ordinal) == true));

        Assert.NotEmpty(mediaStudioActions);
        Assert.All(mediaStudioActions, action =>
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>()));
    }
}
