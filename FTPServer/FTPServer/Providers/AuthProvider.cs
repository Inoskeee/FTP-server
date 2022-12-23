using FubarDev.FtpServer.AccountManagement;
using System.Security.Claims;

namespace FTPServer.Providers
{
    public class AuthProvider : IMembershipProvider
    {
        public Task<MemberValidationResult> ValidateUserAsync(string username, string password)
        {
            
            if(username == "admin" || password == "123")
            {
                var user = new ClaimsPrincipal(
                    new ClaimsIdentity(
                        new[]
                        {
                        new Claim(ClaimsIdentity.DefaultNameClaimType, username),
                        new Claim(ClaimsIdentity.DefaultRoleClaimType, username),
                        new Claim(ClaimsIdentity.DefaultRoleClaimType, "user"),
                        },
                        "custom"));

                return Task.FromResult(new MemberValidationResult(MemberValidationStatus.AuthenticatedUser, user));
            }
            return Task.FromResult(new MemberValidationResult(MemberValidationStatus.InvalidLogin));
        }
    }
}
