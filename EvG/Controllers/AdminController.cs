using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EvG.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EvG.Controllers
{
    [ApiController]
    public class AdminController : Controller
    {
        private readonly IGameEngine _gameEngine;

        public AdminController(IGameEngine gameEngine)
        {
            _gameEngine = gameEngine;
        }

        [HttpGet("api/[controller]/game-config")]
        public ActionResult<GameConfig> GetGameConfig()
        {
            return Ok(_gameEngine.GameConfig);
        }

        [HttpPost("api/[controller]/update-score")]
        public ActionResult UpdateScore([FromBody]RemotePlayer player)
        {
            _gameEngine.UpdatePlayerScore(player);
            return Ok();
        }

        [HttpPut("api/[controller]/game-config")]
        public ActionResult SetGameConfig([FromBody]GameConfigInput config)
        {
            if (config == null)
                return Ok();

            if (config.GameValue != null)
                _gameEngine.GameConfig.GameValue = (int)config.GameValue;

            if (config.ActionDelay != null)
                _gameEngine.GameConfig.ActionDelay = (int)config.ActionDelay;

            if (config.ForceMove != null)
                _gameEngine.GameConfig.ForceMove = (bool)config.ForceMove;

            if (config.BloodLust != null)
                _gameEngine.GameConfig.BloodLust = (bool)config.BloodLust;

            if (config.Fog != null)
                _gameEngine.GameConfig.Fog = (bool)config.Fog;

            if (config.RandomOrder != null)
                _gameEngine.GameConfig.RandomOrder = (bool)config.RandomOrder;

            if (config.StaticOrder != null)
                _gameEngine.GameConfig.StaticOrder = (bool)config.StaticOrder;

            if (config.TournamentMode != null)
                _gameEngine.GameConfig.TournamentMode = (bool)config.TournamentMode;

            if (config.PlayerTimeout != null)
            {
                _gameEngine.GameConfig.PlayerTimeout = (float)config.PlayerTimeout;
                _gameEngine.SetPlayerTimeout((float)config.PlayerTimeout);
            }

            if (config.UnitHealth != null)
                _gameEngine.GameConfig.UnitHealth = Math.Max(1, (int)config.UnitHealth);

            if (config.AttackDamage != null)
                _gameEngine.GameConfig.AttackDamage = Math.Max(1, (int)config.AttackDamage);

            if (config.RematchCount != null)
                _gameEngine.GameConfig.RematchCount = (int)config.RematchCount;

            if (config.MapWidth != null)
                _gameEngine.GameConfig.MapWidth = (int)config.MapWidth;

            if (config.MapHeight != null)
                _gameEngine.GameConfig.MapHeight = (int)config.MapHeight;

            if (config.RandomMap == true)
                _gameEngine.GameConfig.FixedMap = null;
            else if (config.FixedMap != null)
                _gameEngine.GameConfig.FixedMap = config.FixedMap;

            return Ok();
        }

        [HttpPost("api/[controller]/tournament-reset")]
        public ActionResult ResetTournament()
        {
            _gameEngine.ResetTournament();
            return Ok();
        }
    }
}