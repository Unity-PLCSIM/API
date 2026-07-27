//----------------------------------------------------------------------------------------------------------------------
// MODELOS DE DATOS, TIA PORTAL API
//
// Desc: DTOs (Data Transfer Objects) compartidos entre el controlador y el servicio.
//       Define los modelos usados en el cuerpo de las peticiones HTTP.
//
// Autor: Alex Asensio
// Date: Julio 2026
//----------------------------------------------------------------------------------------------------------------------

namespace PlcSimWebApi
{
    /// <summary>
    /// Cuerpo de una petición de escritura de tag.
    /// </summary>
    public class WriteRequest
    {
        public string Value { get; set; }
        public string Type { get; set; }
    }

    /// <summary>
    /// Tag con nombre y tipo, sin valor. Usado en GET /tags.
    /// </summary>
    public class TagItemDto
    {
        public string Name { get; set; }
        public string Type { get; set; }
    }
}