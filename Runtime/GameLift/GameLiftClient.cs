using Amazon;
using Amazon.CognitoIdentity;
# if CLIENT
using Amazon.GameLift;
using Amazon.GameLift.Model;
#endif
using Amazon.Runtime;
using kcp2k;
using Mirror;
using SturfeeVPS.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

using Debug = UnityEngine.Debug;
namespace SturfeeVPS.Networking
{
    public class GameLiftClient : Singleton<GameLiftClient>
    {
#if CLIENT

        public bool Local;

        [SerializeField]
        private int _maxPlayersInSession = 20;
        
        public string PlayerId { private set; get; }
        public string PlayerSessionId { private set; get;}

        private AmazonGameLiftClient _client;

        private bool _initialized;

        public void Initialize(AWSCredentials credentials, RegionEndpoint region)
        {
            var config = new AmazonGameLiftConfig();
            if (Local)
                config.ServiceURL = "http://localhost:7778";
            else
                config.RegionEndpoint = region;
            
            _client = new AmazonGameLiftClient(credentials, config);

            PlayerId = Guid.NewGuid().ToString();   

            _initialized = true;
        }

        public async Task<GameSession> CreateGameSessionAsync(List<GameProperty> gameProperties, string aliasId, CancellationToken token = default)
        {
            while (!_initialized)
            {
                await Task.Yield();
            }

            try
            {
                var request = new CreateGameSessionRequest
                {
                    MaximumPlayerSessionCount = _maxPlayersInSession,
                    GameProperties = gameProperties
                };

                if (Local)
                {
                    request.FleetId = "fleet-123";
                }
                else
                {
                    request.AliasId = aliasId;
                }
                var response = await _client.CreateGameSessionAsync(request, token);

                return response.GameSession;
            }
            catch (Exception ex)
            {
                SturfeeDebug.LogError(ex.Message);
                throw ex;
            }

        }

        /// <summary>
        /// Creates a player session on a specified game session
        /// </summary>
        /// <returns></returns>
        public async Task<PlayerSession> CreatePlayerSessionAsync(GameSession gameSession, CancellationToken token = default)
        {
            while (!_initialized)
            {
                await Task.Yield();
            }

            try
            {
                var response = await _client.CreatePlayerSessionAsync(gameSession.GameSessionId, PlayerId, token);
                PlayerSessionId = response.PlayerSession.PlayerSessionId;

                Debug.Log($" IP Address : {response.PlayerSession.IpAddress}");
                Debug.Log($" Port : {response.PlayerSession.Port}");

                NetworkManager.singleton.GetComponent<KcpTransport>().Port = (ushort)response.PlayerSession.Port;
                NetworkManager.singleton.networkAddress = response.PlayerSession.IpAddress;
                NetworkManager.singleton.StartClient();
                Debug.Log(" NetworkManager Start Client...");
                return response.PlayerSession;
            }
            catch (Exception ex)
            {
                SturfeeDebug.LogError(ex.Message);
                throw ex;
            }
        }

        /// <summary>
        /// Searches for a GameSession using a filter on a specified fleet
        /// </summary>
        /// <param name="filterExpression"></param>
        /// <param name="fleetId"></param>
        /// <returns></returns>
        public async Task<GameSession> SearchGameSessionAsync(string filterExpression, string aliasId, CancellationToken token = default)
        {
            while (!_initialized)
            {
                await Task.Yield();
            }

            Debug.Log($" Searching for GameSession with filter : {filterExpression }");
            try
            {
                var request = new SearchGameSessionsRequest
                {
                    FilterExpression = filterExpression
                };

                if (Local)
                {
                    request.FleetId = "fleet-123";
                }
                else
                {
                    request.AliasId = aliasId;
                }

                var response = await _client.SearchGameSessionsAsync(request, token);                
                if(response.GameSessions.Count <= 0)
                {
                    SturfeeDebug.Log($"No Gamesession running for filterExpression {filterExpression}");
                    return null;
                }

                var gameSession = response.GameSessions.FirstOrDefault();
                SturfeeDebug.Log(Newtonsoft.Json.JsonConvert.SerializeObject(gameSession));
                return gameSession;
            }
            catch (Exception ex)
            {
                SturfeeDebug.LogError(ex.Message);
                throw ex;
            }
        }

        public async Task<List<GameSession>> DescribeGameSessionsAsync(string aliasId, CancellationToken token = default)
        {
            while (!_initialized)
            {
                await Task.Yield();
            }

            try
            {
                var request = new DescribeGameSessionsRequest();
                if (Local)
                {
                    request.FleetId = "fleet-123";
                }
                else
                {
                    request.AliasId = aliasId;
                }

                var resposne = await _client.DescribeGameSessionsAsync(request, token);
                return resposne.GameSessions;
            }
            catch (Exception ex)
            {
                SturfeeDebug.LogError(ex.Message);
                throw ex;
            }
        }

        public async Task<GameSession> DescribeGameSessionAsync(GameSession gameSession)
        {
            while (!_initialized)
            {
                await Task.Yield();
            }

            try
            {
                var response = await _client.DescribeGameSessionsAsync(new DescribeGameSessionsRequest
                {
                    GameSessionId = gameSession.GameSessionId
                });

                return response.GameSessions.FirstOrDefault();
            }
            catch(Exception ex)
            {
                SturfeeDebug.LogError(ex.Message);
                throw ex;
            }
        }
#endif

    }
}
