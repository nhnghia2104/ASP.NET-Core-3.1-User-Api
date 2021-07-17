using Microsoft.AspNetCore.Mvc;
using ShopApi.Models.Users;
using ShopApi.Extensions;
using ShopApi.Services.Users;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ShopApi.Entity;
using ShopApi.Helpers;

namespace ShopApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : BaseController
    {
        private readonly IUserService _userService;

        public UsersController(
            IUserService userService)
        {
            _userService = userService;
        }


        // Authenticate with registered account
        [HttpPost("authenticate")]
        public IActionResult Authenticate(AuthenticateRequest model)
        {
            try
            {
                var response = _userService.Authenticate(model, ipAddress());
                setTokenCookie(response.RefreshToken);
                return Ok(response);
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("refresh-token")]
        public ActionResult<AuthenticateResponse> RefreshToken()
        {
            try
            {
                var refreshToken = Request.Cookies["refreshToken"];
                var response = _userService.RefreshToken(refreshToken, ipAddress());
                setTokenCookie(response.RefreshToken);
                return Ok(response);
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("revoke-token")]
        public IActionResult RevokeToken(RevokeTokenRequest model)
        {
            try
            {
                // accept token from request body or cookie
                var token = model.Token ?? Request.Cookies["refreshToken"];

                if (string.IsNullOrEmpty(token))
                    return BadRequest(new { message = "Token is required" });

                // users can revoke their own tokens and admins can revoke any tokens
                if (!User.OwnsToken(token) && User.Role != Role.Admin)
                    return Unauthorized(new { message = "Unauthorized" });

                _userService.RevokeToken(token, ipAddress());
                return Ok(new { message = "Token revoked" });
            }
            catch (AppException ex)
            {
                return BadRequest(new { message = ex.Message });
            }

        }

        // Authenticate with third-party identity provider
        [HttpPost("third-party-authenticate")]
        public IActionResult Authenticate(AuthenticationProviderRequest model)
        {
            try
            {
                var response = _userService.AuthenticateWithThirdParty(model, ipAddress());
                setTokenCookie(response.RefreshToken);
                return Ok(response);
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("register")]
        public IActionResult Register(UserRegisterRequest model)
        {
            try
            {
                _userService.Register(model, Request.Headers["origin"]);
                return Ok(new { message = "Registration was successful, please check your email for verification" });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("verify-email")]
        public IActionResult VerifyEmail(VerifyEmailRequest model)
        {
            _userService.VerifyEmail(model.Token);
            return Ok(new { message = "Verification successful, you can now login" });
        }

        [HttpPost("forgot-password")]
        public IActionResult ForgotPassword(ForgotPasswordRequest model)
        {
            _userService.ForgotPassword(model, Request.Headers["origin"]);
            return Ok(new { message = "Please check your email for resetting password" });
        }

        [HttpPost("reset-password")]
        public IActionResult ResetPassword(ResetPasswordRequest model)
        {
            _userService.ResetPassword(model);
            return Ok(new { message = "Password reset successful, you can now login" });
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var users = await _userService.GetAllAsync();

            return Ok(users);
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(string id)
        {
            var user = await _userService.GetByIdAsync(id);
            var response = new UserResponse();
            response.CopyPropertiesFrom(user);
            return Ok(response);
        }

        [Authorize]
        [HttpPut("{id}")]
        public IActionResult Update(string id, [FromBody] UpdateUserRequest model)
        {
            try
            {
                _userService.Update(id, model);
                return Ok();
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Helper methods

        private void setTokenCookie(string token)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTime.UtcNow.AddDays(7)
            };
            Response.Cookies.Append("refreshToken", token, cookieOptions);
        }

        private string ipAddress()
        {
            if (Request.Headers.ContainsKey("X-Forwarded-For"))
                return Request.Headers["X-Forwarded-For"];
            else
                return HttpContext.Connection.RemoteIpAddress.MapToIPv4().ToString();
        }
    }
}
