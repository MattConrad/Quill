using System.Collections.Generic;
using System.Linq;
using Ink.Runtime;
using Newtonsoft.Json;

namespace Quill.Models
{
    public static class InkOutputMessageTypes
    {
        public static string Text = "text";
        public static string Choice = "choice";
        public static string Error = "error";
    }

    public class InkOutputMessage
    {
        //we could instead use a contract resolver, but I don't expect a lot of xfer classes. http://james.newtonking.com/archive/2013/05/08/json-net-5-0-release-5-defaultsettings-and-extension-data
        [JsonProperty(PropertyName = "messageType")]
        public string MessageType { get; set; }
        [JsonProperty(PropertyName = "choiceIndex")]
        public int ChoiceIndex { get; set; }
        [JsonProperty(PropertyName = "outputText")]
        public string OutputText { get; set; }
        [JsonProperty(PropertyName = "instructions")]
        public string[] Instructions { get; set; }
    }
    
    public static class InkMethods
    {
        public static List<InkOutputMessage> GetStoryOutputMessages(Story story)
        {
            List<InkOutputMessage> outputs = new List<InkOutputMessage>();
            while (story.canContinue)
            {
                outputs.Add(new InkOutputMessage() { MessageType = InkOutputMessageTypes.Text, OutputText = story.Continue() });
            }
            outputs.AddRange(story.currentChoices.Select(c => new InkOutputMessage() { MessageType = InkOutputMessageTypes.Choice, ChoiceIndex = c.index, OutputText = c.text }));

            return outputs;
        }
        
        public static Story LoadEmptyStory(string inkJsonPath)
        {
            string storyJson = System.IO.File.ReadAllText(inkJsonPath);

            return new Story(storyJson);
        }

        public static Story RestoreStory(string inkJsonPath, string gameStatePath)
        {
            var story = LoadEmptyStory(inkJsonPath);

            string storyState = System.IO.File.ReadAllText(gameStatePath);

            story.state.LoadJson(storyState);

            return story;
        }

        public static void SaveStory(string gameStatePath, Story story)
        {
            string storyState = story.state.ToJson();

            //trying to get 0-turn saving to work, but no luck.
            //storyState = storyState.Replace("\"turnIdx\":-1", "\"turnIdx\":0");
            //storyState = storyState.Replace("\"cPath\":\"\"", "\"cXXXPath\":\"\"");

            System.IO.File.WriteAllText(gameStatePath, storyState);
        }
        
        
    }
    
}