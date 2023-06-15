// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System;
using Duende.Bff;
using Duende.Bff.Yarp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Host6;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Add BFF services to DI - also add server-side session management
        services.AddBff(options =>
            {
                //options.UserEndpointReturnNullForAnonymousUser = true;
            })
            .AddRemoteApis()
            .AddServerSideSessions();

        // local APIs
        services.AddControllers();

        // cookie options
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = "cookie";
                options.DefaultChallengeScheme = "oidc";
                options.DefaultSignOutScheme = "oidc";
            })
            .AddCookie("cookie", options =>
            {
                // set session lifetime
                options.ExpireTimeSpan = TimeSpan.FromHours(8);

                // sliding or absolute
                options.SlidingExpiration = false;

                // host prefixed cookie name
                options.Cookie.Name = "__Host-spa6";

                // strict SameSite handling
                options.Cookie.SameSite = SameSiteMode.Strict;
            })
            .AddOpenIdConnect("oidc", options =>
            {
                options.Authority = "https://localhost:5001";

                // confidential client using code flow + PKCE
                options.ClientId = "spa";
                options.ClientSecret = "secret";
                options.ResponseType = "code";
                options.ResponseMode = "query";

                options.MapInboundClaims = false;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.SaveTokens = true;

                // request scopes + refresh tokens
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("api");
                options.Scope.Add("offline_access");
            });
        services.AddSingleton<ImpersonationAccessTokenRetriever>();

        services.AddUserAccessTokenHttpClient("api",
            configureClient: client => 
            { 
                client.BaseAddress = new Uri("https://localhost:5010/api"); 
            });
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseSerilogRequestLogging();
        app.UseDeveloperExceptionPage();

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.UseAuthentication();
        app.UseRouting();

        // adds antiforgery protection for local APIs
        app.UseBff();

        // adds authorization for local and remote API endpoints
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            // local APIs
            endpoints.MapControllers()
                .RequireAuthorization()
                .AsBffApiEndpoint();

            // login, logout, user, backchannel logout...
            endpoints.MapBffManagementEndpoints();

            //////////////////////////////////////
            // proxy endpoints for cross-site APIs
            //////////////////////////////////////

            // On this path, we use a client credentials token
            endpoints.MapRemoteBffApiEndpoint("/api/client-token", "https://localhost:5010")
                .RequireAccessToken(TokenType.Client);

            // On this path, we use a user token if logged in, and fall back to a client credentials token if not
            endpoints.MapRemoteBffApiEndpoint("/api/user-or-client-token", "https://localhost:5010")
                .RequireAccessToken(TokenType.UserOrClient);

            // On this path, we make anonymous requests
            endpoints.MapRemoteBffApiEndpoint("/api/anonymous", "https://localhost:5010");

            // On this path, we use the client token only if the user is logged in
            endpoints.MapRemoteBffApiEndpoint("/api/optional-user-token", "https://localhost:5010")
                .WithOptionalUserAccessToken();

            // On this path, we require the user token
            endpoints.MapRemoteBffApiEndpoint("/api/user-token", "https://localhost:5010")
                .RequireAccessToken(TokenType.User);

        });
    }
}
