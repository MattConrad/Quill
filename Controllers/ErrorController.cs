using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Diagnostics;
using Karambolo.Extensions.Logging.File;
using Microsoft.Extensions.Logging;

namespace Quill.Controllers
{
    public class ErrorController : Controller
    {
        private readonly ILogger _logger;

        public ErrorController(ILogger<ErrorController> logger)
        {
            _logger = logger;
        }

        public IActionResult HandleError()
        {
            var path = HttpContext.Features.Get<IExceptionHandlerPathFeature>();

            var message = path?.Error?.Message ?? "(failed to retrieve exception message)";
            var pathString = path?.Path ?? "(failed to retrieve exception path)";

            string logMessage = message + Environment.NewLine + Environment.NewLine + Environment.NewLine + "***" + Environment.NewLine;
            _logger.LogError(logMessage, null);

            var request = HttpContext.Request;
            bool isAjax = (request.Headers != null) && (request.Headers["X-Requested-With"] == "XMLHttpRequest");

            if (isAjax)
            {
                // MWCTODO: you probably DON'T want a 500 here, because the client doesn't really expect them, it expects an .errors property on a 200 response.
                return StatusCode(500, new { errors = new[] { message } });
            }
            else
            {
                return Content(message);
            }
        }
    }
}