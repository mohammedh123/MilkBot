using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using StackExchange.Redis;

namespace MilkBot
{
    public interface IMarkovChainCreator
    {
        int LoadString(string text);
        int LoadTextWithSentences(string text);
    }

    public class MarkovChainCreator : IMarkovChainCreator
    {
        private readonly IDatabase _redisDb;

        public MarkovChainCreator(IDatabase redisDb)
        {
            _redisDb = redisDb;
        }

        public int LoadString(string text)
        {            
            // break the message into markov chains of length 2
            // also strip the words of quotes, italics, code marks, bolds, etc
            var words = text.Split(new char[0], StringSplitOptions.RemoveEmptyEntries).Select(str => str.Trim('_', '"', '\'', '“', '”', '`', '*')).ToList();

            if (words.Count < 2) return 0;

            // store the first and last words, for generation later
            var firstWord = String.Format("{0} {1}", words[0], words[1]);
            _redisDb.SetAdd("first-words-set", firstWord);
            _redisDb.SetAdd("last-words-set", words[words.Count-1]);

            for (var i = 0; i < words.Count - 2; i++) {
                var key = String.Format("{0} {1}", words[i], words[i + 1]);
                _redisDb.ListRightPush(key, words[i + 2]);
            }

            return words.Count - 2;
        }

        public int LoadTextWithSentences(string text)
        {
            // split text into 'sentences'
            var sentences = Regex.Split(text, @"(?<=[\.!\?])\s+");
            
            return sentences.Sum(sentence => LoadString(sentence));
        }
    }
}