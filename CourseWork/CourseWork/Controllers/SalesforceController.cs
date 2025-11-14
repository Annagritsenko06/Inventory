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
            try
            {
                await sf.CreateAccountAndContactAsync(model);
                TempData["SuccessMessage"] = "Контакт и компания успешно созданы в Salesforce!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при создании в Salesforce: {ex.Message}";
            }
            return RedirectToAction("Profile", "Users");
        }



    }
    public class UserFormViewModel
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string CompanyName { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }

        public string Title { get; set; }
        public string Department { get; set; }
        public DateTime? Birthdate { get; set; }

        public string AccountNumber { get; set; }     
        public string Website { get; set; }           
        public string Industry { get; set; }          
        public int? Employees { get; set; }           
        public decimal? AnnualRevenue { get; set; }   
           
        public string Rating { get; set; }              
    }



}
