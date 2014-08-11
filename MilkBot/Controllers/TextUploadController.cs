using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using StackExchange.Redis;

namespace MilkBot.Controllers
{
    public class TextUploadController : ApiController
    {
        public ConnectionMultiplexer ConnectionMultiplexer { get; set; }

        [Route("TextUpload/Upload")]
        [HttpPost]
        public async Task<IHttpActionResult> Upload()
        {
            if (!Request.Content.IsMimeMultipartContent())
                throw new Exception(); // divided by zero

            var provider = new MultipartMemoryStreamProvider();
            await Request.Content.ReadAsMultipartAsync(provider);

            foreach (var file in provider.Contents)
            {
                var filename = file.Headers.ContentDisposition.FileName.Trim('\"');

                if (file.Headers.ContentType.MediaType != MediaTypeNames.Text.Plain) {
                    return BadRequest("Invalid file type. Only plain-text files are allowed.");
                }
                var buffer = await file.ReadAsStringAsync();

                var cache = ConnectionMultiplexer.GetDatabase();
                var markovChainCreator = new MarkovChainCreator(cache);
                var numWordsAdded = markovChainCreator.LoadString(buffer);
                return Ok(numWordsAdded);
            }

            return Ok();
        }
    }
}
