using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
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
        private ConcurrentDictionary<string, int> playedPairs = new ConcurrentDictionary<string, int>();

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
            var game = sender as Game;
            if (game == null)
            {
                return;
            }

            var winner = args.Winner ?? game.Winner;
            Console.WriteLine($"[TOURNAMENT] HandleGameEnding: Winner={winner?.Name}, TournamentMode={GameConfig.TournamentMode}, IsTournamentComplete={IsTournamentComplete}");
            OnGameEnded?.Invoke(this, new GameEventArgs("game-ended") { Winner = winner });
            if (winner != null)
            {
                winner.Score += GameConfig.GameValue;
                OnPlayerUpdated?.Invoke(this, new PlayerEventArgs { EventType = "player-updated", Player = winner });
            }

            if (!GameConfig.TournamentMode)
            {
                Console.WriteLine($"[TOURNAMENT] TournamentMode is OFF. Skipping tournament logic.");
                return;
            }

            // Record this pair as played
            if (game.Spec.Players != null && game.Spec.Players.Length == 2)
            {
                RecordPlayedPair(game.Spec.Players[0], game.Spec.Players[1]);
            }
            
            Console.WriteLine($"[TOURNAMENT] Game ended. RematchCount={GameConfig.RematchCount}, Played pairs: {string.Join(", ", playedPairs.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}");

            // Check if all pairs have now played
            if (AreAllPairsPlayed())
            {
                IsTournamentComplete = true;
                Console.WriteLine($"[TOURNAMENT] Tournament complete! All pairs have played {GameConfig.RematchCount}x each.");
                OnTournamentComplete?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Console.WriteLine($"[TOURNAMENT] Tournament continues - not all conditions met yet.");
            }
        }

        private void RecordPlayedPair(Player p1, Player p2)
        {
            // Store as sorted pair to avoid duplicates (A vs B = B vs A)
            var pair = string.Compare(p1.Id, p2.Id) < 0
                ? $"{p1.Id}|{p2.Id}"
                : $"{p2.Id}|{p1.Id}";
            
            playedPairs.AddOrUpdate(pair, 1, (key, oldValue) => oldValue + 1);
        }

        private bool AreAllPairsPlayed()
        {
            // Calculate total possible pairs: C(n, 2) = n * (n - 1) / 2
            int totalPairs = Players.Count * (Players.Count - 1) / 2;
            
            Console.WriteLine($"[TOURNAMENT] AreAllPairsPlayed check: TotalPairs={totalPairs}, PlayedPairsCount={playedPairs.Count}, RematchCount={GameConfig.RematchCount}");
            Console.WriteLine($"[TOURNAMENT] Played pairs detail: {string.Join("; ", playedPairs.Select(kvp => $"{kvp.Key}={kvp.Value}x"))}");
            
            // Check if all pairs have played the required number of times
            foreach (var kvp in playedPairs)
            {
                Console.WriteLine($"[TOURNAMENT] Checking {kvp.Key}: {kvp.Value} < {GameConfig.RematchCount}? {kvp.Value < GameConfig.RematchCount}");
                if (kvp.Value < GameConfig.RematchCount)
                {
                    Console.WriteLine($"[TOURNAMENT] Pair {kvp.Key} has not played enough times. Returning false.");
                    return false;
                }
            }
            
            bool result = playedPairs.Count >= totalPairs;
            Console.WriteLine($"[TOURNAMENT] All pairs played required times. Final check: {playedPairs.Count} >= {totalPairs} = {result}");
            return result;
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
