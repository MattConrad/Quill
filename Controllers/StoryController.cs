using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.Mvc;
using Newtonsoft.Json;
using Quill.Models;
using Ink.Runtime;

namespace Quill.Controllers
{
    public class InkController : Controller
    {
        private static readonly string GameStatesDirectory = "/AppData/GameStates/";

        private string _rootPath;
        
        public InkController(Microsoft.Extensions.PlatformAbstractions.IApplicationEnvironment appEnv)      
        {
            _rootPath = appEnv.ApplicationBasePath;
        }
        
        public IActionResult Story(int storyId) 
        {
            string storyTitle = (HomeController.StoryDictionary != null && HomeController.StoryDictionary.ContainsKey(storyId)) ? HomeController.StoryDictionary[storyId] : "(story title unavailable)";

            var startup = new StoryStartupTuple() { StoryId = storyId, StoryTitle = storyTitle, StoryStateGuid = Guid.NewGuid() };

            //would like to create and save story now, this would make ContinueStory() simpler, but trying to save/restore story before any actions are taken crashes.

            return View(startup);
        }
        
        [Produces("application/json")]
        public IActionResult ContinueStory(int storyId, Guid storyStateGuid, int? choiceIndex, string path) 
        {
            if (choiceIndex.HasValue && !string.IsNullOrEmpty(path)) throw new ArgumentException("Cannot have choice index and story path both in combination.");

            //if there is neither choiceIndex nor path, this is a new story. 
            if (!choiceIndex.HasValue && string.IsNullOrEmpty(path)) return StartNewStory(storyId, storyStateGuid);

            //there was a choiceIndex or a path, which means we're continuing a saved story.
            Story story = RestoreStory(storyId, storyStateGuid);

            if (!string.IsNullOrEmpty(path))
            {
                //what happens if this isn't a valid story path?
                story.ChoosePathString(path);
            }
            else if (choiceIndex.HasValue)
            {
                story.ChooseChoiceIndex(choiceIndex.Value);
            }

            List<InkOutputMessage> outputs = GetStoryOutputMessages(story);

            SaveStory(storyStateGuid, story);

            return Json(outputs);
        }
        
        private List<InkOutputMessage> GetStoryOutputMessages(Story story)
        {
            List<InkOutputMessage> outputs = new List<InkOutputMessage>();
            while (story.canContinue)
            {
                outputs.Add(new InkOutputMessage() { MessageType = InkOutputMessageTypes.Text, OutputText = story.Continue() });
            }
            outputs.AddRange(story.currentChoices.Select(c => new InkOutputMessage() { MessageType = InkOutputMessageTypes.Choice, ChoiceIndex = c.index, OutputText = c.text }));

            return outputs;
        }

        private Story LoadEmptyStory(int storyId)
        {
            string storyJson = System.IO.File.ReadAllText(_rootPath + HomeController.InkJsonsDirectory + "story_" + storyId + ".json");

            return new Story(storyJson);
        }

        private Story RestoreStory(int storyId, Guid storyStateGuid)
        {
            var story = LoadEmptyStory(storyId);

            string path = _rootPath + GameStatesDirectory + storyStateGuid.ToString() + ".json";
            string storyState = System.IO.File.ReadAllText(path);

            story.state.LoadJson(storyState);

            return story;
        }

        private void SaveStory(Guid storyStateGuid, Story story)
        {
            string path = _rootPath + GameStatesDirectory + storyStateGuid.ToString() + ".json";
            string storyState = story.state.ToJson();

            //trying to get 0-turn saving to work, but no luck.
            //storyState = storyState.Replace("\"turnIdx\":-1", "\"turnIdx\":0");
            //storyState = storyState.Replace("\"cPath\":\"\"", "\"cXXXPath\":\"\"");

            System.IO.File.WriteAllText(path, storyState);
        }

        [Produces("application/json")]
        private IActionResult StartNewStory(int storyId, Guid storyStateGuid)
        {
            var story = LoadEmptyStory(storyId);

            List<InkOutputMessage> outputs = GetStoryOutputMessages(story);

            SaveStory(storyStateGuid, story);

            return Json(outputs);
        }
        
    }
}
