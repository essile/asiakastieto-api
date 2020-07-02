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

            var checksum = FormatChecksum(userId, endUser, timestamp, checkSumkey, log);
            log.LogInformation("Checksum: " + checksum);

            // Fetch data from api

            string demoUrl = "https://demo.asiakastieto.fi/services/company5/REST";
            string prodUrl = "https://www.asiakastieto.fi/services/company5/REST";

            return new OkObjectResult($"hash: {checksum}");
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
            string dateFormat = "yyyyMMddHHmmssff";
            var timestamp = DateTime.UtcNow.ToString(dateFormat); // YYYYMMDDHHMMSSXX
            string offset = "+02"; //time zone correction in relation to GMT. In Finland always "+02". Daylight saving time is not added.
            string consecutiveNumber = "00000"; // from Asiakastieto, this should work in most cases

            return timestamp + offset + consecutiveNumber;
        }

        private static object FormatChecksum(string userId, string endUser, string asiakastietoTimestamp, string checkSumkey, ILogger log)
        {

            string checksumString = $"{userId}&{endUser}&{asiakastietoTimestamp}&{checkSumkey}&";
            log.LogInformation("Checksum string: " + checksumString);

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
