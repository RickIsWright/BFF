// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Duende.Bff;

internal static class HttpContextExtensions
{
    public static void CheckForBffMiddleware(this HttpContext context, BffOptions options)
    {
        if (options.EnforceBffMiddleware)
        {
            var found = context.Items.TryGetValue(Constants.BffMiddlewareMarker, out _);
            if (!found)
            {
                throw new InvalidOperationException(
                    "The BFF middleware is missing in the pipeline. Add 'app.UseBff' after 'app.UseRouting' but before 'app.UseAuthorization'");
            }
        }
    }

    public static bool CheckAntiForgeryHeader(this HttpContext context, BffOptions options)
    {
        var antiForgeryHeader = context.Request.Headers[options.AntiForgeryHeaderName].FirstOrDefault();
        return antiForgeryHeader != null && antiForgeryHeader == options.AntiForgeryHeaderValue;
    }

    public static async Task<string?> GetManagedAccessToken(this HttpContext context, TokenType tokenType, UserTokenRequestParameters? userAccessTokenParameters = null)
    {
        string? token;

        if (tokenType == TokenType.User)
        {
            token = (await context.GetUserAccessTokenAsync(userAccessTokenParameters)).AccessToken;
        }
        else if (tokenType == TokenType.Client)
        {
            token = (await context.GetClientAccessTokenAsync()).AccessToken;
        }
        else
        {
            token = (await context.GetUserAccessTokenAsync(userAccessTokenParameters)).AccessToken;

            if (string.IsNullOrEmpty(token))
            {
                token = (await context.GetClientAccessTokenAsync()).AccessToken;
            }
        }

        return token;
    }

    public static bool IsAjaxRequest(this HttpContext context)
    {
        if ("cors".Equals(context.Request.Headers["Sec-Fetch-Mode"].ToString(), StringComparison.OrdinalIgnoreCase))
            return true;
        if ("XMLHttpRequest".Equals(context.Request.Query["X-Requested-With"].ToString(), StringComparison.OrdinalIgnoreCase))
            return true;
        if ("XMLHttpRequest".Equals(context.Request.Headers["X-Requested-With"].ToString(), StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }
}