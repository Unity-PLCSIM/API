//----------------------------------------------------------------------------------------------------------------------
// PROGRAM, TIA PORTAL API
//
// Desc: Punto de entrada de la aplicación. Arranca el servidor OWIN en el puerto 9000
//       y mantiene la consola abierta hasta que el usuario pulse ENTER.
//
// Coms:  - Abrir puerto en el firewall antes de ejecutar:
//            New-NetFirewallRule -DisplayName "API PLCSim" -Direction Inbound -LocalPort 9000 -Protocol TCP -Action Allow
//        - Ejecutar como administrador si se usa http://+:9000/
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

            using (WebApp.Start<Startup>(url: baseAddress))
            {
                Console.WriteLine($"API de PLCSim Advanced corriendo en {baseAddress}");
                Console.WriteLine("Presiona ENTER para detener la API...");
                Console.ReadLine();
            }
        }
    }
}