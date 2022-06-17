using Mirror;
using UnityEngine;
#if SERVER
using Aws.GameLift.Server;
using Aws.GameLift.Server.Model;
using System.Collections;
#endif
namespace SturfeeVPS.Networking
{
    public class GameLiftAuthenticator : NetworkAuthenticator
    {
        public struct AuthRequestMessage : NetworkMessage
        {
            public string PlayerSessionId;
            public string PlayerId;
            public string PlayerName;
        }

        public struct AuthResponseMessage : NetworkMessage
        {
            public byte Code;
            public string Message;
        }

        public delegate void AuthenticationFailed(string error);
        public event AuthenticationFailed OnAuthenticationFailed;

        /// <summary>
        /// Called on server from OnServerAuthenticateInternal when a client needs to authenticate
        /// </summary>
        /// <param name="conn">Connection to client.</param>
        public override void OnServerAuthenticate(NetworkConnection conn) { }

#if SERVER
        /// <summary>
        /// Called on server from StartServer to initialize the Authenticator
        /// <para>Server message handlers should be registered in this method.</para>
        /// </summary>
        public override void OnStartServer()
        {
            MyLogger.Log("GameLiftAuthenticator :: On server start");
            // register a handler for the authentication request we expect from client
            NetworkServer.RegisterHandler<AuthRequestMessage>(OnAuthRequestMessage, false);
        }

        /// <summary>
        /// Called on server when the client's AuthRequestMessage arrives
        /// </summary>
        /// <param name="conn">Connection to client.</param>
        /// <param name="msg">The message payload</param>
        public void OnAuthRequestMessage(NetworkConnection conn, AuthRequestMessage msg)
        { 
            MyLogger.Log(" GameLiftAuthenticator :: Auth request message received");

            var outcome = GameLiftServerAPI.AcceptPlayerSession(msg.PlayerSessionId);
            if (outcome.Success)
            {
                MyLogger.Log($" GameLiftAuthenticator :: GameLift accepted player session");

                conn.Send(new AuthResponseMessage() 
                { 
                    Code = 100,
                    Message = "Authentication successful"
                });
                conn.authenticationData = msg;

                // Accept the successful authentication
                ServerAccept(conn);
            }
            else
            {
                MyLogger.Log($" GameLiftAuthenticator :: GameLift did not accept player session with Player Session ID {msg.PlayerSessionId}");

                conn.Send(new AuthResponseMessage()
                {
                    Code = 200,
                    //Message = $"Authentication failed. The server could not find any Player Sessions with the Player Session ID {msg.PlayerSessionId}"
                    Message = $"Authentication failed. GameLIft did not accept player session with Player Session ID {msg.PlayerSessionId}." +
                    $"Reason : {outcome.Error.ErrorMessage}"
                }); ;
                conn.isAuthenticated = false;
                DelayedDisconnect(conn, 1);
            }
        }

        Coroutine DelayedDisconnect(NetworkConnection conn, float waitTime)
        {
            return StartCoroutine(delayedDisconnectRoutine());
            IEnumerator delayedDisconnectRoutine()
            {
                yield return new WaitForSeconds(waitTime);

                // Reject the unsuccessful authentication
                ServerReject(conn);
            }
        }
#endif

        /// <summary>
        /// Called on client from OnClientAuthenticateInternal when a client needs to authenticate
        /// </summary>
        /// <param name="conn">Connection of the client.</param>
        public override void OnClientAuthenticate()
        {

#if CLIENT                        
            var gameliftClient = FindObjectOfType<GameLiftClient>();
            if(gameliftClient == null)
            {
                MyLogger.LogError($" GameLiftAuthenticator :: GameLiftClient not found in scene");
            }
            MyLogger.Log($" GameLiftAuthenticator :: Authenticating Client...   playerId : {gameliftClient.PlayerId}  plaserSessionId : {gameliftClient.PlayerSessionId}");
            AuthRequestMessage msg = new AuthRequestMessage()
            {
                PlayerId = gameliftClient.PlayerId,
                PlayerSessionId = gameliftClient.PlayerSessionId,            
            };

            NetworkClient.Send(msg);            
#endif
        }

#if CLIENT
        /// <summary>
        /// Called on client from StartClient to initialize the Authenticator
        /// <para>Client message handlers should be registered in this method.</para>
        /// </summary>
        public override void OnStartClient()
        {

            MyLogger.Log("Client started...");
            // register a handler for the authentication response we expect from server
            NetworkClient.RegisterHandler<AuthResponseMessage>(OnAuthResponseMessage, false);
        }

        public override void OnStopClient()
        {
            MyLogger.Log(" Client disconnected");
            OnAuthenticationFailed?.Invoke("");
        }

        /// <summary>
        /// Called on client when the server's AuthResponseMessage arrives
        /// </summary>
        /// <param name="conn">Connection to client.</param>
        /// <param name="msg">The message payload</param>
        public void OnAuthResponseMessage(AuthResponseMessage msg)
        {
            if (msg.Code == 100)
            {
                MyLogger.Log($"GameLiftAuthenticator :: Authentication success  \t msg : {msg.Message}");

                // Authentication has been accepted
                ClientAccept();
            }
            else
            {
                MyLogger.Log($"GameLiftAuthenticator :: Authentication fail : {msg.Message}");                
                OnAuthenticationFailed?.Invoke(msg.Message);
                ClientReject();
            }
        }
#endif
    }
}