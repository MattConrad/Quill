using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.Mvc;

namespace Quill.Controllers
{
    public class HomeController : Controller
    {
        public static readonly string InkJsonsDirectory = "/AppData/InkJsons/";
        public static Dictionary<int, string> StoryDictionary = null;

        private string _rootPath;
        
        public HomeController(Microsoft.Extensions.PlatformAbstractions.IApplicationEnvironment appEnv)
        {
            _rootPath = appEnv.ApplicationBasePath;
        }
        
        public IActionResult Index()
        {
            return View();
        }
        
        public IActionResult OrigIndex()
        {
            if (StoryDictionary == null) InitStoryDict();

            if (StoryDictionary.Count == 0) throw new InvalidOperationException("MWCTODO: invite them to init the application");

            return View(StoryDictionary);
        }
        
        private void InitStoryDict()
        {
            StoryDictionary = System.IO.File.ReadAllText(_rootPath + InkJsonsDirectory + "index.txt")
                .Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Split('~'))
                .ToDictionary(k => int.Parse(k[0]), v => v[1]);
        }
        
        
    }
}
