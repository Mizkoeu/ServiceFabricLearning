using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Actors.Client;
using Game.Interfaces;

namespace Game
{
    /// <remarks>
    /// This class represents an actor.
    /// Every ActorID maps to an instance of this class.
    /// The StatePersistence attribute determines persistence and replication of actor state:
    ///  - Persisted: State is written to disk and replicated.
    ///  - Volatile: State is kept in memory only and replicated.
    ///  - None: State is kept in memory only and not replicated.
    /// </remarks>
    [StatePersistence(StatePersistence.Persisted)]
    internal class Game : Actor, IGame
    {
        private const string GameStateKey = "GameState";

        /// <summary>
        /// Initializes a new instance of Game
        /// </summary>
        /// <param name="actorService">The Microsoft.ServiceFabric.Actors.Runtime.ActorService that will host this actor instance.</param>
        /// <param name="actorId">The Microsoft.ServiceFabric.Actors.ActorId for this actor instance.</param>
        public Game(ActorService actorService, ActorId actorId) 
            : base(actorService, actorId)
        {
        }

        public async Task<bool> AcceptPlayerMoveAsync(long playerId, int x, int y)
        {
            var state = await StateManager.GetStateAsync<GameState>(GameStateKey);
            if (x < 0 || x > 2 || y < 0 || y > 2) return false;
            if (state.Players.Count != 2 || state.NumberOfMoves >= 9 || state.Winner != "") return false;

            int index = state.Players.FindIndex(p => p.Item1 == playerId);
            if (index == state.NextPlayerIndex)
            {
                if (state.Board[y * 3 + x] == 0)
                {
                    int piece = index * 2 - 1; // black piece is -1 and white piece is 1
                    state.Board[y * 3 + x] = piece;
                    state.NumberOfMoves++;
                    if (HasWon(state.Board, piece * 3))
                    {
                        state.Winner = $"{state.Players[index].Item2} ({(piece == -1 ? "X" : "O")})";
                    }
                    else if (state.Winner == "" && state.NumberOfMoves >= 9)
                    {
                        state.Winner = "TIE";
                    }
                    state.NextPlayerIndex = (state.NextPlayerIndex + 1) % 2;
                    await StateManager.SetStateAsync(GameStateKey, state);
                    return true;
                }

                return false;
            }

            return false;
        }

        private static bool HasWon(int[] board, int sum)
        {
            return board[0] + board[1] + board[2] == sum
                || board[3] + board[4] + board[5] == sum
                || board[6] + board[7] + board[8] == sum
                || board[0] + board[3] + board[6] == sum
                || board[1] + board[4] + board[7] == sum
                || board[2] + board[5] + board[8] == sum
                || board[0] + board[4] + board[8] == sum
                || board[2] + board[4] + board[6] == sum;
        }

        public async Task<bool> AcceptPlayerToGameAsync(long playerId, string playerName)
        {
            var state = await this.StateManager.GetStateAsync<GameState>(GameStateKey);
            if (state == null || state.Players.Count >= 2 || state.Players.FirstOrDefault(p => p.Item2 == playerName) != null)
            {
                return false;
            }

            state.Players.Add(new Tuple<long, string>(playerId, playerName));
            await this.StateManager.SetStateAsync<GameState>(GameStateKey, state);
            return true;
        }

        public async Task<int[]> GetGameBoardAsync()
        {
            return (await this.StateManager.GetStateAsync<GameState>(GameStateKey)).Board;
        }

        public async Task<string> GetWinnerAsync()
        {
            return (await StateManager.GetStateAsync<GameState>(GameStateKey)).Winner;
        }

        /// <summary>
        /// This method is called whenever an actor is activated.
        /// An actor is activated the first time any of its methods are invoked.
        /// </summary>
        protected override Task OnActivateAsync()
        {
            ActorEventSource.Current.ActorMessage(this, "Actor activated.");

            // The StateManager is this actor's private state store.
            // Data stored in the StateManager will be replicated for high-availability for actors that use volatile or persisted state storage.
            // Any serializable object can be saved in the StateManager.
            // For more information, see https://aka.ms/servicefabricactorsstateserialization
            ActorEventSource.Current.ActorMessage(this, "Actor is activated nowwwwww");

            return this.StateManager.TryAddStateAsync(GameStateKey, new GameState
            {
                Board = new int[9],
                NextPlayerIndex = 0,
                NumberOfMoves = 0,
                Players = new List<Tuple<long, string>>(),
                Winner = ""
            });
        }
    }
}
