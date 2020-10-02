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
    // MWCTODO: when you deploy to linux, be sure to set up upstart and tmpreaper, both, correctly.

    public class HomeController : Controller
    {
        public static readonly string _inkJsonsDirectory = "/AppData/InkJsons/";
        public static readonly string _rawInksDirectory = "/AppData/RawInks/";
        private static readonly string _gameStatesDirectory = "/AppData/GameStates/";
        private static readonly string _permaplaysDirectory = "/Permaplays/";
        private static object _lock = new object();

        //_rootPath is a filesystem path, for writing .ink/.jsons. _webAppPath is a URL modifier that is used instead of ~ (tilde, ofc, doesn't work with nginx)
        //  tilde handling is under discussion: https://github.com/aspnet/Announcements/issues/57  doesn't quite look like this is speaking to my issue, though.
        private string _rootPath;
        private string _libExePath;
        private string _webAppPath;

        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;

            _rootPath = System.IO.Directory.GetCurrentDirectory();

            ////if you aren't running win-x64 or linux-x64 you'll need to alter this.
            //_libExePath = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            //    ? "/lib/linux-x64/cate-netcore"
            //    : "/lib/win-x64/cate-netcore.exe";

            // MWCTODO: this isn't updated for linux. it will need to be.
            //if you aren't running win-x64 or linux-x64 you'll need to alter this.
            _libExePath = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? "/lib/linux-x64/cate-netcore"
                : "/lib/win-x64/inklecate.exe";

            // MWCTODO: this must be restored + fixed somehow, or maybe there's a better approach by now. this is fundamental, you won't even be able to test without it.
            //_webAppPath = config["WebAppPath"];
            _webAppPath = @"/";
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
            lock(_lock)
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
            try
            {
                string newInkPath = _rootPath + _rawInksDirectory + sessionGuid + ".ink";
                string newJsonPath = _rootPath + _inkJsonsDirectory + sessionGuid + ".json";

                System.IO.File.WriteAllText(newInkPath, inktext);

                var processStartInfo = new ProcessStartInfo()
                {
                    Arguments = " -o " + newJsonPath + " " + newInkPath,
                    FileName = _rootPath + _libExePath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };

                Process p = new Process();
                p.StartInfo = processStartInfo;
                p.Start();

                //let's hope any syntax errs are found w/in a second
                System.Threading.Thread.Sleep(1000);
                string errorMessage = p.StandardError.ReadToEnd();
                string outputMessage = p.StandardOutput.ReadToEnd();
                p.WaitForExit(2000);

                if (!string.IsNullOrEmpty(errorMessage)) throw new InvalidOperationException(errorMessage);
                if (!string.IsNullOrEmpty(outputMessage)) throw new InvalidOperationException(outputMessage);
                if (p.ExitCode != 0) throw new InvalidOperationException("Ink processing crashed. No details are available.");

                return base.Json(new { });
            }
            catch (Exception x)
            {
                // we used to try/catch GetInklecateErrors() as well, but we can let the global error handler handle that improbable error scenario now.
                var errors = GetInklecateErrors(x.Message);

                return base.Json(new { errors });
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
         * all other private helper methods
         *  
         */

        private static void AddCateError(string errorMessage, int start, int end, Regex re, ref List<CateError> errs)
        {
            int line = -1;
            string msg = errorMessage.Substring(start, end);
            string lineStr = re.Match(msg).Value;
            if (!string.IsNullOrEmpty(lineStr)) line = int.Parse(lineStr);

            errs.Add(new CateError() { Message = msg, LineNumber = line });
        }
        
        private static List<CateError> GetInklecateErrors(string errorMessage)
        {
            List<CateError> errs = new List<CateError>();

            // if we've been handling warnings as errors all this time, it's probably ok to continue doing so . . .
            string[] lineNumsWithErrors = Regex.Split(errorMessage, @"ERROR: '.*?\.ink' |WARNING: '.*?\.ink' ")
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .ToArray();

            // we're expecting a certain format in the split results: if it doesn't look like we got it, bail out and send back the whole error message as a single error
            if (lineNumsWithErrors.Any(lwe => !Regex.IsMatch(lwe, @"^line \d+?:")))
            {
                errs.Add(new CateError() { Message = errorMessage, LineNumber = -1 });
                return errs;
            }

            // we've got good format, break up the error message into CateErrors with line numbers.
            return lineNumsWithErrors
                .Select(lwe => new { lineInfo = lwe.Split(':')[0], lwe })
                .Select(n => new { lineNumAsString = n.lineInfo.Split(' ')[1], n.lwe })
                .Select(n => new CateError { LineNumber = int.Parse(n.lineNumAsString), Message = n.lwe })
                .ToList();
        }

        private static string GetStoryExceptionMessage(StoryException x)
        {
            // i think these exceptions will always have the token text, but i'm not 100% sure. if we don't find the token, send back StoryException.Message entire.
            string token = "error handler to story.onError. The first issue was: ";

            int indexOfToken = x.Message.IndexOf(token);

            return indexOfToken >= 0
                ? x.Message.Substring(indexOfToken + token.Length)
                : x.Message;
        }

        
    }
}
