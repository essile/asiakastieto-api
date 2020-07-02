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
            string searchTerm = ValidateHeader("SearchTerm", req, headerError);
            string mode = ValidateHeader("Mode", req, headerError); // demo (by default) or prod
            string language = ValidateHeader("Language", req, headerError); // FI for Finnish, EN for English (default), SV for Swedish
            string endUser = "ccccc";

            if (headerError.Count > 0)
                return new ObjectResult("ERROR: " + String.Join(" ", headerError.ToArray())) { StatusCode = 400 };

            // Checksum

            string timestamp = FormatAsiakastietoTimeStamp();
            string checksum = FormatChecksum(userId, endUser, timestamp, checkSumkey);

            // Fetch data from api

            string url = "https://demo.asiakastieto.fi/services/company5/REST";
            string target = "TAP1";

            if (mode == "prod")
            {
                url = "https://www.asiakastieto.fi/services/company5/REST";
                target = "PAP1";
            }

            var queryParameters =
                $"userid={userId}&" +
                $"passwd={password}&" +
                $"timestamp={Uri.EscapeDataString(timestamp)}&" +
                $"checksum={checksum}&" +
                "version=5.01&" +
                $"enduser={endUser}&" +
                "reqmsg=COMPANY&" + // always COMPANY
                "format=xml&" + // always xml
                $"target={target}&" + // TAP1 for demo, PAP1 for production
                $"lang={language}"; // FI for Finnish, EN for English, SV for Swedish

            var searchParameters =
                "segment=A&" + // Always A
                "qtype=01&" + // Always 01
                "request=N&" + // Always N
                $"name={searchTerm}"; // Search term. Company name "Asiakastieto" or part of the company name "Asiakast"

            var queryString = $"{url}?{queryParameters}&{searchParameters}";

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
                return new ObjectResult(webException.Message) { StatusCode = 500 };
            }

            return new ContentResult { Content = response, ContentType = "application/xml" };
        }


        private static string ValidateHeader(string headerKey, HttpRequest req, List<string> headerError)
        {
            if (req.Headers.TryGetValue(headerKey, out var headerValue))
                return headerValue.FirstOrDefault();
            else
            {
                if (headerKey == "Mode")
                    return "demo"; // Demo by default.

                if (headerKey == "Language")
                    return "EN"; // By default products should be collected starting from 0.

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
            string offset = "+02"; // Time zone correction in relation to GMT. In Finland always "+02". Daylight saving time is not added.
            string consecutiveNumber = "00000"; // from Asiakastieto, this should work in most cases

            return timestamp + offset + consecutiveNumber;
        }

        private static string FormatChecksum(string userId, string endUser, string asiakastietoTimestamp, string checkSumkey)
        {

            string checksumString = $"{userId}&{endUser}&{asiakastietoTimestamp}&{checkSumkey}&";

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
