using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Web.Client.Extensions;

public static class Security
{
    extension(AuthenticationStateProvider authStateProvider)
    {
        public async Task<bool> IsAuthenticated()
        {
            var authState = await authStateProvider.GetAuthenticationStateAsync();
            
            return authState.User.Identity?.IsAuthenticated == true;
        }

        public async Task<bool> IsAdmin()
        {
            var authState = await authStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            if (user.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            return user.IsInRole(Domain.Shared.Constants.AdminRoleName);
        }
    }
}
