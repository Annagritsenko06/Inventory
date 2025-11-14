using Microsoft.AspNetCore.Mvc;
using CourseWork.Models;

namespace CourseWork.Controllers
{
    public class SalesforceController : Controller
    {
        private readonly IConfiguration _configuration;
        public SalesforceController(IConfiguration config)
        {
            _configuration = config;
        }
        [HttpPost]
        public async Task<IActionResult> SyncUser(UserFormViewModel model)
        {
            var sf = new SalesforceJwtAuth(_configuration);
            await sf.CreateAccountAndContactAsync(model);

            return RedirectToAction("Profile", "Users");
        }


    }
    public class UserFormViewModel
    {
        public string CompanyName { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
    }

    
}
