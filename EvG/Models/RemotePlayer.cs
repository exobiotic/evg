using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace EvG.Models
{
    public class RemotePlayer: Player
    {
        public string? Address { get; set; }

        private HttpClient _client;

        private JsonSerializerSettings SerializationSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            },
            Formatting = Formatting.None
        };

        public RemotePlayer()
        {
            _client = new HttpClient();
            _client.Timeout = TimeSpan.FromSeconds(5);
        }

        public override void SetTimeout(float timeout)
        {
            _client.Dispose();
            _client = new HttpClient();
            _client.Timeout = TimeSpan.FromSeconds(timeout);
        }

            public override async Task<Action[]> GetActions(Game game, Unit unit, Unit[] units, Unit[] foes)
            {
                var data = JsonConvert.SerializeObject(new
                {
                    game.Spec.FloorMap,
                    Unit = unit,
                    Units = units,
                    Foes = foes
                }, SerializationSettings);

                Console.WriteLine($"[DEBUG] -> POST {Address}  unit={unit.Id} foes={foes.Length}");
                Console.WriteLine($"[DEBUG]    body={data}");

                try
                {
                    var response = await _client.PostAsync(Address, new StringContent(data, System.Text.Encoding.UTF8, "application/json"));
                    var responseString = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[DEBUG] <- {(int)response.StatusCode} body={responseString}");

                    if (response.IsSuccessStatusCode)
                    {
                        var actions = JsonConvert.DeserializeObject<Action[]>(responseString, SerializationSettings) ?? new Action[0];
                        Console.WriteLine($"[DEBUG]    parsed {actions.Length} action(s): {string.Join(", ", actions.Select(a => $"{a.Type}/{a.Direction}"))}");
                        return actions;
                    }
                    else
                    {
                        Console.WriteLine($"[DEBUG]    non-success status {response.StatusCode}, returning empty actions");
                        return new Action[0];
                    }
                }
                catch (TaskCanceledException)
                {
                    Console.WriteLine($"[DEBUG] TIMEOUT for {Address} after {_client.Timeout.TotalSeconds}s");
                    return new Action[0];
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[DEBUG] EXCEPTION for {Address}: {e.Message}");
                    return new Action[0];
                }
            }

            public override async Task GameEnded(Game game, Unit[] units, Unit[] foes)
            {
                var data = JsonConvert.SerializeObject(new
                {
                    game.Spec.FloorMap,
                    Units = units,
                    Foes = foes
                }, SerializationSettings);

                await _client.PostAsync(Address + "/end", new StringContent(data));
            }
    }
}
