﻿using System;
using System.Collections.Generic;
using System.IdentityModel.Claims;
using System.IdentityModel.Tokens;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using GroupManager.Utils;
using Microsoft.Identity.Client;
using Microsoft.IdentityModel.Protocols;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.Notifications;
using Microsoft.Owin.Security.OpenIdConnect;
using Owin;

namespace GroupManager
{
    public partial class Startup
    {
        private void ConfigureAuth(IAppBuilder app)
        {
            app.SetDefaultSignInAsAuthenticationType(CookieAuthenticationDefaults.AuthenticationType);

            app.UseCookieAuthentication(new CookieAuthenticationOptions { });

            app.UseOpenIdConnectAuthentication(
                new OpenIdConnectAuthenticationOptions
                {
                    Authority = Globals.Authority,
                    ClientId = Globals.ClientId,
                    RedirectUri = Globals.RedirectUri,
                    PostLogoutRedirectUri = Globals.RedirectUri,
                    Scope = Globals.BasicSignInScopes, // a basic set of permissions for user sign in & profile access
                    TokenValidationParameters = new System.IdentityModel.Tokens.TokenValidationParameters
                    {
                        // We'll inject our own issuer validation logic below.
                        ValidateIssuer = false,
                        NameClaimType = "name",
                    },
                    Notifications = new OpenIdConnectAuthenticationNotifications()
                    {
                        SecurityTokenValidated = OnSecurityTokenValidated,
                        AuthorizationCodeReceived = OnAuthorizationCodeRecieved,
                        AuthenticationFailed = OnAuthenticationFailed,
                    }
                });
        }

        private Task OnAuthenticationFailed(AuthenticationFailedNotification<OpenIdConnectMessage, OpenIdConnectAuthenticationOptions> context)
        {
            // Handle any unexpected errors during sign in
            context.OwinContext.Response.Redirect("/Error?message=" + context.Exception.Message);
            context.HandleResponse(); // Suppress the exception
            return Task.FromResult(0);
        }

        private async Task OnAuthorizationCodeRecieved(AuthorizationCodeReceivedNotification context)
        {
            // Upon successful sign in, get & cache a token using MSAL
            string userId = context.AuthenticationTicket.Identity.FindFirst(ClaimTypes.NameIdentifier).Value;
            ConfidentialClientApplication cc = new ConfidentialClientApplication(Globals.ClientId, Globals.RedirectUri, new ClientCredential(Globals.ClientSecret), new MsalSessionTokenCache(userId, context.OwinContext.Environment["System.Web.HttpContextBase"] as HttpContextBase));
            AuthenticationResult result = await cc.AcquireTokenByAuthorizationCodeAsync(new[] { "user.readbasic.all" }, context.Code);
        }

        private Task OnSecurityTokenValidated(SecurityTokenValidatedNotification<OpenIdConnectMessage, OpenIdConnectAuthenticationOptions> context)
        {
            // Verify the user signing in is a business user, not a consumer user.
            string[] issuer = context.AuthenticationTicket.Identity.FindFirst(Globals.IssuerClaim).Value.Split('/');
            string tenantId = issuer[(issuer.Length - 2)];
            if (tenantId == Globals.ConsumerTenantId)
            {
                throw new SecurityTokenValidationException("Consumer accounts are not supported for the Group Manager App.  Please log in with your work account.");
            }

            return Task.FromResult(0);
        }
    }
}