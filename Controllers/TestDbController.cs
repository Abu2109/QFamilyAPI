
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QFamilyAPI.Data;

namespace QFamilyAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestDbController : ControllerBase
    {
        private readonly QFamilyDbContext _context;

        public TestDbController(QFamilyDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            bool connected = await _context.Database.CanConnectAsync();

            if (!connected)
            {
                return StatusCode(503, new
                {
                    message = "Khong ket noi duoc SQL Server"
                });
            }

            return Ok(new
            {
                message = "Ket noi QFamilyDB thanh cong!",
                database = _context.Database.GetDbConnection().Database
            });
        }
    }
}
