using System;
using System.Web.Http;

namespace PlcSimWebApi
{
    [RoutePrefix("api/plc")]
    public class PlcController : ApiController
    {
        [HttpGet]
        [Route("instances")]
        public IHttpActionResult GetInstances()
        {
            try
            {
                var instances = PlcService.Instance.GetInstances();
                return Ok(instances);
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        [HttpPost]
        [Route("instances/{id}/connect")]
        public IHttpActionResult Connect(int id)
        {
            try
            {
                PlcService.Instance.Connect(id);
                return Ok(new { Message = $"Conectado a la instancia ID {id}" });
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        [HttpGet]
        [Route("tags")]
        public IHttpActionResult GetTags()
        {
            try
            {
                var tags = PlcService.Instance.GetTags();
                return Ok(tags);
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        [HttpGet]
        [Route("tags/{tag}")]
        public IHttpActionResult ReadTag(string tag, string type)
        {
            try
            {
                var value = PlcService.Instance.ReadValue(tag, type);
                return Ok(new { Tag = tag, Value = value });
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        [HttpPut]
        [Route("tags/{tag}")]
        public IHttpActionResult WriteTag(string tag, [FromBody] WriteRequest request)
        {
            try
            {
                if (request == null) return BadRequest("Cuerpo de la petición vacío.");

                PlcService.Instance.WriteValue(tag, request.Value, request.Type);
                return Ok(new { Message = $"Variable {tag} actualizada correctamente a {request.Value}" });
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }
    }
}