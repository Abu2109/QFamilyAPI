
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QFamilyAPI.Data;
using QFamilyAPI.Models;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace QFamilyAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly QFamilyDbContext _context;
        private readonly IConfiguration _configuration;

        public UsersController(
            QFamilyDbContext context,
            IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // GET: api/Users
        [Authorize(Roles = "Parent")]
        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            var userId = GetCurrentUserId();

            if (userId == null)
                return Unauthorized();

            var users = await _context.Users
                .AsNoTracking()
                .Where(u =>
                    u.UserId == userId.Value &&
                    u.IsActive)
                .Select(u => new
                {
                    u.UserId,
                    u.Username,
                    u.FullName,
                    u.Email,
                    u.Role,
                    u.IsActive,
                    u.CreatedAt
                })
                .ToListAsync();

            return Ok(users);
        }

        // GET: api/Users/{id}
        [Authorize]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetUser(int id)
        {
            var currentUserId = GetCurrentUserId();

            if (currentUserId == null)
                return Unauthorized();

            if (currentUserId.Value != id)
                return Forbid();

            var user = await _context.Users
                .AsNoTracking()
                .Where(u =>
                    u.UserId == id &&
                    u.IsActive)
                .Select(u => new
                {
                    u.UserId,
                    u.Username,
                    u.FullName,
                    u.Email,
                    u.Role,
                    u.IsActive,
                    u.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return NotFound(new
                {
                    message = "Không tìm thấy tài khoản đang hoạt động."
                });

            return Ok(user);
        }

        // POST: api/Users/register
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register(
            [FromBody] RegisterRequest request)
        {
            var username = request.Username
                .Trim()
                .ToLowerInvariant();

            var email = request.Email
                .Trim()
                .ToLowerInvariant();

            var fullName = request.FullName.Trim();

            if (string.IsNullOrWhiteSpace(fullName))
                return BadRequest(new
                {
                    message = "Họ tên không được để trống."
                });

            if (await _context.Users.AnyAsync(
                u => u.Username == username))
            {
                return Conflict(new
                {
                    message = "Username đã tồn tại."
                });
            }

            if (await _context.Users.AnyAsync(
                u => u.Email == email))
            {
                return Conflict(new
                {
                    message = "Email đã được sử dụng."
                });
            }

            var user = new User
            {
                Username = username,
                FullName = fullName,
                Email = email,
                PasswordHash =
                    BCrypt.Net.BCrypt.HashPassword(
                        request.Password),
                Role = "Parent",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict(new
                {
                    message =
                        "Không thể tạo tài khoản. Kiểm tra Username và Email."
                });
            }

            return CreatedAtAction(
                nameof(GetUser),
                new { id = user.UserId },
                new
                {
                    message = "Đăng ký thành công.",
                    user.UserId,
                    user.Username,
                    user.FullName,
                    user.Email,
                    user.Role,
                    user.IsActive
                });
        }

        // POST: api/Users/login
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequest request)
        {
            var account = request.Username
                .Trim()
                .ToLowerInvariant();

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u =>
                    u.Username == account ||
                    (u.Role == "Parent" &&
                     u.Email == account));

            if (user == null ||
                !user.IsActive ||
                !BCrypt.Net.BCrypt.Verify(
                    request.Password,
                    user.PasswordHash))
            {
                return Unauthorized(new
                {
                    message =
                        "Tài khoản hoặc mật khẩu không đúng, hoặc tài khoản đã bị vô hiệu hóa."
                });
            }

            var token = GenerateJwtToken(user);

            return Ok(new
            {
                message = "Đăng nhập thành công.",
                accessToken = token,
                tokenType = "Bearer",
                expiresIn = GetExpiryMinutes() * 60,
                user = new
                {
                    user.UserId,
                    user.Username,
                    user.FullName,
                    user.Email,
                    user.Role,
                    user.IsActive
                }
            });
        }

        private string GenerateJwtToken(User user)
        {
            var key = _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException(
                    "Chưa cấu hình Jwt:Key.");

            var issuer = _configuration["Jwt:Issuer"]
                ?? throw new InvalidOperationException(
                    "Chưa cấu hình Jwt:Issuer.");

            var audience = _configuration["Jwt:Audience"]
                ?? throw new InvalidOperationException(
                    "Chưa cấu hình Jwt:Audience.");

            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    user.UserId.ToString()),

                new Claim(
                    ClaimTypes.Name,
                    user.Username ?? ""),

                new Claim(
                    ClaimTypes.Role,
                    user.Role),

                new Claim(
                    JwtRegisteredClaimNames.Jti,
                    Guid.NewGuid().ToString())
            };

            var securityKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(key));

            var credentials = new SigningCredentials(
                securityKey,
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(
                    GetExpiryMinutes()),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }

        private int GetExpiryMinutes()
        {
            var value = _configuration.GetValue<int>(
                "Jwt:ExpiryMinutes", 60);

            return value > 0 ? value : 60;
        }

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirst(
                ClaimTypes.NameIdentifier)?.Value;

            return int.TryParse(claim, out var userId)
                ? userId
                : null;
        }
    }

    public class RegisterRequest
    {
        [Required]
        [StringLength(50, MinimumLength = 3)]
        [RegularExpression(@"^[a-zA-Z0-9_]+$")]
        public string Username { get; set; } = "";

        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string FullName { get; set; } = "";

        [Required]
        [EmailAddress]
        [StringLength(150)]
        public string Email { get; set; } = "";

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = "";
    }

    public class LoginRequest
    {
        [Required]
        public string Username { get; set; } = "";

        [Required]
        public string Password { get; set; } = "";
    }
}
