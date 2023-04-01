
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.IO;

namespace vg.docusign.handler
{
    public static class DocuSignConnectFunction
    {
        private const string dsHeaderName = "X-DocuSign-Signature-1";
        private static string DS_CONNECT_KEY = Environment.GetEnvironmentVariable("DS_CONNECT_KEY");

        [FunctionName("DocuSignConnectFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            // Set up the HMAC key
            byte[] keyBytes = Encoding.UTF8.GetBytes(DS_CONNECT_KEY);
            
            // Verify the HMAC token
            string receivedHmac = req.Headers[dsHeaderName];
            if (String.IsNullOrEmpty(receivedHmac))
            {
                log.LogWarning($"Header {dsHeaderName} is missing");
                return new UnauthorizedResult();
            }

            log.LogInformation($"After {dsHeaderName} key: {receivedHmac}");
            byte[] receivedHmacBytes = Convert.FromBase64String(receivedHmac);

            byte[] dataBytes = new byte[req.ContentLength ?? 0];
            await req.Body.ReadAsync(dataBytes, 0, dataBytes.Length);
            byte[] computedHmacBytes;
            using (HMACSHA256 hmac = new HMACSHA256(keyBytes))
            {
                computedHmacBytes = hmac.ComputeHash(dataBytes);
            }

            if (!HmacEquals(receivedHmacBytes, computedHmacBytes))
            {
                log.LogWarning("HMAC comparison failed");
                return new UnauthorizedResult();
            }

            // Read the JSON payload
            string json = System.Text.Encoding.UTF8.GetString(dataBytes);
            log.LogInformation($"Received JSON: {json}");
            var payload = JsonConvert.DeserializeObject<EnvelopeInformation>(json);

            // some action here (update DB etc.)

            return new OkResult();
        }
        
        /*
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {name}. This HTTP triggered function executed successfully.";

            return new OkObjectResult(responseMessage);
        }
        */

        private static bool HmacEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }
            int result = 0;
            for (int i = 0; i < a.Length; i++)
            {
                result |= a[i] ^ b[i];
            }
            return result == 0;
        }
    }

    public class EnvelopeInformation
    {
        public string envelopeId { get; set; }
        public string status { get; set; }
        public string statusChangedDateTime { get; set; }
        public string recipientStatuses { get; set; }
    }
}
