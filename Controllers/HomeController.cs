using System;
using System.Collections.Generic;
using Microsoft.AspNet.Mvc;
using System.Diagnostics;
using Quill.Models;
using Microsoft.Extensions.Configuration;

namespace Quill.Controllers
{
    public class HomeController : Controller
    {
        public static readonly string _inkJsonsDirectory = "/AppData/InkJsons/";
        public static readonly string _rawInksDirectory = "/AppData/RawInks/";
        private static readonly string _gameStatesDirectory = "/AppData/GameStates/";

        //_rootPath is a filesystem path, for writing .ink/.jsons. _webAppPath is a URL modifier that is used instead of ~ (tilde, ofc, doesn't work with nginx)
        //  tilde handling is under discussion: https://github.com/aspnet/Announcements/issues/57  doesn't quite look like this is speaking to my issue, though.
        private string _rootPath;
        private string _webAppPath;

        public HomeController(Microsoft.Extensions.PlatformAbstractions.IApplicationEnvironment appEnv, IConfiguration config)
        {
            _rootPath = appEnv.ApplicationBasePath;
            _webAppPath = config["WebAppPath"];
        }
        
        public IActionResult Index()
        {
            ViewBag.SessionGuid = Guid.NewGuid();
            ViewBag.WebAppPath = _webAppPath;
            return View();
        }
        
        public IActionResult ContinueStory(Guid sessionGuid, int? choiceIndex) 
        {
            string inkJsonPath = _rootPath + _inkJsonsDirectory + sessionGuid + ".json";
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
        
        public IActionResult PlayInk(string inktext, Guid sessionGuid)
        {
            try 
            {
                string newInkPath = _rootPath + _rawInksDirectory + sessionGuid + ".ink";
                string newJsonPath = _rootPath + _inkJsonsDirectory + sessionGuid + ".json";
                
                System.IO.File.WriteAllText(newInkPath, inktext);
                
                var processStartInfo = new ProcessStartInfo()
                {
                    Arguments = _rootPath + "/lib/inklecate.dll" + " -o " + newJsonPath + " " + newInkPath,
                    FileName = "dnx",
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
                    
                return Json(new { error = "" });
            }
            catch (Exception x)
            {
                return Json(new { error = x.Message });
            }
        }
        
        //default ink syntax error messages prepend path/file, which is verbose and also useless here.
        private string FixInkMessages(string newInkPath, string message)
        {
            string expectedPrefix = "'" + newInkPath + "' ";
            return message.Replace(expectedPrefix, "");
        }
        
        private IActionResult StartNewStory(string inkJsonPath, string gameStatePath)
        {
            var story = Models.InkMethods.LoadEmptyStory(inkJsonPath);

            List<InkOutputMessage> outputs = InkMethods.GetStoryOutputMessages(story);

            InkMethods.SaveStory(gameStatePath, story);

            return Json(outputs);
        }
        
        
    }
}
