using Microsoft.Owin.Hosting;
using System;

namespace PlcSimWebApi
{
    class Program
    {
        static void Main(string[] args)
        {
            string baseAddress = "http://+:9000/";  //abrir firewall maquina New-NetFirewallRule -DisplayName "API PLCSim" -Direction Inbound -LocalPort 9000 -Protocol TCP -Action Allow

            // Iniciar servidor web de OWIN
            using (WebApp.Start<Startup>(url: baseAddress))
            {
                Console.WriteLine($"API de PLCSim Advanced corriendo en {baseAddress}");
                Console.WriteLine("Presiona ENTER para detener la API...");
                Console.ReadLine(); // Evita que la consola se cierre automáticamente
            }
        }
    }
}