using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using MLM_Level.Services;

namespace MLM_Level.Filters
{
    public class MaintenanceModeFilter : IAsyncActionFilter
    {
        private readonly IMaintenanceModeService _maintenance;

        public MaintenanceModeFilter(IMaintenanceModeService maintenance)
        {
            _maintenance = maintenance;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
            {
                await next();
                return;
            }

            var controller = descriptor.ControllerName;
            var action = descriptor.ActionName;

            if (string.Equals(controller, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            if (string.Equals(controller, "Home", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(action, "Error", StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            if (await _maintenance.IsSiteOnlineAsync())
            {
                await next();
                return;
            }

            var supportEmail = await _maintenance.GetSupportEmailAsync();
            var siteName = await _maintenance.GetSiteNameAsync();

            context.HttpContext.Items["MaintenanceMode"] = true;
            context.HttpContext.Items["MaintenanceSupportEmail"] = supportEmail;
            context.HttpContext.Items["MaintenanceSiteName"] = siteName;

            if (context.Controller is Controller controllerBase)
            {
                controllerBase.ViewBag.MaintenanceMode = true;
                controllerBase.ViewBag.MaintenanceSupportEmail = supportEmail;
                controllerBase.ViewBag.MaintenanceSiteName = siteName;
            }

            if (string.Equals(controller, "User", StringComparison.OrdinalIgnoreCase))
            {
                await context.HttpContext.SignOutAsync("UserAuth");
                context.Result = new RedirectToActionResult("Login", "Account", new { maintenance = 1 });
                return;
            }

            if (string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(action, "Logout", StringComparison.OrdinalIgnoreCase))
                {
                    await next();
                    return;
                }

                if (HttpMethods.IsPost(context.HttpContext.Request.Method))
                {
                    var blockedActions = new[] { "Login", "Register", "ForgotPassword", "ResetPassword" };
                    if (blockedActions.Contains(action, StringComparer.OrdinalIgnoreCase))
                    {
                        context.Result = new RedirectToActionResult("Login", "Account", new { maintenance = 1 });
                        return;
                    }
                }

                if (string.Equals(action, "Login", StringComparison.OrdinalIgnoreCase) &&
                    HttpMethods.IsGet(context.HttpContext.Request.Method))
                {
                    await context.HttpContext.SignOutAsync("UserAuth");
                }
            }

            await next();
        }
    }
}
