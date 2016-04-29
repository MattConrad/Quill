using System;
using System.Collections.Generic;
using Microsoft.AspNet.Mvc;
using System.Diagnostics;
using Quill.Models;

namespace Quill.Controllers
{
    public class HomeController : Controller
    {
        public static readonly string _inkJsonsDirectory = "/AppData/InkJsons/";
        public static readonly string _rawInksDirectory = "/AppData/RawInks/";
        private static readonly string _gameStatesDirectory = "/AppData/GameStates/";

        private string _rootPath;
        
        public HomeController(Microsoft.Extensions.PlatformAbstractions.IApplicationEnvironment appEnv)
        {
            _rootPath = appEnv.ApplicationBasePath;
        }
        
        public IActionResult Index()
        {
            ViewBag.SessionGuid = Guid.NewGuid();
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
                    Arguments = _rootPath + "/lib/inklecate.exe" + " -o " + newJsonPath + " " + newInkPath,
                    FileName = "dnx",
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };
                
                Process p = new Process();
                p.StartInfo = processStartInfo;
                p.Start();
                p.WaitForExit(3000);
                
                if (p.ExitCode != 0) {
                    throw new InvalidOperationException("Ink processing crashed. No details are available, but invalid .ink syntax is likely.");
                }
                    
                return Json(new { error = "" });
            }
            catch (Exception x)
            {
                return Json(new { error = "Processing failed. Likely cause is invalid syntax. (error text: " + x.Message + ")" });
            }
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
