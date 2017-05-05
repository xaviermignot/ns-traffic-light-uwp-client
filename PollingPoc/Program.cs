using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.System.Threading;

namespace PollingPoc
{
    class Program
    {
        static void Main(string[] args)
        {
            PollApi().GetAwaiter().GetResult();
        }

        private static async Task PollApi()
        {
            while (true)
            {
                using (var httpClient = new HttpClient())
                {
                    var getResult = await httpClient.GetAsync("https://ns-traffic-light-api.azurewebsites.net/api/trafficlight");
                    if (getResult.IsSuccessStatusCode)
                    {
                        var lightValue = await getResult.Content.ReadAsStringAsync();
                        switch (lightValue)
                        {
                            case "Green":
                                Console.WriteLine("Le feu est vert");
                                break;
                            case "Orange":
                                Console.WriteLine("Le feu est orange");
                                break;
                            case "Red":
                                Console.WriteLine("Le feu est rouge");
                                break;
                            case "Off":
                                Console.WriteLine("Le feu est éteint");
                                break;
                        }
                    }
                }

                Thread.Sleep(500);
            }
        }
    }
}
