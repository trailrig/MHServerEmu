﻿using System.Net;
using MHServerEmu.Auth.Handlers;
using MHServerEmu.Core.Config;
using MHServerEmu.Core.Logging;
using MHServerEmu.Core.Network;
using MHServerEmu.Core.Network.Tcp;

namespace MHServerEmu.Auth
{
    /// <summary>
    /// Handles HTTP requests from clients.
    /// </summary>
    public class AuthServer : IGameService
    {
        private static readonly Logger Logger = LogManager.CreateLogger();

        private readonly string _url;
        private readonly AuthProtobufHandler _protobufHandler;
        private readonly AuthWebApiHandler _webApiHandler;

        private CancellationTokenSource _cts;
        private HttpListener _listener;

        /// <summary>
        /// Constructs a new <see cref="AuthServer"/> instance.
        /// </summary>
        public AuthServer()
        {
            var config = ConfigManager.Instance.GetConfig<AuthConfig>();

            _url = $"http://{config.Address}:{config.Port}/";
            _protobufHandler = new();

            if (config.EnableWebApi)
                _webApiHandler = new();
        }

        #region IGameService Implementation

        /// <summary>
        /// Runs this <see cref="AuthServer"/> instance.
        /// </summary>
        public async void Run()
        {
            // Reset CTS
            _cts?.Dispose();
            _cts = new();

            // Create an http server and start listening for incoming connections
            _listener = new HttpListener();
            _listener.Prefixes.Add(_url);
            _listener.Start();

            Logger.Info($"AuthServer is listening on {_url}...");

            while (true)
            {
                try
                {
                    // Wait for a connection, and handle the request
                    HttpListenerContext context = await _listener.GetContextAsync().WaitAsync(_cts.Token);
                    await HandleRequestAsync(context.Request, context.Response);
                    context.Response.Close();
                }
                catch (TaskCanceledException) { return; }       // Stop handling connections
                catch (Exception e)
                {
                    Logger.Error($"Run(): Unhandled exception: {e}");
                }
            }
        }

        /// <summary>
        /// Stops listening and shuts down this <see cref="AuthServer"/> instance.
        /// </summary>
        public void Shutdown()
        {
            if (_listener == null) return;
            if (_listener.IsListening == false) return;

            // Cancel async tasks (listening for context)
            _cts.Cancel();

            // Close the listener
            _listener.Close();
            _listener = null;
        }

        public void Handle(ITcpClient client, MessagePackage message)
        {
            Logger.Warn($"Handle(): AuthServer should not be handling messages from TCP clients!");
        }

        public void Handle(ITcpClient client, IEnumerable<MessagePackage> messages)
        {
            Logger.Warn($"Handle(): AuthServer should not be handling messages from TCP clients!");
        }

        public void Handle(ITcpClient client, MailboxMessage message)
        {
            Logger.Warn($"Handle(): AuthServer should not be handling messages from TCP clients!");
        }

        public string GetStatus()
        {
            if (_listener == null || _listener.IsListening == false)
                return "Not listening";
            
            return $"Protobuf Handler: {_protobufHandler != null} | Web API Handler: {_webApiHandler != null}";
        }

        #endregion

        /// <summary>
        /// Routes an <see cref="HttpListenerRequest"/> to the appropriate handler.
        /// </summary>
        private async Task HandleRequestAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            bool requestIsFromGameClient = (request.UserAgent == "Secret Identity Studios Http Client");

            // We should be getting only GET and POST
            switch (request.HttpMethod)
            {
                case "GET":
                    if (request.Url.LocalPath == "/favicon.ico") return;     // Ignore favicon requests

                    // Web API get requests
                    if (requestIsFromGameClient == false && _webApiHandler != null)
                    {
                        await _webApiHandler.HandleRequestAsync(request, response);
                        return;
                    }

                    break;

                case "POST":
                    // Client auth messages
                    if (requestIsFromGameClient && request.Url.LocalPath == "/Login/IndexPB")
                    {
                        await _protobufHandler.HandleMessageAsync(request, response);
                        return;
                    }
                    
                    // Web API post requests
                    if (requestIsFromGameClient == false && _webApiHandler != null)
                    {
                        await _webApiHandler.HandleRequestAsync(request, response);
                        return;
                    }

                    break;
            }

            // Display a warning for unhandled requests
            string source = requestIsFromGameClient ? "a game client" : $"an unknown UserAgent ({request.UserAgent})";
            Logger.Warn($"HandleRequestAsync(): Unhandled {request.HttpMethod} to {request.Url.LocalPath} from {source} on {request.RemoteEndPoint}");
        }
    }
}
