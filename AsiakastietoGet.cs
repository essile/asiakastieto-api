using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Net;
using System.IO;

namespace AsiakastietoApi
{
    public static class AsiakastietoGet
    {
        [FunctionName("AsiakastietoGet")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            List<string> headerError = new List<string>();
            string userId = ValidateHeader("UserId", req, headerError);
            string password = ValidateHeader("Password", req, headerError);
            string checkSumkey = ValidateHeader("CheckSumKey", req, headerError);
            string endUser = "ccccc";

            if (headerError.Count > 0)
                return new ObjectResult("ERROR: " + String.Join(" ", headerError.ToArray())) { StatusCode = 400 };

            // Checksum

            string timestamp = FormatAsiakastietoTimeStamp();
            string checksum = FormatChecksum(userId, endUser, timestamp, checkSumkey, log);
            //log.LogInformation("Checksum: " + checksum);

            // Fetch data from api

            string demoUrl = "https://demo.asiakastieto.fi/services/company5/REST";
            string demoTarget = "TAP1";
            string prodUrl = "https://www.asiakastieto.fi/services/company5/REST";
            string prodTarget = "PAP1";

            string language = "FI"; // FI for Finnish, EN for English, SV for Swedish

            var queryParameters =
                $"userid={userId}&" +
                $"passwd={password}&" +
                $"timestamp={Uri.EscapeDataString(timestamp)}&" +
                $"checksum={checksum}&" +
                $"version=5.01&" +
                $"enduser={endUser}&" +
                $"reqmsg=COMPANY&" + // always COMPANY
                $"format=xml&" + // always xml
                $"target={demoTarget}&" + // TAP1 for demo, PAP1 for production
                $"lang={language}"; // FI for Finnish, EN for English, SV for Swedish

            var searchParameters =
                "segment=A&" + // Always A
                "qtype=01&" + // Always 01
                "request=N&" + // Always N
                "name=asiakastieto"; // Search term. Company name "Asiakastieto" or part of the company name "Asiakast"

            var queryString = $"{demoUrl}?{queryParameters}&{searchParameters}";

            var httpWebRequest = (HttpWebRequest)WebRequest.Create(queryString);
            httpWebRequest.Method = "GET";

            var response = "";
            try
            {
                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    response = streamReader.ReadToEnd();
                }
            }
            catch (WebException webException)
            {
                return new ObjectResult(webException.Message) { StatusCode = 401 };
            }

            return new OkObjectResult(response);
        }


        private static string ValidateHeader(string headerKey, HttpRequest req, List<string> headerError)
        {
            if (req.Headers.TryGetValue(headerKey, out var headerValue))
                return headerValue.FirstOrDefault();
            else
            {
                headerError.Add($"Header '{headerKey}' missing.");
                return "";
            }
        }
        private static string FormatAsiakastietoTimeStamp()
        {
            string dateFormat = "yyyyMMddHHmmssff"; // As Asiakastieto wants it
            TimeZoneInfo finTime = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
            DateTimeOffset finTimeNow = TimeZoneInfo.ConvertTime(DateTime.UtcNow, finTime);
            var timestamp = finTimeNow.ToString(dateFormat);
            string offset = "+02"; //time zone correction in relation to GMT. In Finland always "+02". Daylight saving time is not added.
            string consecutiveNumber = "00000"; // from Asiakastieto, this should work in most cases

            return timestamp + offset + consecutiveNumber;
        }

        private static string FormatChecksum(string userId, string endUser, string asiakastietoTimestamp, string checkSumkey, ILogger log)
        {

            string checksumString = $"{userId}&{endUser}&{asiakastietoTimestamp}&{checkSumkey}&";
            //log.LogInformation("Checksum string: " + checksumString);

            // checksumStringExample = "123456123456&ccccc&2020010111000000+0200000&9Gk487z6qBC48R27hpq6RBPoS1hWt88Z755Ku7ub5M5NE08HRj2Mt7KOQhtL0spr&";
            // The one above should produce checksum 442B66F745DE4CAF0A1E6DC551C9C676205498C7CDF28036DB2229573A12D71C14F13430A1E34D6B4CAF1360E9573931019A7DACB27178D5998B97F4301D54EE

            using (SHA512 shaM = new SHA512Managed())
            {
                var hash = shaM.ComputeHash(Encoding.UTF8.GetBytes(checksumString));
                return GetStringFromHash(hash);
            }
        }

        private static string GetStringFromHash(byte[] hash)
        {
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                result.Append(hash[i].ToString("X2"));
            }
            return result.ToString();
        }
    }
}
