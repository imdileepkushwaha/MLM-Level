using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLM_Level.Data;
using MLM_Level.Models;

namespace MLM_Level.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var settings = await _context.MlmSettings.FirstOrDefaultAsync() ?? new MlmSetting();
        var packages = await _context.Packages
            .Where(p => p.IsActive)
            .OrderBy(p => p.Price)
            .Take(3)
            .ToListAsync();

        var starterPrice = packages.FirstOrDefault()?.Price ?? 1000m;

        ViewData["FullWidth"] = true;
        ViewData["LandingPage"] = true;

        return View(new LandingViewModel
        {
            Settings = settings,
            StarterPackagePrice = starterPrice,
            ActivePackages = packages
        });
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
