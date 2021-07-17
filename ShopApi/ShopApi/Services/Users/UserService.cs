using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ShopApi.Models.Users;
using ShopApi.Entity;
using ShopApi.Extensions;
using ShopApi.Helpers;
using ShopApi.Entity.Models;
using ShopApi.Utils;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using AutoMapper;

namespace ShopApi.Services.Users
{
    public interface IUserService
    {
        AuthenticateResponse Authenticate(AuthenticateRequest model, string ipAddress);
        AuthenticateResponse RefreshToken(string token, string ipAddress);
        void RevokeToken(string token, string ipAddress);
        AuthenticationProviderResponse AuthenticateWithThirdParty(AuthenticationProviderRequest model, string ipAddress);
        Task<IEnumerable<User>> GetAllAsync();
        Task<User> GetByIdAsync(string Id);
        void Update(string id, UpdateUserRequest model);
        void Register(UserRegisterRequest model, string origin);
        void VerifyEmail(string token);
        void ForgotPassword(ForgotPasswordRequest model, string origin);
        void ResetPassword(ResetPasswordRequest model);
        
    }
    public class UserService : IUserService
    {
        private readonly IServiceScopeFactory scopeFactory;
        private readonly IMapper _mapper;
        private readonly AppSettings _appSettings;
        private readonly IEmailService _emailService;


        public UserService(IServiceScopeFactory scopeFactory,
            IMapper mapper,
            IOptions<AppSettings> appSettings,
            IEmailService emailService)
        {
            this.scopeFactory = scopeFactory;
            _appSettings = appSettings.Value;
            _emailService = emailService;
            _mapper = mapper;
        }

        #region Authenticate
        public AuthenticateResponse Authenticate(AuthenticateRequest model, string ipAddress)
        {
            checkValidModel(model);
            using (var scope = scopeFactory.CreateScope())
            {
                var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var found = appDb.UserAccounts
                    .Where(x => x.Username == model.Username)
                    .Include(account => account.User)
                    .FirstOrDefault();

                if (found == null)
                    throw new ApplicationException("Username or password is incorrect.");

                if (found.User == null || !found.User.IsVerified)
                    throw new ApplicationException("Username or password is incorrect.");

                string hashed = PasswordUtil.HashPasswordWithSalt(model.Password, found.PasswordSalt);

                if (hashed.Equals(found.PasswordHash))
                {
                    // authenticate successfully => generate jwt and refresh token
                    var response = new AuthenticateResponse
                    {
                        Token = generateJwtToken(found.User)
                    };
                    var refreshToken = generateRefreshToken(ipAddress);
                    found.User.RefreshTokens.Add(refreshToken);

                    // remove old refresh tokens from account
                    removeOldRefreshTokens(found.User);

                    // save changes to db
                    appDb.Update(found);
                    appDb.SaveChanges();

                    response.CopyPropertiesFrom(found.User);
                    response.RefreshToken = refreshToken.Token;
                    return response;
                }
                else
                {
                    throw new ApplicationException("Username or password is incorrect.");
                }
            }
        }

        public AuthenticateResponse RefreshToken(string token, string ipAddress)
        {
            using (var scope = scopeFactory.CreateScope())
            {
                var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var (refreshToken, user) = getRefreshToken(token);

                // replace old refresh token with a new one and save
                var newRefreshToken = generateRefreshToken(ipAddress);
                refreshToken.Revoked = DateTime.UtcNow;
                refreshToken.RevokedByIp = ipAddress;
                refreshToken.ReplacedByToken = newRefreshToken.Token;
                user.RefreshTokens.Add(newRefreshToken);

                removeOldRefreshTokens(user);

                appDb.Update(user);
                appDb.SaveChanges();

                // generate new jwt
                var jwtToken = generateJwtToken(user);

                var response = new AuthenticateResponse();
                response.CopyPropertiesFrom(user);
                response.Token = jwtToken;
                response.RefreshToken = newRefreshToken.Token;
                return response;
            }

        }

        public void RevokeToken(string token, string ipAddress)
        {
            var (refreshToken, user) = getRefreshToken(token);
            using (var scope = scopeFactory.CreateScope())
            {
                var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                // revoke token and save
                refreshToken.Revoked = DateTime.UtcNow;
                refreshToken.RevokedByIp = ipAddress;
                appDb.Update(user);
                appDb.SaveChanges();
            }
        }

        public AuthenticationProviderResponse AuthenticateWithThirdParty(AuthenticationProviderRequest model, string ipAddress)
        {
            checkValidModel(model);
            using (var scope = scopeFactory.CreateScope())
            {
                var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var found = appDb.AuthenticationProviders
                    .Where(x => x.Id == String.Format("{0}{1}", model.ProviderType.ToString(), model.KeyProvided))
                    .Include(x => x.User)
                    .FirstOrDefault();

                if (found == null)
                {
                    // register new authentication privider
                    Register(model);

                    found = appDb.AuthenticationProviders
                    .Where(x => x.Id == String.Format("{0}{1}", model.ProviderType.ToString(), model.KeyProvided))
                    .Include(x => x.User)
                    .FirstOrDefault();
                }

                var response = new AuthenticationProviderResponse
                {
                    Token = generateJwtToken(found.User)
                };
                response.CopyPropertiesFrom(found.User);
                var refreshToken = generateRefreshToken(ipAddress);
                found.User.RefreshTokens.Add(refreshToken);

                // remove old refresh tokens from account
                removeOldRefreshTokens(found.User);

                // save changes to db
                appDb.Update(found);
                appDb.SaveChanges();

                response.RefreshToken = refreshToken.Token;
                return response;
            }
        }

        #endregion

        #region Common Methods ( Get, Update, Delete )
        public async Task<User> GetByIdAsync(string Id)
        {
            using (var scope = scopeFactory.CreateScope())
            {
                var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                return await appDb.Users.SingleOrDefaultAsync(x => x.Id == Id);
            }
        }

        public async Task<IEnumerable<User>> GetAllAsync()
        {
            using (var scope = scopeFactory.CreateScope())
            {
                var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                return await appDb.Users.ToListAsync();
            }
        }

        public void Update(string id, UpdateUserRequest model)
        {
            using (var scope = scopeFactory.CreateScope())
            {
                var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = appDb.Users.Where(x => x.Id == id).FirstOrDefault();
                if (user == null)
                    throw new ApplicationException("User not found.");

                if (!string.IsNullOrEmpty(model.Firstname))
                    user.Firstname = model.Firstname;

                if (!string.IsNullOrEmpty(model.Lastname))
                    user.Lastname = model.Lastname;

                if (!string.IsNullOrEmpty(model.Phone))
                    user.Phone = model.Phone;

                if (!string.IsNullOrEmpty(model.Email))
                    user.Email = model.Email;

                if (!string.IsNullOrEmpty(model.ImageUrl))
                    user.ImageUrl = model.ImageUrl;

                if (model.Birthday != null)
                    user.Birthday = model.Birthday;

                appDb.SaveChanges();
            }
        }
        #endregion

        #region Register
        public void Register(UserRegisterRequest model, string origin)
        {
            // check
            checkValidModel(model);

            var user = new User();
            user.CopyPropertiesFrom(model);
            user.Id = IdentityUtil.GenerateId();
            user.Role = Role.User;
            user.Created = DateTimeOffset.Now;
            user.VerificationToken = randomTokenString();

            var account = new UserAccount();
            account.CopyPropertiesFrom(model);

            // hash password
            var hashedResult = PasswordUtil.HashPasswordWithRandomSalt(model.Password);
            account.Id = String.Format("AC{0}", user.Id);
            account.PasswordHash = hashedResult.hashed;
            account.PasswordSalt = hashedResult.salt;
            account.Created = DateTimeOffset.Now;
            account.User = user;

            using (var scope = scopeFactory.CreateScope())
            {
                var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // save account
                appDb.UserAccounts.Add(account);
                appDb.SaveChanges();

                // save mail
                sendVerificationEmail(user, origin);
            }
        }

        // Register new Authentication Provider
        public void Register(AuthenticationProviderRequest model)
        {
            var authenticationProvider = new AuthenticationProvider();
            authenticationProvider.User = new User
            {
                Id = IdentityUtil.GenerateId(),
                Created = DateTimeOffset.Now,
                Verified = DateTimeOffset.Now
            };
            authenticationProvider.CopyPropertiesFrom(model);

            using (var scope = scopeFactory.CreateScope())
            {
                var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                authenticationProvider.Id = String.Format("{0}{1}", authenticationProvider.ProviderTypeString, authenticationProvider.KeyProvided);
                appDb.AuthenticationProviders.Add(authenticationProvider);
                appDb.SaveChanges();
            }
        }

        public void VerifyEmail(string token)
        {
            using (var scope = scopeFactory.CreateScope())
            {
                var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var user = appDb.Users.SingleOrDefault(x => x.VerificationToken == token);

                if (user == null) throw new AppException("Verification failed");

                user.VerificationToken = null;
                user.Verified = DateTime.Now;

                appDb.Users.Update(user);
                appDb.SaveChanges();
            }
        }
        #endregion

        #region Password

        public void ForgotPassword(ForgotPasswordRequest model, string origin)
        {
            using (var scope = scopeFactory.CreateScope())
            {
                var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = appDb.Users.SingleOrDefault(x => x.Email == model.Email);
                if (user == null) return;

                // create reset token that expired after 15 mintutes
                user.ResetToken = randomTokenString();
                user.ResetTokenExpires = DateTime.UtcNow.AddMinutes(15);

                appDb.Users.Update(user);
                appDb.SaveChanges();

                //send mail
                sendPasswordResetEmail(user, origin);
            }
        }

        public void ResetPassword(ResetPasswordRequest model)
        {
            using (var scope = scopeFactory.CreateScope())
            {
                var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = appDb.Users.SingleOrDefault(x =>
                    x.ResetToken == model.Token &&
                    x.ResetTokenExpires > DateTime.UtcNow);

                if (user == null)
                    throw new AppException("Invalid token");

                //match account 
                var account = appDb.UserAccounts.SingleOrDefault(x => x.UserId == user.Id);
                if (account == null)
                    throw new AppException("Invalid token");

                // update password and remove reset token
                var hashedResult = PasswordUtil.HashPasswordWithRandomSalt(model.Password);
                account.PasswordHash = hashedResult.hashed;
                account.PasswordSalt = hashedResult.salt;
                user.ResetToken = null;
                user.ResetTokenExpires = null;
                user.PasswordReset = DateTime.UtcNow;

                appDb.Users.Update(user);
                appDb.UserAccounts.Update(account);
                appDb.SaveChanges();
            }
        }

        #endregion

        #region Helper Methods

        private void sendPasswordResetEmail(User account, string origin)
        {
            string message;
            if (!string.IsNullOrEmpty(origin))
            {
                var resetUrl = $"{origin}/account/reset-password?token={account.ResetToken}";
                message = $@"<p>Please click the below link to reset your password, the link will be valid for 1 day:</p>
                             <p><a href=""{resetUrl}"">{resetUrl}</a></p>";
            }
            else
            {
                message = $@"<p>Please use the below token to reset your password with the <code>/accounts/reset-password</code> api route:</p>
                             <p><code>{account.ResetToken}</code></p>";
            }

            _emailService.Send(
                to: account.Email,
                subject: "Sign-up Verification API - Reset Password",
                html: $@"<h4>Reset Password Email</h4>
                         {message}"
            );
        }

        private void sendVerificationEmail(User account, string origin)
        {
            string message;
            if (!string.IsNullOrEmpty(origin))
            {
                var verifyUrl = $"{origin}/account/verify-email?token={account.VerificationToken}";
                message = $@"<p>Please click the below link to verify your email address:</p>
                             <p><a href=""{verifyUrl}"">{verifyUrl}</a></p>";
            }
            else
            {
                message = $@"<p>Please use the below token to verify your email address with the <code>/accounts/verify-email</code> api route:</p>
                             <p><code>{account.VerificationToken}</code></p>";
            }

            _emailService.Send(
                to: account.Email,
                subject: "Sign-up Verification API - Verify Email",
                html: $@"<h4>Verify Email</h4>
                         <p>Thanks for registering!</p>
                         {message}"
            );
        }

        private string randomTokenString()
        {
            using var rngCryptoServiceProvider = new RNGCryptoServiceProvider();
            var randomBytes = new byte[40];
            rngCryptoServiceProvider.GetBytes(randomBytes);
            // convert random bytes to hex string
            return BitConverter.ToString(randomBytes).Replace("-", "");
        }

        private void checkValidModel(AuthenticationProviderRequest model)
        {
            if (string.IsNullOrWhiteSpace(model.KeyProvided))
                throw new ApplicationException("`KeyProvided` can not be null or white space.");

            if (model.ProviderType == ProviderType.Undefined || !Enum.IsDefined(typeof(ProviderType), model.ProviderType))
                throw new ApplicationException("`ProviderType` is invalid.");
        }

        private void checkValidModel(AuthenticateRequest model)
        {
            if (string.IsNullOrWhiteSpace(model.Username))
                throw new ApplicationException("Username is required.");

            if (string.IsNullOrWhiteSpace(model.Password))
                throw new ApplicationException("Password is required.");

            if (model.ReturnUrl != null && !RegexUtilities.IsValidUrl(model.ReturnUrl))
                throw new ApplicationException("`Return url` is invalid.");
        }

        private void checkValidModel(UserRegisterRequest model)
        {
            if (string.IsNullOrWhiteSpace(model.Username))
                throw new ApplicationException("Username is required.");

            if (isExistedUsername(model.Username))
                throw new ApplicationException("That username is already taken. Please try another.");

            if (string.IsNullOrWhiteSpace(model.Password))
                throw new ApplicationException("Password is required.");

            if (model.Email != null)
            {
                if (!RegexUtilities.IsValidEmail(model.Email))
                    throw new ApplicationException("That email is invalid. Please try another.");

                if (isExistedEmail(model.Email))
                    throw new ApplicationException("This email is already used in another account. Please try another.");
            }
        }

        private bool isExistedEmail(string email)
        {
            using (var scope = scopeFactory.CreateScope())
            {
                var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                if (appDb.Users.Any(user => user.Email == email))
                    return true;

                return false;
            }
        }

        private bool isExistedUsername(string username)
        {
            using (var scope = scopeFactory.CreateScope())
            {
                var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                if (appDb.UserAccounts.Any(x => x.Username == username))
                    return true;

                return false;
            }
        }

        private (RefreshToken, User) getRefreshToken(string token)
        {
            using (var scope = scopeFactory.CreateScope())
            {
                var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = appDb.Users.SingleOrDefault(u => u.RefreshTokens.Any(t => t.Token == token));
                if (user == null) throw new AppException("Invalid token");
                var refreshToken = user.RefreshTokens.Single(x => x.Token == token);
                if (!refreshToken.IsActive) throw new AppException("Invalid token");
                return (refreshToken, user);
            }
        }

        private string generateJwtToken(User user)
        {
            // generate token that is valid for 7 days
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_appSettings.Secret);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { new Claim("id", user.Id.ToString()) }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private RefreshToken generateRefreshToken(string ipAddress)
        {
            return new RefreshToken
            {
                Token = randomTokenString(),
                Expires = DateTime.UtcNow.AddDays(7),
                Created = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };
        }

        private void removeOldRefreshTokens(User user)
        {
            using (var scope = scopeFactory.CreateScope())
            {
                var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                user.RefreshTokens.RemoveAll(x => !x.IsActive && x.Created.AddDays(_appSettings.RefreshTokenTTL) <= DateTime.UtcNow);
                appDb.Update(user);
                appDb.SaveChanges();
            }
        }

        #endregion

    }
}
