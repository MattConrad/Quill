using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Diagnostics;

namespace Quill.Controllers
{
    public class ErrorController : Controller
    {
        public IActionResult HandleError()
        {
            var path = HttpContext.Features.Get<IExceptionHandlerPathFeature>();

            // MWCTODO: returning message is all very well, but we probably want to log the whole stack trace + "path"

            // https://github.com/nreco/logging

            var message = path?.Error?.Message ?? "(failed to retrieve exception message)";
            var pathString = path?.Path ?? "(failed to retrieve exception path)";

            var request = HttpContext.Request;
            bool isAjax = (request.Headers != null) && (request.Headers["X-Requested-With"] == "XMLHttpRequest");

            if (isAjax)
            {
                return StatusCode(500, new { errors = new[] { message } });
            }
            else
            {
                return Content(message);
            }
        }
    }
}