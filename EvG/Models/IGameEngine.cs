using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EvG.Models
{
    public interface IGameEngine
    {
        Game CurrentGame { get; }
        List<Player> Players { get; }
        GameConfig GameConfig { get; set; }
        bool IsTournamentComplete { get; }

        event EventHandler<GameEventArgs> OnGameCreated;
        event EventHandler<GameEventArgs> OnGameEnded;
        event EventHandler<PlayerEventArgs> OnPlayerCreated;
        event EventHandler<PlayerEventArgs> OnPlayerUpdated;
        event EventHandler OnTournamentComplete;

        void NewGame(GameSpec spec);
        void AddOrUpdatePlayer(Player player);
        void UpdatePlayerScore(Player player);
        void SetPlayerTimeout(float playerTimeout);
        void ResetTournament();
        Player? GetTournamentWinner();
    }
}
