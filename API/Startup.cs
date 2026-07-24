using Owin;
using System.Web.Http;
using Microsoft.Owin.Cors;
using Microsoft.Owin.StaticFiles;
using Microsoft.Owin.FileSystems;
using System.IO;
using System.Reflection;

namespace PlcSimWebApi
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // 1. Permitir CORS (por si acaso haces peticiones desde otro origen)
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

            // 3. Configurar el Servidor de Archivos Estáticos (La página web)
            // Obtiene la ruta donde se está ejecutando la API (.exe)
            var exeFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var webFolder = Path.Combine(exeFolder, "wwwroot");

            // Crea la carpeta automáticamente si no existe para evitar errores
            if (!Directory.Exists(webFolder))
            {
                Directory.CreateDirectory(webFolder);
            }

            var fileSystem = new PhysicalFileSystem(webFolder);
            var options = new FileServerOptions
            {
                EnableDefaultFiles = true,
                FileSystem = fileSystem
            };

            // Busca index.html por defecto cuando entras a la raíz (http://IP:9000/)
            options.DefaultFilesOptions.DefaultFileNames = new[] { "index.html" };

            app.UseFileServer(options);
        }
    }
}