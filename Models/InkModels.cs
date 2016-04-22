using System;
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

    public class StoryStartupTuple
    {
        public int StoryId { get; set; }
        public string StoryTitle { get; set; }
        public Guid StoryStateGuid { get; set; }
    }
}