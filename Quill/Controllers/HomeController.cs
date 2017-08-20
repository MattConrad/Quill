using System;
using System.Collections.Generic;
using System.Diagnostics;
using QuillNetCore.Models;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace QuillNetCore.Controllers
{
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
        private string _webAppPath;

        public HomeController(IConfigurationRoot config)
        {
            _rootPath = System.IO.Directory.GetCurrentDirectory();
            _webAppPath = config["WebAppPath"];
        }
        
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

        public JsonResult ContinueStory(Guid sessionGuid, string playId, int? choiceIndex) 
        {
            //if we have a playId, this is a permaplay story, and we load it from permaplays insetad of inkJsons.
            string inkJsonPath = string.IsNullOrEmpty(playId)
                ? _rootPath + _inkJsonsDirectory + sessionGuid + ".json"
                : _rootPath + _permaplaysDirectory + playId + ".json";
            string gameStatePath = _rootPath + _gameStatesDirectory + sessionGuid + ".json";
            
            //if no choices at all, this means we're starting a new story.
            if (!choiceIndex.HasValue) return StartNewStory(inkJsonPath, gameStatePath);

            //there was a choiceIndex selected, which means we're continuing a saved story.
            var story = InkMethods.RestoreStory(inkJsonPath, gameStatePath);

            //much happens in the Ink runtime here.
            story.ChooseChoiceIndex(choiceIndex.Value);

            List<InkOutputMessage> outputs = InkMethods.GetStoryOutputMessages(story);

            InkMethods.SaveStory(gameStatePath, story);

            return Json(outputs);
        }
        
        private JsonResult StartNewStory(string inkJsonPath, string gameStatePath)
        {
            var story = Models.InkMethods.LoadEmptyStory(inkJsonPath);

            List<InkOutputMessage> outputs = InkMethods.GetStoryOutputMessages(story);

            InkMethods.SaveStory(gameStatePath, story);

            return Json(outputs);
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

            return Json(new { link = builder.Uri });
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
                    FileName = _rootPath + "/lib/inklecate.exe",
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
                
                if (!string.IsNullOrEmpty(errorMessage)) throw new InvalidOperationException(FixInkMessages(newInkPath, errorMessage));
                if (!string.IsNullOrEmpty(outputMessage)) throw new InvalidOperationException(FixInkMessages(newInkPath, outputMessage));
                if (p.ExitCode != 0) throw new InvalidOperationException("Ink processing crashed. No details are available.");
                    
                return Json(new { errors = new string[]{} });
            }
            catch (Exception x)
            {
                try 
                {
                    var errors = GetInklecateErrors(x.Message); 
                    return Json(new { errors = errors });
                }
                //MWCTODO: this means GetInklecateErrors() threw a new exception, should also write to the internal log (figure out the RC2 way of doing this)
                catch
                {
                    var error = new CateError() { Message = x.Message, LineNumber = 0 };
                    return Json(new { errors = new List<CateError>() { error } });
                }
            }
        }
        
        //default ink syntax error messages prepend path/file, which is verbose and also useless here.
        private string FixInkMessages(string newInkPath, string message)
        {
            string expectedPrefix = "'" + newInkPath + "' ";
            return message.Replace(expectedPrefix, "");
        }
        
        private List<CateError> GetInklecateErrors(string errorMessage)
        {
            List<CateError> errs = new List<CateError>();
            
            string target = "ERROR: line";
            int current = errorMessage.IndexOf(target);
            //we expect an error set that starts with the target, if we get something weird, send the original message with no parsing.
            if (current != 0)
            {
                errs.Add(new CateError() { Message = errorMessage, LineNumber = -1 });
                return errs;
            }
            
            //this regex isn't very fancy, but (so far) InkleCate err format is simple & predictable and it works.
            Regex reLineNum = new Regex(@"\d+");
            int next = 0;
            int bailout = 0;
            while(bailout < 100)
            {
                next = errorMessage.IndexOf(target, current + 1);
                if (next < 0) break;
                
                AddCateError(errorMessage, current, next - current, reLineNum, ref errs);                

                current = next;
                bailout++;
            }
            
            AddCateError(errorMessage, current, errorMessage.Length - current, reLineNum, ref errs);
            
            return errs;
        }
        
        private void AddCateError(string errorMessage, int start, int end, Regex re, ref List<CateError> errs)
        {
            int line = -1;
            string msg = errorMessage.Substring(start, end);
            string lineStr = re.Match(msg).Value;
            if (!string.IsNullOrEmpty(lineStr)) line = int.Parse(lineStr);
           
           errs.Add(new CateError() { Message = msg, LineNumber = line }); 
        }
        
    }
}
