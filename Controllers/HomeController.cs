using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using Ink.Runtime;
using Quill.Models;

namespace Quill.Controllers
{
    public class HomeController : Controller
    {
        public static readonly string _inkJsonsDirectory = "/AppData/InkJsons/";
        public static readonly string _rawInksDirectory = "/AppData/RawInks/";
        private static readonly string _gameStatesDirectory = "/AppData/GameStates/";
        private static readonly string _permaplaysDirectory = "/Permaplays/";
        private static object _lock = new object();

        // for now at least, errors and warnings are combined into a single collection.
        private List<string> _errorsAndWarnings = new List<string>();

        // _rootPath is a filesystem path, for writing .inks/.jsons. 
        private string _rootPath;
        // _webAppPath is a URL modifier that is used like ~ in IIS. (tilde, ofc, doesn't work with nginx)
        private string _webAppPath;

        private IConfiguration _configuration;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
        {
            _configuration = configuration;
            _logger = logger;

            _rootPath = System.IO.Directory.GetCurrentDirectory();

            _webAppPath = _configuration["WebAppPath"];
        }

        /* 
         * public ViewResult methods
         * 
         */

        public ViewResult Index()
        {
            ViewBag.SessionGuid = Guid.NewGuid();
            ViewBag.WebAppPath = _webAppPath;

            return View();
        }

        public ViewResult PlayOnly(string playId)
        {
            ViewBag.SessionGuid = Guid.NewGuid();
            ViewBag.WebAppPath = _webAppPath;
            ViewBag.PlayId = playId;

            //make sure it's a valid path.
            string inkPath = _rootPath + _permaplaysDirectory + playId + ".json";
            ViewBag.InkFileExists = System.IO.File.Exists(inkPath);

            return View();
        }

        /* 
         * public JsonResult methods
         * 
         */

        public JsonResult ContinueStory(Guid sessionGuid, string playId, int? choiceIndex)
        {
            // if we have a playId, this is a permaplay story, and we load it from permaplays instead of inkJsons.
            string inkJsonPath = string.IsNullOrEmpty(playId)
                ? _rootPath + _inkJsonsDirectory + sessionGuid + ".json"
                : _rootPath + _permaplaysDirectory + playId + ".json";
            string gameStatePath = _rootPath + _gameStatesDirectory + sessionGuid + ".json";

            try
            {
                // if no choices at all, this means we're starting a new story.
                if (!choiceIndex.HasValue) return StartNewStory(inkJsonPath, gameStatePath);

                // there was a choiceIndex selected, which means we're continuing a saved story.
                var story = InkMethods.RestoreStory(inkJsonPath, gameStatePath);

                // much happens in the Ink runtime here.
                story.ChooseChoiceIndex(choiceIndex.Value);

                List<InkOutputMessage> outputs = InkMethods.GetStoryOutputMessages(story);

                InkMethods.SaveStory(gameStatePath, story);

                return base.Json(outputs);
            }
            catch (Exception x)
            {
                string message;

                if (x is StoryException)
                {
                    // we don't need to log story exceptions. we do want to parse the exception message a little.
                    message = GetStoryExceptionMessage((StoryException)x);
                }
                else
                {
                    _logger.LogError(Environment.NewLine + "HANDLED EXCEPTION IN Home/ContinueStory" + Environment.NewLine + x.ToString(), null);

                    message = "Some especially weird error occurred. If you get this message repeatedly, please file an issue at https://github.com/MattConrad/Quill/issues "
                        + $"with a copy of this error text (/Home/ContinueStory {DateTime.Now})";
                }

                CateError[] errors = new CateError[] { new CateError { Message = message, LineNumber = -1 } };
                return base.Json(new { errors });
            }
        }

        public JsonResult GetPermalink(Guid sessionGuid)
        {
            string currentJsonPath = _rootPath + _inkJsonsDirectory + sessionGuid + ".json";

            string permaId;
            lock (_lock)
            {
                long ticks = (DateTime.UtcNow - new DateTime(2016, 01, 01)).Ticks;
                permaId = Helpers.Utils.EncodeTicks(ticks);
                string permaFilename = _rootPath + _permaplaysDirectory + permaId + ".json";

                //not much chance of collision, but if there is one, bump forward a tick until an open slot found.                
                while (System.IO.File.Exists(permaFilename))
                {
                    ticks++;
                    permaId = Helpers.Utils.EncodeTicks(ticks);
                    permaFilename = _rootPath + _permaplaysDirectory + permaId + ".json";
                }

                System.IO.File.Copy(currentJsonPath, permaFilename);
            }

            //http://stackoverflow.com/questions/31617345/what-is-the-asp-net-core-mvc-equivalent-to-request-requesturi
            string[] hostComponents = Request.Host.ToUriComponent().Split(':');
            var builder = new UriBuilder
            {
                Scheme = Request.Scheme,
                Host = hostComponents[0],
                Path = _webAppPath + "play/" + permaId,
            };
            if (hostComponents.Length == 2)
            {
                builder.Port = Convert.ToInt32(hostComponents[1]);
            }

            return base.Json(new { link = builder.Uri });
        }

        public JsonResult PlayInk(string inktext, Guid sessionGuid)
        {
            string newInkPath = _rootPath + _rawInksDirectory + sessionGuid + ".ink";
            string newJsonPath = _rootPath + _inkJsonsDirectory + sessionGuid + ".json";

            // we don't NEED to write the ink to the ink path any more. does cost some resources, could come in handy with troubleshooting.
            System.IO.File.WriteAllText(newInkPath, inktext);

            var compiler = new Ink.Compiler(inktext, new Ink.Compiler.Options { errorHandler = InkErrorHandler });

            var story = compiler.Compile();

            // we should either have errors/warnings, or a working story. (probably can have warnings + story, but we treat warnings as errors in Quill.)
            // in a better world, we would send back story, errors, and warnings as separate object properties, and handle whatever we got, but that would 
            //   take some rethink and rework and i am out of steam on that. for now, anyway.
            if (_errorsAndWarnings.Any())
            {
                return base.Json(new { errors = ParseCateErrorsFromErrorStrings(_errorsAndWarnings) });
            }
            else if (story != null)
            {
                System.IO.File.WriteAllText(newJsonPath, story.ToJson());

                return base.Json(new { });
            }
            else
            {
                throw new InvalidOperationException("Story compilation produced no usable story, and also no errors/warnings. This should never happen. If you get this error more than once, please file an issue at https://github.com/MattConrad/Quill/issues .");
            }
        }


        /* 
         * private JsonResult methods
         * 
         */

        // StartNewStory is only called from within a try {} and does not need special error handling of its own.
        private JsonResult StartNewStory(string inkJsonPath, string gameStatePath)
        {
            var story = Models.InkMethods.LoadEmptyStory(inkJsonPath);

            List<InkOutputMessage> outputs = InkMethods.GetStoryOutputMessages(story);

            InkMethods.SaveStory(gameStatePath, story);

            return base.Json(outputs);
        }

        /* 
         * Ink event handlers
         *  
         */

        private void InkErrorHandler(string message, Ink.ErrorType errorType)
        {
            // ignore errors of type "Author", add both errors and warnings to the error collection.
            if (errorType == Ink.ErrorType.Author) return;

            _errorsAndWarnings.Add(message);
        }

        /* 
         * all other private helper methods
         *  
         */

        // considered removing this and using story.onError, but runtime errors are (i think) rare and I don't the rewriting is warranted. as is for now.
        private static string GetStoryExceptionMessage(StoryException x)
        {
            // i think these exceptions will always have the token text, but i'm not 100% sure. if we don't find the token, send back StoryException.Message entire.
            string token = "error handler to story.onError. The first issue was: ";

            int indexOfToken = x.Message.IndexOf(token);

            return indexOfToken >= 0
                ? x.Message.Substring(indexOfToken + token.Length)
                : x.Message;
        }

        private static List<CateError> ParseCateErrorsFromErrorStrings(IEnumerable<string> errorStrings)
        {
            return errorStrings
                .Select(s => new 
                { 
                    errorString = s.Replace("ERROR: ", "").Replace("WARNING: ", ""), 
                    matches = Regex.Match(s, @": line (\d+):") 
                })
                .Select(n => new 
                { 
                    n.errorString, 
                    lineNumAsString = n.matches.Groups.Count > 1 ? n.matches.Groups[1].Value : "-1" 
                })
                .Select(n => new CateError
                {
                    Message = System.Web.HttpUtility.HtmlEncode(n.errorString),
                    LineNumber = int.Parse(n.lineNumAsString)
                })
                .ToList();
        }
    }
}
