//----------------------------------------------------------------------------------------------------------------------
// STARTUP, TIA PORTAL API
//
// Desc: Configuración del servidor OWIN. Define el pipeline de la aplicación:
//       habilita CORS y registra las rutas de la Web API.
//
// Coms:  - Requiere paquetes NuGet: Microsoft.Owin.Host.HttpListener, Microsoft.Owin.Cors
//        - CORS habilitado para permitir peticiones desde cualquier origen
//
// Autor: Alex Asensio
// Date: Julio 2026
//----------------------------------------------------------------------------------------------------------------------

using Owin;
using System.Web.Http;
using Microsoft.Owin.Cors;

namespace PlcSimWebApi
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            //Permitir CORS para peticiones desde cualquier origen
            app.UseCors(CorsOptions.AllowAll);

            //Configurar Web API con rutas por atributo y ruta por defecto
            HttpConfiguration config = new HttpConfiguration();
            config.MapHttpAttributeRoutes();
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            app.UseWebApi(config);
        }
    }
}