//----------------------------------------------------------------------------------------------------------------------
// PROGRAM, TIA PORTAL API
//
// Desc: Punto de entrada de la aplicación. Arranca el servidor OWIN en el puerto 9000
//       y el servidor WebSocket en el puerto 9001. Mantiene la consola abierta hasta
//       que el usuario pulse ENTER.
//
// Coms:  - Abrir puertos en el firewall antes de ejecutar:
//            New-NetFirewallRule -DisplayName "API PLCSim"    -Direction Inbound -LocalPort 9000 -Protocol TCP -Action Allow
//            New-NetFirewallRule -DisplayName "API PLCSim WS" -Direction Inbound -LocalPort 9001 -Protocol TCP -Action Allow
//        - Ejecutar como administrador si se usa http://+:9000/ y http://+:9001/
//
// Autor: Alex Asensio
// Date: Julio 2026
//----------------------------------------------------------------------------------------------------------------------

using Microsoft.Owin.Hosting;
using System;

namespace PlcSimWebApi
{
    class Program
    {
        static void Main(string[] args)
        {
            string baseAddress = "http://+:9000/";

            WebSocketHandler.Start("http://+:9001/ws/tags/"); // [WS] Servidor WebSocket en puerto 9001

            using (WebApp.Start<Startup>(url: baseAddress))
            {
                Console.WriteLine($"API REST   corriendo en {baseAddress}");
                Console.WriteLine($"WebSocket  corriendo en ws://+:9001/ws/tags");
                Console.WriteLine("Presiona ENTER para detener...");
                Console.ReadLine();
            }

            WebSocketHandler.Stop(); // [WS] Cierre limpio al salir
        }
    }
}