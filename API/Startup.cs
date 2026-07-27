using Owin;
using System.Web.Http;
using Microsoft.Owin.Cors;

namespace PlcSimWebApi
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // 1. Permitir CORS (por si haces peticiones a la API desde otro frontend en el futuro)
            app.UseCors(CorsOptions.AllowAll);

            // 2. Configurar la Web API (Rutas de tu controlador)
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