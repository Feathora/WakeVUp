using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Devices;
using System.Text;

namespace WakeVUpFunctions
{
    public static class SetAlarm
    {
        private const string CONNECTION_STRING = "<connection_string>";
        private const string DEVICE_ID = "<device_id>";

        [FunctionName("PostAlarmData")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            try
            {
                ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(CONNECTION_STRING, TransportType.Amqp);
                await serviceClient.SendAsync(DEVICE_ID, new Message(Encoding.ASCII.GetBytes(requestBody)));
            }
            catch(Exception e)
            {
                log.LogError("Unable to contact Raspberry Pi", e.Message, e.StackTrace);
                return new BadRequestResult();
            }

            return new OkResult();
        }
    }
}
