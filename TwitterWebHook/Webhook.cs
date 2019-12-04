using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using IronScheme;
using System.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace TwitterWebHook
{
    public class Webhook
    {
        private readonly string ConsumerKey = "";
        private readonly string ConsumerSecret = "";
        private readonly string AccessToken = "";
        private readonly string AccessTokenSecret = "";

        public Webhook(IConfiguration configuration)
        {
            ConsumerKey = configuration["ConsumerKey"];
            ConsumerSecret = configuration["ConsumerSecret"];
            AccessToken = configuration["AccessToken"];
            AccessTokenSecret = configuration["AccessTokenSecret"];
        }

        [FunctionName("CrcToken")]
        public IActionResult CrcToken(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "twitter-webhook")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("CRC token request");

            string crc_token = req.Query["crc_token"];

            var hashKeyArray = Encoding.UTF8.GetBytes(ConsumerSecret);
            var crcTokenArray = Encoding.UTF8.GetBytes(crc_token);

            using (var hmacSHA256Alog = new HMACSHA256(hashKeyArray))
            {
                var computedHash = hmacSHA256Alog.ComputeHash(crcTokenArray);

                var response = new
                {
                    response_token = $"sha256={Convert.ToBase64String(computedHash)}"
                };

                return new OkObjectResult(response);
            }
        }

        [FunctionName("Eval")]
        public async Task<IActionResult> Eval(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "twitter-webhook")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("eval request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            //log.LogInformation("eval request: " + requestBody);

            dynamic data = JsonConvert.DeserializeObject(requestBody);

            var tweet_create_events = data.tweet_create_events;
            var tweet_create_event = tweet_create_events[0];

            if (tweet_create_event.in_reply_to_screen_name == "IronScheme")
            {
                var text = (string) tweet_create_event.text;
                var from = (string) tweet_create_event.user.screen_name;
                var id = (long) tweet_create_event.id;

                var code = text.Replace("@IronScheme", "", StringComparison.OrdinalIgnoreCase).Trim();

                log.LogInformation("eval from: " + from + " : " + code);

                var result = code.Eval();
                var str_result = "(format \"~a\" {0})".Eval<string>(result);

                var reply = $"@{from} {str_result}";

                Tweetinvi.Auth.SetUserCredentials(ConsumerKey, ConsumerSecret, AccessToken, AccessTokenSecret);
                Tweetinvi.Tweet.PublishTweetInReplyTo(reply, id);
            }

            return new OkResult();
        }
    }
}
