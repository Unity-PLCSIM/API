//----------------------------------------------------------------------------------------------------------------------
// WEBSOCKET HANDLER, TIA PORTAL API
//
// Desc: Gestiona las conexiones WebSocket entrantes y emite cambios de tags
//       a todos los clientes conectados. Usa HttpListener nativo de .NET Framework,
//       sin dependencias externas. Corre en un puerto separado (9001) para evitar
//       conflictos con el pipeline OWIN de la API REST (9000).
//
// Ruta WebSocket: ws://<host>:9001/ws/tags
//
// Protocolo de mensajes (JSON):
//   Servidor → Cliente:
//     [ { "Name": "Motor", "Type": "Bool", "Value": "True", "Area": "S" }, ... ]
//
//   Cliente → Servidor:
//     (no se esperan mensajes del cliente; se ignoran)
//
// Autor: Alex Asensio
// Date: Julio 2026
//----------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlcSimWebApi
{
    public static class WebSocketHandler
    {
        // Clientes conectados: guid → socket
        private static readonly ConcurrentDictionary<Guid, WebSocket> _clients
            = new ConcurrentDictionary<Guid, WebSocket>();

        private static HttpListener _listener;
        private static Thread _listenerThread;
        private static volatile bool _running = false;

        // -- Arranque y parada --------------------------------------------------

        /// <summary>
        /// Arranca el servidor WebSocket en ws://<host>:9001/ws/tags
        /// Llamar desde Program.cs al iniciar la aplicación.
        /// </summary>
        public static void Start(string prefix = "http://+:9001/ws/tags/")
        {
            if (_running) return;

            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _listener.Start();
            _running = true;

            _listenerThread = new Thread(AcceptLoop)
            {
                IsBackground = true,
                Name = "WebSocketAcceptLoop"
            };
            _listenerThread.Start();

            Console.WriteLine($"[WS] Servidor WebSocket escuchando en {prefix}");
        }

        /// <summary>
        /// Para el servidor WebSocket limpiamente.
        /// </summary>
        public static void Stop()
        {
            _running = false;
            _listener?.Stop();
        }

        // -- Bucle de aceptación de conexiones ----------------------------------

        private static void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    HttpListenerContext context = _listener.GetContext();

                    if (context.Request.IsWebSocketRequest)
                    {
                        // Aceptar en un Task aparte para no bloquear el bucle
                        Task.Run(() => HandleClientAsync(context));
                    }
                    else
                    {
                        // Rechazar peticiones HTTP normales en este puerto
                        context.Response.StatusCode = 426; // Upgrade Required
                        context.Response.Close();
                    }
                }
                catch (HttpListenerException)
                {
                    // Listener parado — salir del bucle limpiamente
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WS] Error en AcceptLoop: {ex.Message}");
                }
            }
        }

        // -- Gestión de cliente -------------------------------------------------

        private static async Task HandleClientAsync(HttpListenerContext context)
        {
            HttpListenerWebSocketContext wsContext =
                await context.AcceptWebSocketAsync(subProtocol: null);

            WebSocket socket = wsContext.WebSocket;
            Guid id = Guid.NewGuid();
            _clients[id] = socket;

            Console.WriteLine($"[WS] Cliente conectado: {id} | Total: {_clients.Count}");

            // --- PARCHE DE ESTADO INICIAL ---
            try
            {
                var initialState = PlcService.Instance.GetCachedOutputs();
                if (initialState.Count > 0)
                {
                    string json = SerializeChanges(initialState);
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WS] Error enviando estado inicial a {id}: {ex.Message}");
            }
            // --------------------------------

            try
            {
                byte[] buffer = new byte[256];
                while (socket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result = await socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Cierre solicitado por cliente",
                            CancellationToken.None);
                    }
                }
            }
            catch (WebSocketException ex)
            {
                Console.WriteLine($"[WS] Error en cliente {id}: {ex.Message}");
            }
            finally
            {
                _clients.TryRemove(id, out _);
                Console.WriteLine($"[WS] Cliente desconectado: {id} | Total: {_clients.Count}");
            }
        }

        // -- Broadcast ----------------------------------------------------------

        /// <summary>
        /// Serializa la lista de cambios a JSON y la envía a todos los clientes conectados.
        /// Llamar desde PlcService.DetectarCambiosSalidas() cuando hay cambios.
        /// Los clientes caídos se eliminan automáticamente.
        /// </summary>
        public static void Broadcast(List<TagChangeDto> changes)
        {
            if (changes == null || changes.Count == 0) return;
            if (_clients.IsEmpty) return;

            string json = SerializeChanges(changes);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(bytes);

            foreach (var kvp in _clients)
            {
                WebSocket socket = kvp.Value;
                if (socket.State != WebSocketState.Open) continue;

                _ = SendAsync(kvp.Key, socket, segment);
            }
        }

        private static async Task SendAsync(Guid id, WebSocket socket, ArraySegment<byte> segment)
        {
            try
            {
                await socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WS] Error enviando a {id}: {ex.Message}");
                _clients.TryRemove(id, out _);
            }
        }

        // -- Serialización JSON manual ------------------------------------------

        /// <summary>
        /// Serialización JSON manual para evitar dependencias externas.
        /// Produce: [{"Name":"...","Type":"...","Value":"...","Area":"..."},...]
        /// </summary>
        private static string SerializeChanges(List<TagChangeDto> changes)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < changes.Count; i++)
            {
                TagChangeDto c = changes[i];
                sb.Append("{");
                sb.Append($"\"Name\":\"{Escape(c.Name)}\",");
                sb.Append($"\"Type\":\"{Escape(c.Type)}\",");
                sb.Append($"\"Value\":\"{Escape(c.Value)}\",");
                sb.Append($"\"Area\":\"{Escape(c.Area)}\"");
                sb.Append("}");
                if (i < changes.Count - 1) sb.Append(",");
            }
            sb.Append("]");
            return sb.ToString();
        }

        private static string Escape(string s)
            => s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
    }
}