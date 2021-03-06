﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Diagnostics;
using Karambolo.Extensions.Logging.File;
using Microsoft.Extensions.Logging;
using Quill.Models;

namespace Quill.Controllers
{
    public class ErrorController : Controller
    {
        private readonly ILogger _logger;

        public ErrorController(ILogger<ErrorController> logger)
        {
            _logger = logger;
        }

        // the actual exception, including stack trace, is automatically logged to the log file. 
        // not sure exactly where this is triggered--i speculate it is considered "unhandled" when it comes here--but anyway we don't need to repeat stack trace here.
        public IActionResult HandleError()
        {
            var path = HttpContext.Features.Get<IExceptionHandlerPathFeature>();

            var message = path?.Error?.Message ?? "(failed to retrieve exception message)";
            var pathString = path?.Path ?? "(failed to retrieve exception path)";

            string logMessage = Environment.NewLine + "***" + Environment.NewLine + pathString + Environment.NewLine + message + Environment.NewLine + "***" + Environment.NewLine;
            _logger.LogError(logMessage, null);

            var request = HttpContext.Request;
            bool isAjax = (request.Headers != null) && (request.Headers["X-Requested-With"] == "XMLHttpRequest");

            if (isAjax)
            {
                // i have to force a 200 here? something upstream appears to be magically causing a 500 if I don't force a 200.
                return StatusCode(200, new { errors = new CateError[] { new CateError { 
                    Message = System.Web.HttpUtility.HtmlEncode(message), 
                    LineNumber = -1 
                } } });
            }
            else
            {
                return Content(message);
            }
        }
    }
}