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
                // get the words
                // also strip the words of quotes, italics, code marks, bolds, etc
                var decodedMsg = DecodeString(values[TextKey]);
                var words = decodedMsg.Split(new char[0], StringSplitOptions.RemoveEmptyEntries).Select(str => str.Trim('_', '"', '“', '”', '`', '*')).ToList();

                var cache = ConnectionMultiplexer.GetDatabase();
                var startWord = "";
                // pick a random word, check if that word exists in the redis brain and try a different word if it doesnt
                var foundValidWord = false;

                if (words.Count == 0) {
                    startWord = cache.KeyRandom();
                    foundValidWord = true;
                }
                while (words.Count > 0) {
                    startWord = GetRandomWordFromListAndRemoveIt(words);

                    if (cache.KeyExists(startWord)) {
                        foundValidWord = true;
                        break;
                    }
                }

                if (!foundValidWord) {
                    // send error message back to user (via response)
                    var textObject = new { text = "None of those words were in my brain. Try with a different set of words." };
                    return Ok(textObject);
                }

                var numWordsInSentence = _random.Next(3, 10);
                var sentenceWords = new List<string>() {startWord};
                var currentWord = startWord;

                while (sentenceWords.Count < numWordsInSentence && cache.KeyExists(currentWord)) {
                    var nextPossibleWords = cache.ListRange(currentWord).Select(rv => rv.ToString()).ToList();

                    // pick a random word as the next word
                    currentWord = GetRandomWordFromList(nextPossibleWords);
                    sentenceWords.Add(currentWord);
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
