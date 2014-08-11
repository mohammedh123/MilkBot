using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using StackExchange.Redis;

namespace MilkBot
{
    public interface IMarkovChainCreator
    {
        int LoadString(string text);
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
            // break the message into markov chains of length 1 (for now)
            // also strip the words of quotes, italics, code marks, bolds, etc
            var words = text.Split(new char[0], StringSplitOptions.RemoveEmptyEntries).Select(str => str.Trim('_', '"', '“', '”', '`', '*')).ToList();
            
            for (var i = 0; i < words.Count - 1; i++)
            {
                _redisDb.ListRightPush(words[i], words[i + 1]);
            }

            return words.Count - 1;
        }
    }
}