using Microsoft.AspNetCore.Mvc;

namespace WebApplication1.Controllers
{
    public class FarmController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
