using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace EvG.Models
{
    public class GameEngine: IGameEngine
    {
        public Game? CurrentGame { get; private set; }
        public List<Player> Players { get; } = new List<Player>();
        public GameConfig GameConfig { get; set; } = new GameConfig();
        public bool IsTournamentComplete { get; private set; } = false;

        public event EventHandler<GameEventArgs>? OnGameCreated;
        public event EventHandler<GameEventArgs>? OnGameEnded;
        public event EventHandler<PlayerEventArgs>? OnPlayerCreated;
        public event EventHandler<PlayerEventArgs>? OnPlayerUpdated;
        public event EventHandler? OnTournamentComplete;

        private Stack<Player> playerStack = new Stack<Player>();
        private readonly RandomNumberGenerator random = RandomNumberGenerator.Create()!;
        private Dictionary<string, int> playedPairs = new Dictionary<string, int>();

        public void NewGame(GameSpec spec)
        {
            if (Players.Count < 2)
                return;

            if (GameConfig.TournamentMode && IsTournamentComplete)
            {
                Console.WriteLine("Tournament is complete. Call ResetTournament() to play again.");
                return;
            }

            if (CurrentGame != null)
            {
                CurrentGame.OnGameEnded -= HandleGameEnding;
            }

            var player1 = DrawPlayer(null);
            var player2 = DrawPlayer(player1);
            if (player1 == null || player2 == null)
            {
                return;
            }

            // Use authored map dimensions to keep tile rendering and collision stable.
            // (Resizing introduced tile-artifact regressions on non-square sizes.)

            CurrentGame = new Game(spec, player1, player2, GameConfig);
            CurrentGame.Spec.Players = new Player[] { player1, player2 };
            CurrentGame.OnGameEnded += HandleGameEnding;
            OnGameCreated?.Invoke(this, new GameEventArgs("game-created"));
        }

        public void AddOrUpdatePlayer(Player player)
        {
            var existing = Players.FirstOrDefault((p) => p.Id == player.Id);
            if (existing != null)
            {
                existing.Name = player.Name;
                OnPlayerUpdated?.Invoke(
                    this,
                    new PlayerEventArgs { EventType = "player-updated", Player = existing }
                );
            }
            else
            {
                player.Score = 0;
                Players.Add(player);
                OnPlayerCreated?.Invoke(
                    this,
                    new PlayerEventArgs { EventType = "player-created", Player = player }
                );
            }
        }

        public void UpdatePlayerScore(Player player)
        {
            var existing = Players.FirstOrDefault((p) => p.Id == player.Id);
            if (existing != null)
            {
                existing.Score = player.Score;
                OnPlayerUpdated?.Invoke(
                    this,
                    new PlayerEventArgs { EventType = "player-updated", Player = existing }
                );
            }
        }

        private void HandleGameEnding(object sender, GameEventArgs args)
        {
            var winner = CurrentGame.Winner;
            OnGameEnded?.Invoke(this, new GameEventArgs("game-ended") { Winner = winner });
            if (winner != null)
            {
                winner.Score += GameConfig.GameValue;
                OnPlayerUpdated?.Invoke(this, new PlayerEventArgs { EventType = "player-updated", Player = winner });
            }

            if (!GameConfig.TournamentMode)
            {
                return;
            }

            // Record this pair as played
            RecordPlayedPair(CurrentGame.Spec.Players[0], CurrentGame.Spec.Players[1]);

            // Check if all pairs have now played
            if (AreAllPairsPlayed())
            {
                IsTournamentComplete = true;
                Console.WriteLine("Tournament complete! All player pairs have met.");
                OnTournamentComplete?.Invoke(this, EventArgs.Empty);
            }
        }

        private void RecordPlayedPair(Player p1, Player p2)
        {
            // Store as sorted pair to avoid duplicates (A vs B = B vs A)
            var pair = string.Compare(p1.Id, p2.Id) < 0
                ? $"{p1.Id}|{p2.Id}"
                : $"{p2.Id}|{p1.Id}";
            
            if (playedPairs.ContainsKey(pair))
                playedPairs[pair]++;
            else
                playedPairs[pair] = 1;
        }

        private bool AreAllPairsPlayed()
        {
            // Calculate total possible pairs: C(n, 2) = n * (n - 1) / 2
            int totalPairs = Players.Count * (Players.Count - 1) / 2;
            
            // Check if all pairs have played the required number of times
            foreach (var kvp in playedPairs)
            {
                if (kvp.Value < GameConfig.RematchCount)
                    return false;
            }
            
            // Also ensure we have all possible pairs recorded at least once
            return playedPairs.Count >= totalPairs;
        }

        public void ResetTournament()
        {
            playedPairs.Clear();
            IsTournamentComplete = false;

            foreach (var player in Players)
            {
                player.Score = 0;
                OnPlayerUpdated?.Invoke(this, new PlayerEventArgs { EventType = "player-updated", Player = player });
            }

            Console.WriteLine("Tournament reset. Players can meet again.");
        }

        public Player? GetTournamentWinner()
        {
            if (Players.Count == 0)
            {
                return null;
            }

            var topScore = Players.Max(p => p.Score);
            var winners = Players.Where(p => p.Score == topScore).ToList();
            if (winners.Count != 1)
            {
                return null;
            }

            return winners[0];
        }

        private Player DrawPlayer(Player exclude)
        {
            if (playerStack.Count == 0)
            {
                var bytes = new byte[Players.Count];
                random.GetBytes(bytes);
                playerStack = new Stack<Player>(Players
                    .Zip(bytes, (player, order) => new { player, order })
                    .OrderBy(o => o.order)
                    .Select(o => o.player));

                if (exclude != null && playerStack.Count == 1 && playerStack.Peek() == exclude)
                {
                    return null;
                }
            }

            var draw = playerStack.Pop();
            if (exclude != null && exclude.Id == draw.Id)
            {
                var second = DrawPlayer(draw);
                playerStack.Push(draw);
                draw = second;
            }
            return draw;
        }

        public void SetPlayerTimeout(float playerTimeout)
        {
            if (Players != null)
            {
                Players.ForEach(player => player.SetTimeout(playerTimeout));
            }
        }
    }
}
