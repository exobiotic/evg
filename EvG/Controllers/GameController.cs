using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EvG.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace EvG.Controllers
{
    [Route("api/[controller]")]
    public class GameController : Controller
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IGameEngine GameEngine;
        private JsonSerializerSettings SerializationSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            },
            Formatting = Formatting.None
        };

        public GameController(IHttpContextAccessor httpContextAccessor, IGameEngine gameEngine)
        {
            _httpContextAccessor = httpContextAccessor;
            GameEngine = gameEngine;
        }

        [HttpGet]
        public async Task Get()
        {
            var response = _httpContextAccessor.HttpContext.Response;
            response.Headers["Content-Type"] = "text/event-stream";

            EventHandler<GameEventArgs> createdHandler = async (object sender, GameEventArgs args) =>
            {
                var gameData = new
                {
                    type = "game-created",
                    players = GameEngine.CurrentGame?.Spec?.Players ?? Array.Empty<Player>()
                };
                await response.WriteAsync($"data: {JsonConvert.SerializeObject(gameData, SerializationSettings)}\n\n");
                await response.Body.FlushAsync();
            };
            EventHandler<GameEventArgs> endedHandler = async (object sender, GameEventArgs args) =>
            {
                await response.WriteAsync($"data: {{ \"type\": \"game-ended\"}}\n\n");
            };

            EventHandler<PlayerEventArgs> playerHandler = async (object sender, PlayerEventArgs args) =>
            {
                await response.WriteAsync($"data: {{ \"type\": \"{args.EventType}\", \"player\": {JsonConvert.SerializeObject(args.Player, SerializationSettings)}}}\n\n");
            };
            EventHandler tournamentCompleteHandler = async (object sender, EventArgs args) =>
            {
                var winner = GameEngine.GetTournamentWinner();
                var tournamentData = new
                {
                    type = "tournament-complete",
                    winner
                };
                await response.WriteAsync($"data: {JsonConvert.SerializeObject(tournamentData, SerializationSettings)}\n\n");
                await response.Body.FlushAsync();
            };
            GameEngine.OnGameCreated += createdHandler;
            GameEngine.OnGameEnded += endedHandler;
            GameEngine.OnPlayerCreated += playerHandler;
            GameEngine.OnPlayerUpdated += playerHandler;
            GameEngine.OnTournamentComplete += tournamentCompleteHandler;

            try
            {
                await new Task(() => { });
            }
            finally
            {
                GameEngine.OnGameCreated -= createdHandler;
                GameEngine.OnGameEnded -= endedHandler;
                GameEngine.OnPlayerCreated -= playerHandler;
                GameEngine.OnPlayerUpdated -= playerHandler;
                GameEngine.OnTournamentComplete -= tournamentCompleteHandler;
            }
        }

        [HttpPost]
        public ActionResult Post([FromBody]string action)
        {
            if (action == "new")
            {
                GameEngine.NewGame(new GameSpec());
            }
            if (action == "start")
            {
                GameEngine.CurrentGame.Start();
            }
            return Ok();
        }
    }
}
