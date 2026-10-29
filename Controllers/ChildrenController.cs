
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QFamilyAPI.Data;
using QFamilyAPI.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace QFamilyAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ChildrenController : ControllerBase
    {
        private readonly QFamilyDbContext _context;
        private readonly ILogger<ChildrenController> _logger;

        public ChildrenController(
            QFamilyDbContext context,
            ILogger<ChildrenController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ==========================================
        // LAY USER ID TU JWT
        // ==========================================

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirst(
                ClaimTypes.NameIdentifier)?.Value;

            return int.TryParse(claim, out var id)
                ? id
                : null;
        }

        // ==========================================
        // KIEM TRA NGAY SINH
        // ==========================================

        private static bool IsValidBirthDate(
            DateOnly? birthDate)
        {
            return !birthDate.HasValue ||
                   birthDate.Value <=
                   DateOnly.FromDateTime(DateTime.UtcNow);
        }

        // ==========================================
        // GET: api/Children
        // Parent xem danh sach con cua minh
        // ==========================================

        [Authorize(Roles = "Parent")]
        [HttpGet]
        public async Task<IActionResult> GetChildren()
        {
            var parentId = GetCurrentUserId();

            if (parentId == null)
                return Unauthorized();

            var children = await _context.Children
                .AsNoTracking()
                .Where(c => c.ParentId == parentId.Value)
                .Select(c => new
                {
                    c.ChildId,
                    c.FullName,
                    c.BirthDate,
                    c.ParentId,
                    c.UserId,
                    Username = c.User != null
                        ? c.User.Username
                        : null,
                    IsActive = c.User != null
                        ? c.User.IsActive
                        : false
                })
                .OrderBy(c => c.ChildId)
                .ToListAsync();

            return Ok(children);
        }

        // ==========================================
        // GET: api/Children/{id}
        // Parent xem con cua minh
        // Child chi xem ho so cua minh
        // ==========================================

        [Authorize(Roles = "Parent,Child")]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetChild(int id)
        {
            var currentUserId = GetCurrentUserId();

            if (currentUserId == null)
                return Unauthorized();

            bool isParent = User.IsInRole("Parent");
            bool isChild = User.IsInRole("Child");

            var child = await _context.Children
                .AsNoTracking()
                .Where(c => c.ChildId == id)
                .Where(c =>
                    (isParent &&
                     c.ParentId == currentUserId.Value)
                    ||
                    (isChild &&
                     c.UserId == currentUserId.Value))
                .Select(c => new
                {
                    c.ChildId,
                    c.FullName,
                    c.BirthDate,
                    c.ParentId,
                    c.UserId,
                    Username = c.User != null
                        ? c.User.Username
                        : null,
                    IsActive = c.User != null
                        ? c.User.IsActive
                        : false
                })
                .FirstOrDefaultAsync();

            if (child == null)
            {
                return NotFound(new
                {
                    message =
                        "Không tìm thấy hồ sơ hoặc không có quyền truy cập."
                });
            }

            return Ok(child);
        }

        // ==========================================
        // POST: api/Children
        // Parent tao tai khoan tre
        // ==========================================

        [Authorize(Roles = "Parent")]
        [HttpPost]
        public async Task<IActionResult> CreateChild(
            [FromBody] CreateChildRequest request)
        {
            var parentId = GetCurrentUserId();

            if (parentId == null)
                return Unauthorized();

            var username = request.Username
                .Trim()
                .ToLowerInvariant();

            var fullName = request.FullName.Trim();

            if (string.IsNullOrWhiteSpace(fullName))
            {
                return BadRequest(new
                {
                    message = "Họ tên không được để trống."
                });
            }

            if (!IsValidBirthDate(request.BirthDate))
            {
                return BadRequest(new
                {
                    message =
                        "Ngày sinh không được lớn hơn ngày hiện tại."
                });
            }

            var parentExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u =>
                    u.UserId == parentId.Value &&
                    u.Role == "Parent" &&
                    u.IsActive);

            if (!parentExists)
                return Forbid();

            var usernameExists = await _context.Users
                .AnyAsync(u => u.Username == username);

            if (usernameExists)
            {
                return Conflict(new
                {
                    message = "Username đã tồn tại."
                });
            }

            var user = new User
            {
                Username = username,
                FullName = fullName,
                Email = null,
                PasswordHash =
                    BCrypt.Net.BCrypt.HashPassword(
                        request.Password),
                Role = "Child",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var child = new Child
            {
                ParentId = parentId.Value,
                FullName = fullName,
                BirthDate = request.BirthDate,
                CreatedAt = DateTime.UtcNow,
                User = user
            };

            _context.Children.Add(child);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "Loi database khi tao tai khoan tre.");

                return Conflict(new
                {
                    message =
                        "Không thể tạo tài khoản trẻ. Vui lòng kiểm tra dữ liệu."
                });
            }

            return CreatedAtAction(
                nameof(GetChild),
                new { id = child.ChildId },
                new
                {
                    message = "Tạo tài khoản trẻ thành công.",
                    child.ChildId,
                    child.ParentId,
                    child.UserId,
                    user.Username,
                    child.FullName,
                    child.BirthDate,
                    user.IsActive
                });
        }

        // ==========================================
        // PUT: api/Children/{id}
        // Parent cap nhat thong tin con
        // ==========================================

        [Authorize(Roles = "Parent")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateChild(
            int id,
            [FromBody] UpdateChildRequest request)
        {
            var parentId = GetCurrentUserId();

            if (parentId == null)
                return Unauthorized();

            var child = await _context.Children
                .Include(c => c.User)
                .FirstOrDefaultAsync(c =>
                    c.ChildId == id &&
                    c.ParentId == parentId.Value);

            if (child == null)
            {
                return NotFound(new
                {
                    message =
                        "Không tìm thấy trẻ hoặc không có quyền cập nhật."
                });
            }

            if (child.User == null)
            {
                return Conflict(new
                {
                    message =
                        "Hồ sơ trẻ chưa liên kết tài khoản đăng nhập."
                });
            }

            if (!child.User.IsActive)
            {
                return Conflict(new
                {
                    message =
                        "Tài khoản đã bị vô hiệu hóa. Hãy khôi phục trước."
                });
            }

            var fullName = request.FullName.Trim();

            if (string.IsNullOrWhiteSpace(fullName))
            {
                return BadRequest(new
                {
                    message = "Họ tên không được để trống."
                });
            }

            if (!IsValidBirthDate(request.BirthDate))
            {
                return BadRequest(new
                {
                    message =
                        "Ngày sinh không được lớn hơn ngày hiện tại."
                });
            }

            child.FullName = fullName;
            child.BirthDate = request.BirthDate;

            // Dong bo ho ten voi tai khoan dang nhap
            child.User.FullName = fullName;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "Loi database khi cap nhat tre {ChildId}.",
                    id);

                return StatusCode(500, new
                {
                    message =
                        "Không thể cập nhật thông tin trẻ."
                });
            }

            return Ok(new
            {
                message = "Cập nhật thông tin trẻ thành công.",
                child.ChildId,
                child.ParentId,
                child.UserId,
                child.FullName,
                child.BirthDate,
                child.User.Username,
                child.User.IsActive
            });
        }

        // ==========================================
        // DELETE: api/Children/{id}
        // VO HIEU HOA, KHONG XOA DU LIEU
        // ==========================================

        [Authorize(Roles = "Parent")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DisableChild(int id)
        {
            var parentId = GetCurrentUserId();

            if (parentId == null)
                return Unauthorized();

            var child = await _context.Children
                .Include(c => c.User)
                .FirstOrDefaultAsync(c =>
                    c.ChildId == id &&
                    c.ParentId == parentId.Value);

            if (child == null)
            {
                return NotFound(new
                {
                    message =
                        "Không tìm thấy trẻ hoặc không có quyền truy cập."
                });
            }

            if (child.User == null)
            {
                return Conflict(new
                {
                    message =
                        "Hồ sơ trẻ chưa liên kết tài khoản đăng nhập."
                });
            }

            if (!child.User.IsActive)
            {
                return Conflict(new
                {
                    message =
                        "Tài khoản trẻ đã được vô hiệu hóa trước đó."
                });
            }

            child.User.IsActive = false;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "Loi khi vo hieu hoa tre {ChildId}.",
                    id);

                return StatusCode(500, new
                {
                    message =
                        "Không thể vô hiệu hóa tài khoản trẻ."
                });
            }

            return Ok(new
            {
                message =
                    "Vô hiệu hóa tài khoản trẻ thành công. Dữ liệu vẫn được giữ lại.",
                child.ChildId,
                child.UserId,
                child.User.Username,
                child.User.IsActive
            });
        }

        // ==========================================
        // PUT: api/Children/{id}/restore
        // KHOI PHUC TAI KHOAN TRE
        // ==========================================

        [Authorize(Roles = "Parent")]
        [HttpPut("{id:int}/restore")]
        public async Task<IActionResult> RestoreChild(int id)
        {
            var parentId = GetCurrentUserId();

            if (parentId == null)
                return Unauthorized();

            var child = await _context.Children
                .Include(c => c.User)
                .FirstOrDefaultAsync(c =>
                    c.ChildId == id &&
                    c.ParentId == parentId.Value);

            if (child == null)
            {
                return NotFound(new
                {
                    message =
                        "Không tìm thấy trẻ hoặc không có quyền khôi phục."
                });
            }

            if (child.User == null)
            {
                return Conflict(new
                {
                    message =
                        "Hồ sơ trẻ chưa liên kết tài khoản đăng nhập."
                });
            }

            if (child.User.IsActive)
            {
                return Conflict(new
                {
                    message =
                        "Tài khoản trẻ đang hoạt động, không cần khôi phục."
                });
            }

            child.User.IsActive = true;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "Loi khi khoi phuc tre {ChildId}.",
                    id);

                return StatusCode(500, new
                {
                    message =
                        "Không thể khôi phục tài khoản trẻ."
                });
            }

            return Ok(new
            {
                message = "Khôi phục tài khoản trẻ thành công.",
                child.ChildId,
                child.UserId,
                child.User.Username,
                child.User.IsActive
            });
        }
    }

    // ==========================================
    // REQUEST TAO TAI KHOAN TRE
    // ==========================================

    public class CreateChildRequest
    {
        [Required]
        [StringLength(50, MinimumLength = 3)]
        [RegularExpression(@"^[a-zA-Z0-9_]+$")]
        public string Username { get; set; } = "";

        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string FullName { get; set; } = "";

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = "";

        public DateOnly? BirthDate { get; set; }
    }

    // ==========================================
    // REQUEST CAP NHAT THONG TIN TRE
    // ==========================================

    public class UpdateChildRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string FullName { get; set; } = "";

        public DateOnly? BirthDate { get; set; }
    }
}
