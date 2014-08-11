using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Flurl.Http;
using Microsoft.Ajax.Utilities;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace MilkBot.Controllers
{
    public class SlackController : ApiController
    {
        private const string MilkBotName = "slackbot";

        private const string TokenKey = "token";
        private const string TeamIdKey = "team_id";
        private const string ChannelIdKey = "channel_id";
        private const string ChannelNameKey = "channel_name";
        private const string TimestampKey = "timestamp";
        private const string UserIdKey = "user_id";
        private const string UserNameKey = "user_name";
        private const string TextKey = "text";
        private const string TriggerWordKey = "trigger_word";
        private const string CommandKey = "command";

        private static Random _random = new Random();

        public ConnectionMultiplexer ConnectionMultiplexer { get; set; }
        
        [Route("Slack")]
        [HttpPost]
        public HttpResponseMessage ReceiveSlackOutgoingMessage()
        {
            var requestContent = Request.Content;
            var stringContent = requestContent.ReadAsStringAsync().Result;
            var values = stringContent.Split('&')
                .Select(str => str.Split('='))
                .ToDictionary(arr => arr[0], arr => arr[1]);

            // dont listen to yourself
            if (values[UserNameKey] == MilkBotName) {
                return ConstructResponseMessage(HttpStatusCode.OK);
            }

            var decodedMsg = HttpUtility.HtmlDecode(HttpUtility.UrlDecode(values[TextKey]));
             
            var cache = ConnectionMultiplexer.GetDatabase();
            var markovChainCreator = new MarkovChainCreator(cache);

            markovChainCreator.LoadString(decodedMsg);

            return ConstructResponseMessage(HttpStatusCode.OK);
        }

        [Route("Slack/MilkBotQuote")]
        [HttpPost]
        public async Task<IHttpActionResult> ReceiveSlackSlashCommand()
        {
            var requestContent = Request.Content;
            var stringContent = requestContent.ReadAsStringAsync().Result;
            var values = stringContent.Split('&')
                .Select(str => str.Split('='))
                .ToDictionary(arr => arr[0], arr => arr[1]);

            // dont listen to yourself
            if (values[UserNameKey] == MilkBotName) {
                return Ok();
            }

            if (DecodeString(values[CommandKey]) == "/milkbotquote")
            {
                var cache = ConnectionMultiplexer.GetDatabase();
                var startWord = cache.SetRandomMember("first-words-set").ToString();
                var sentenceWords = new List<string>() {startWord};
                var currentWord = startWord;
                var lastWordOfCurrentWord = currentWord.Split(' ').Last();
                while ((sentenceWords.Count < 5 || !cache.SetContains("last-words-set", lastWordOfCurrentWord)) && cache.KeyExists(currentWord))
                {
                    var nextPossibleWords = cache.ListRange(currentWord).Select(rv => rv.ToString()).ToList();

                    // pick a random word as the next word
                    currentWord = GetRandomWordFromList(nextPossibleWords);
                    sentenceWords.Add(currentWord);

                    var tempCurWord = currentWord;
                    currentWord = String.Format("{0} {1}", lastWordOfCurrentWord, currentWord);
                    lastWordOfCurrentWord = tempCurWord;
                }

                // construct the 'sentence'
                var constructedSentence = String.Join(" ", sentenceWords);
                var responseObject = new {constructedSentence};

                // send message via incoming webhook
                var slackIncomingWebhookUrl = ConfigurationManager.AppSettings["SLACK_WEBHOOK_URL"];
                var sentenceObject = new {text = constructedSentence};
                await slackIncomingWebhookUrl.PostJsonAsync(sentenceObject);

                return Ok(responseObject);
            }

            return Ok();
        }

        private string GetRandomWordFromList(List<string> words)
        {
            var idx = _random.Next(words.Count);
            return words[idx];
        }

        private string GetRandomWordFromListAndRemoveIt(List<string> words)
        {
            var idx = _random.Next(words.Count);
            var word = words[idx];
            words.RemoveAt(idx);
            return word;
        }

        private HttpResponseMessage ConstructResponseMessage(HttpStatusCode statusCode, object bodyObject = null)
        {
            var response = Request.CreateResponse(statusCode);
            
            if (bodyObject != null) {
                var textJson = JsonConvert.SerializeObject(bodyObject);
                response.Content = new StringContent(textJson);
            }

            return response;
        }

        private string DecodeString(string encodedString)
        {
            return HttpUtility.HtmlDecode(HttpUtility.UrlDecode(encodedString));
        }
    }
}
