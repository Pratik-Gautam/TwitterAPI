using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;

namespace TwitterAPI.Controllers
{
    /// <summary>
    /// This is the controller that has handle for getting tweets from all the relevant twitter handles that are currently mentioned in TwitterHandles array
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    public class TwitterController : ControllerBase
    {

        private static readonly string[] TwitterHandles = new[]
        {
        "to:TwitterDev", "to:TwitterAPI", "to:prgaut"
        };

        List<Tweet>? returnedTweets = null;

        //private readonly ILogger<TwitterController> _logger;
        private IConfiguration _configuration;
        public TwitterController(IConfiguration iConfig)
        {
            _configuration = iConfig;
        }

        /// <summary>
        /// GetTweets API endpoint to be called from the client with a valid ICM id
        /// </summary>
        /// <param name="icm"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("GetTweets")]
        public async Task<IActionResult> GetTweets([FromHeader] string icm)
        {
            //setting variables that point to the Twitter handles, query to be triggered and bearer token to call Twitter API
            string twtrHandles = string.Join(" OR ", TwitterHandles);
            string query = "API";
            string bearerToken = _configuration.GetValue<string>("accessToken");
            string? nextToken = null;
            returnedTweets = new List<Tweet>();

            //Condition leveraged whent he QueryTweets function is called for the first time
            if (nextToken == null)
            {
                nextToken = await QueryTweets(twtrHandles, query, bearerToken, nextToken, returnedTweets);
            }

            //While loop to handle pagination and harness subsequent tweets
            while (nextToken != null)
            {
                nextToken = await QueryTweets(twtrHandles, query, bearerToken, nextToken, returnedTweets);
            }

            //successfully returning tweets back to the caller
            if (returnedTweets != null)
            {
                return Ok(returnedTweets);
            }
            //no tweet available in-case an error occurs
            else
            {
                Tweet emptyTweetResult = new Tweet();
                emptyTweetResult.Created_at = "no tweet available";
                emptyTweetResult.Text = "no tweet available";
                returnedTweets.Add(emptyTweetResult);
                return BadRequest();
            }
        }

        /// <summary>
        /// Core function to recursively query twitter for data as per pagination, if applicable
        /// </summary>
        /// <param name="twtrHandles">list of twitter handles that are to be queried</param>
        /// <param name="query">the set of keywords to be harnessed</param>
        /// <param name="bearerToken">bearer token applicable for twitter</param>
        /// <param name="nextToken">token to handle pagination</param>
        /// <param name="returnedTweets">the collection of tweets to be handled</param>
        /// <returns></returns>
        private async Task<string> QueryTweets(string twtrHandles, string query, string bearerToken, string? nextToken,List<Tweet> returnedTweets)
        {
            using (var twitterClient = new HttpClient())
            {
                twitterClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                string twitterUrl = @"https://api.twitter.com/2/tweets/search/recent?query={query} ({handles})&tweet.fields=created_at";
                twitterUrl = twitterUrl.Replace("{handles}", twtrHandles).Replace("{query}", query);

                using (var twitterResponse = await twitterClient.GetAsync(twitterUrl))
                {
                    string apiResponse = await twitterResponse.Content.ReadAsStringAsync();
                    if (apiResponse is not null && apiResponse.ToLower().Contains("data") && apiResponse.ToLower().Contains("meta"))
                    {
                        var tweetData = JObject.Parse(apiResponse).SelectToken("data").ToString();
                        var tweetMetaData = JObject.Parse(apiResponse).SelectToken("meta").ToString();
                        returnedTweets.AddRange(JsonConvert.DeserializeObject<List<Tweet>>(tweetData));
                        nextToken = JsonConvert.DeserializeObject<Meta>(tweetMetaData).Next_token;
                    }
                }
            }

            return nextToken;
        }
    }
}
