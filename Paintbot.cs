namespace PaintBot
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Game.Configuration;
    using Messaging;
    using Messaging.Request;
    using Messaging.Response;
    using Action = Game.Action.Action;

    public abstract class Paintbot
    {
        private Client _client;
        private bool _hasGameEnded;
        private bool _hasTournamentEnded;
        private string _playerId;

        public abstract GameMode GameMode { get; }
        public abstract string Name { get; }
        public abstract Action GetAction(MapUpdated mapUpdated);

        public async Task Run(CancellationToken ct)
        {
            try
            {
                var url = $"wss://server.paintbot.cygni.se/{GameMode.ToString().ToLower()}";
                _client = await Client.ConnectAsync(new Uri(url), ct);

                await _client.SendAsync(new RegisterPlayer(Name, new GameSettings()), ct);

                await foreach (var response in _client.ReceiveEnumerableAsync<Response>(ct))
                {
                    await HandleResponseAsync(response, ct);
                    if (!IsPlaying()) break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message); // TODO: Add proper logging
            }
            finally
            {
                _client?.Close();
            }
        }

        private async Task HandleResponseAsync(Response response, CancellationToken ct)
        {
            switch (response)
            {
                case PlayerRegistered playerRegistered:
                    await OnPlayerRegistered(playerRegistered, ct);
                    break;

                case MapUpdated mapUpdated:
                    await OnMapUpdated(mapUpdated, ct);
                    break;

                case GameLink gameLink:
                    OnInfoEvent(gameLink);
                    break;

                case GameStarting gameStarting:
                    OnInfoEvent(gameStarting);
                    break;

                case GameResult gameResult:
                    OnInfoEvent(gameResult);
                    break;

                case CharacterStunned characterStunned:
                    OnInfoEvent(characterStunned);
                    break;

                case HeartBeatResponse heartBeatResponse:
                    OnHearBeatEvent(heartBeatResponse);
                    break;

                case GameEnded gameEnded:
                    OnGameEnded(gameEnded);
                    break;

                case TournamentEnded tournamentEnded:
                    OnTournamentEnded(tournamentEnded);
                    break;

                case InvalidPlayerName invalidPlayerName:
                    OnInfoEvent(invalidPlayerName);
                    throw new Exception("Player name was invalid. Shutting down...");

                default:
                    Console.WriteLine($"Unhandled response of type {response?.Type}");
                    break;
            }
        }

        private void OnTournamentEnded(TournamentEnded tournamentEnded)
        {
            _hasTournamentEnded = true;
            Console.WriteLine(tournamentEnded);
        }

        private async Task OnPlayerRegistered(PlayerRegistered playerRegistered, CancellationToken ct)
        {
            _playerId = playerRegistered.ReceivingPlayerId;
            SendHearBeat();
            await _client.SendAsync(new StartGame(playerRegistered.ReceivingPlayerId),
                ct); // TODO: Create a factory for requests?
            await _client.SendAsync(
                new ClientInfo("C#", "8", "Windows", "10", "1", playerRegistered.ReceivingPlayerId), ct);
            Console.WriteLine(playerRegistered);
        }

        private async Task OnMapUpdated(MapUpdated mapUpdated, CancellationToken ct)
        {
            var action = GetAction(mapUpdated);
            await _client.SendAsync(
                new RegisterMove(mapUpdated.ReceivingPlayerId)
                {
                    GameId = mapUpdated.GameId,
                    GameTick = mapUpdated.GameTick,
                    Direction = action.ToString().ToUpper()
                }, ct);
        }

        private void OnInfoEvent(Response response)
        {
            Console.WriteLine(response);
        }

        private void OnHearBeatEvent(HeartBeatResponse heartBeat)
        {
            Console.WriteLine(heartBeat);
            SendHearBeat();
        }

        private void OnGameEnded(GameEnded response)
        {
            _hasGameEnded = true;
            Console.WriteLine(response);
        }

        private bool IsPlaying()
        {
            if (GameMode == GameMode.Training)
                return !_hasGameEnded;
            return !_hasTournamentEnded;
        }

        private void SendHearBeat()
        {
            new HeartBeatSender(_playerId, _client).Run();
        }
    }
}