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
    [Route("api/game/[controller]")]
    public class ActionsController : Controller
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IGameEngine _gameEngine;
        private JsonSerializerSettings SerializationSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            },
            Formatting = Formatting.None
        };

        public ActionsController(IHttpContextAccessor httpContextAccessor, IGameEngine gameEngine)
        {
            _httpContextAccessor = httpContextAccessor;
            _gameEngine = gameEngine;
        }

        [HttpGet]
        public async Task Get()
        {
            var gameTask = new Task(() => { });
            var response = _httpContextAccessor.HttpContext.Response;
            response.Headers["Content-Type"] = "text/event-stream";

            // Wait for a game to be created if one doesn't exist yet
            while (_gameEngine.CurrentGame == null)
            {
                await Task.Delay(100);
            }

            var game = _gameEngine.CurrentGame;

            EventHandler<MoveEventArgs> moveHandler = async (object sender, MoveEventArgs args) =>
            {
                await WriteMoveEvent(response, args);
            };
            EventHandler<AttackEventArgs> attackHandler = async (object sender, AttackEventArgs args) =>
            {
                await WriteAttackEvent(response, args);
            };
            EventHandler<GameEventArgs> endedHandler = async (object sender, GameEventArgs args) =>
            {
                await WriteGameEndedEvent(response, args);
                gameTask.Start();
            };
            game.OnUnitMoved += moveHandler;
            game.OnUnitAttacked += attackHandler;
            game.OnGameEnded += endedHandler;

            try
            {
                await gameTask;
            }
            finally
            {
                game.OnUnitMoved -= moveHandler;
                game.OnUnitAttacked -= attackHandler;
                game.OnGameEnded -= endedHandler;
            }
        }

        private async Task WriteMoveEvent(HttpResponse response, MoveEventArgs eventArgs)
        {
            await response.WriteAsync($"data: {{ \"type\": \"move\", \"unit\": {JsonConvert.SerializeObject(eventArgs.Unit, SerializationSettings)}}}\n\n");
            await response.Body.FlushAsync();
        }

        private async Task WriteAttackEvent(HttpResponse response, AttackEventArgs eventArgs)
        {
            await response.WriteAsync($"data: {{ \"type\": \"attack\", \"unit\": {JsonConvert.SerializeObject(eventArgs.Unit, SerializationSettings)}, \"target\": {JsonConvert.SerializeObject(eventArgs.Target, SerializationSettings)}}}\n\n");
            await response.Body.FlushAsync();
        }

        private async Task WriteGameEndedEvent(HttpResponse response, GameEventArgs eventArgs)
        {
            await response.WriteAsync($"data: {{ \"type\": \"game-ended\", \"winner\": {JsonConvert.SerializeObject(eventArgs.Winner, SerializationSettings)}}}\n\n");
            await response.Body.FlushAsync();
        }
    }
}
