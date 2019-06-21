﻿using Microsoft.AspNetCore.Mvc;
using Application.Interfaces;
using System;
using Microsoft.AspNetCore.Diagnostics;
using Application.Interfaces.Repositories;

namespace Web.Controllers
{
    public class HomeController : AbstractController
    {
        public readonly IPlanRepository _planRepository;

        public HomeController(ILoggerManager loggerManager, IPlanRepository planRepository) : base(loggerManager)
        {
            _planRepository = planRepository;
        }

        public IActionResult Index()
        {
            _loggerManager.Info("Home.Index was requested");

            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();

            if (feature != null)
            {
                _loggerManager.Error("Uncaught exception was thrown.", feature.Error);
            }

            return View();
        }

    }
}
