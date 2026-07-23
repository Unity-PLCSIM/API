using Owin;
using System.Web.Http;

namespace PlcSimWebApi
{
    public class Startup
    {
        public void Configuration(IAppBuilder appBuilder)
        {
            HttpConfiguration config = new HttpConfiguration();

            // Habilitar enrutamiento por atributos ([Route])
            config.MapHttpAttributeRoutes();

            // Formatear JSON de salida para que sea más legible
            config.Formatters.JsonFormatter.SerializerSettings.Formatting = Newtonsoft.Json.Formatting.Indented;

            appBuilder.UseWebApi(config);
        }
    }
}