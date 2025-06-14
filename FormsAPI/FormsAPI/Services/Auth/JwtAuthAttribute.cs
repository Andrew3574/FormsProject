﻿using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Models.Enums;
using Repositories;
using System.Security.Claims;

namespace FormsAPI.Services.Auth
{
    public class JwtAuthAttribute : Attribute, IAsyncActionFilter
    {
        private readonly string _role;

        public JwtAuthAttribute(string role = "user")
        {
            _role = role.ToLower();
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!context.HttpContext.Request.Cookies.TryGetValue("jwt", out string? token))
            {
                SetErrorResult(context, "Token not found");
                return;
            }
            if(await IsClaimsValidated(token!, context))
            {
                await next();
            }
            return;
        }

        private async Task<bool> IsClaimsValidated(string token, ActionExecutingContext context)
        {
            ClaimsPrincipal? claims = JwtAuthenticationService.ValidateToken(token);
            if (IsAuthenticated(claims, context) && IsAuthorized(claims, context) && await IsUserValidated(claims, context))
            {
                return true;
            }
            return false;
        }

        private bool IsAuthenticated(ClaimsPrincipal? claims, ActionExecutingContext context)
        {
            if (claims == null || !claims.Identity!.IsAuthenticated)
            {
                SetErrorResult(context, "Invalid token");
                return false;
            }
            if (string.IsNullOrEmpty(claims?.Claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value))
            {
                SetErrorResult(context, "Authenticate to proceed");
                return false;
            }
            return true;
        }

        private bool IsAuthorized(ClaimsPrincipal? claims, ActionExecutingContext context)
        {
            var role = claims?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
            if (string.IsNullOrEmpty(role) || !_role.Contains(role))
            {
                SetErrorResult(context,"Insufficient access");
                return false;
            }
            return true;
        }

        private async Task<bool> IsUserValidated(ClaimsPrincipal? claims, ActionExecutingContext context)
        {
            var userRepository = context.HttpContext.RequestServices.GetService<UsersRepository>();
            var user = await userRepository!.GetByEmail(claims?.Claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value!);
            if (user == null)
            {
                SetErrorResult(context, "User not found");
                return false;
            }
            if (user.State == UserState.blocked)
            {
                SetErrorResult(context, "You were blocked");
                return false;
            }
            return true;
        }
        private void SetErrorResult(ActionExecutingContext context, string message, int statusCode = 400)
        {
            context.HttpContext.Response.Headers.Append("ErrorMessage", message);
            context.HttpContext.Response.StatusCode = statusCode;
        }
    }
}
