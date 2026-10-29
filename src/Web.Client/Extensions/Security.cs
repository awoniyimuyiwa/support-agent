using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace Web.Client.Extensions;

public static class Security
{
    extension(AuthenticationStateProvider authStateProvider)
    {
        public async Task<bool> IsAuthenticated()
        {
            var authState = await authStateProvider.GetAuthenticationStateAsync();

            return authState.User.IsAuthenticated;
        }

        public async Task<bool> IsAdmin()
        {
            var authState = await authStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            return user.IsAdmin;
        }
    }

    extension(ClaimsPrincipal user)
    {
        public bool IsAuthenticated => user.Identity?.IsAuthenticated == true;

        public bool IsAdmin
        {
            get
            {
                if (!user.IsAuthenticated)
                {
                    return false;
                }

                return user.IsInRole(Domain.Shared.Constants.AdminRoleName);
            }
        }
    }
}
