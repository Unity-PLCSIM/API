//----------------------------------------------------------------------------------------------------------------------
// SERVICIO PLC, TIA PORTAL API
//
// Desc: Capa de negocio que abstrae la comunicación con PLCSim Advanced a través
//       de la librería Siemens.Simatic.Simulation.Runtime. Expone métodos para
//       gestionar instancias y leer/escribir tags del PLC.
//
// Coms:  - Singleton: acceder siempre a través de PlcService.Instance
//        - Thread-safe: todas las operaciones sobre _plcInstance están bajo lock
//        - Requiere que el Runtime Manager de PLCSim Advanced esté en ejecución
//
// Uso:
//   PlcService.Instance.Connect(0);
//   string val = PlcService.Instance.ReadValue("Motor", "Bool");
//   PlcService.Instance.WriteValue("Marcha", "true", "Bool");
//
// Autor: Alex Asensio
// Date: Julio 2026
//----------------------------------------------------------------------------------------------------------------------

using Siemens.Simatic.Simulation.Runtime;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PlcSimWebApi
{
    public class PlcService
    {
        // Instancia global única (Singleton)
        private static readonly PlcService _instanceSingleton = new PlcService();
        public static PlcService Instance => _instanceSingleton;

        private IInstance _plcInstance;
        private readonly object _lock = new object();

        /// <summary>
        /// Traduce los tipos primitivos de PLCSim a los nombres de tipo usados en la API.
        /// </summary>
        private static readonly Dictionary<EPrimitiveDataType, string> TypeNames =
            new Dictionary<EPrimitiveDataType, string>
            {
                { EPrimitiveDataType.Bool,   "Bool" },
                { EPrimitiveDataType.UInt8,  "Byte (UInt8)" },
                { EPrimitiveDataType.Int8,   "SInt (Int8)" },
                { EPrimitiveDataType.Int16,  "Int (Int16)" },
                { EPrimitiveDataType.UInt16, "UInt (UInt16)" },
                { EPrimitiveDataType.Int32,  "DInt (Int32)" },
                { EPrimitiveDataType.UInt32, "UDInt (UInt32)" },
                { EPrimitiveDataType.Int64,  "LInt (Int64)" },
                { EPrimitiveDataType.UInt64, "ULInt (UInt64)" },
                { EPrimitiveDataType.Float,  "Real (Float)" },
                { EPrimitiveDataType.Double, "LReal (Double)" },
            };

        private PlcService() { }

        // -- Tags ---------------------------------------------------------------

        /// <summary>
        /// Devuelve la lista completa de tags con nombre, tipo y valor actual.
        /// Excluye tags internos de sistema (RTG, F_SystemInfo).
        /// Si un tag falla al leerse devuelve "Error al leer" en su valor.
        /// </summary>
        public List<object> GetTagsWithValues()
        {
            if (_plcInstance == null) throw new Exception("No conectado a ninguna instancia.");

            lock (_lock)
            {
                _plcInstance.UpdateTagList();
                var result = new List<object>();

                foreach (STagInfo tag in _plcInstance.TagInfos)
                {
                    if (TypeNames.TryGetValue(tag.PrimitiveDataType, out string typeName) && (!tag.Name.StartsWith("RTG") && !tag.Name.StartsWith("F_SystemInfo")))
                    {
                        string valorActual;
                        try
                        {
                            valorActual = ReadValue(tag.Name, typeName);
                        }
                        catch (Exception)
                        {
                            // Si el PLC está en STOP o hay un error con un tag específico, evitamos que rompa toda la lista
                            valorActual = "Error al leer";
                        }

                        result.Add(new
                        {
                            Name = tag.Name,
                            Type = typeName,
                            Value = valorActual
                        });
                    }
                }
                return result;
            }
        }

        // -- Instancias ---------------------------------------------------------

        /// <summary>
        /// Devuelve la lista de instancias PLC registradas en el Runtime Manager.
        /// </summary>
        public List<object> GetInstances()
        {
            if (!SimulationRuntimeManager.IsRuntimeManagerAvailable)
                throw new Exception("Runtime Manager no disponible.");

            var result = new List<object>();
            foreach (SInstanceInfo info in SimulationRuntimeManager.RegisteredInstanceInfo)
            {
                result.Add(new { info.ID, info.Name });
            }
            return result;
        }

        /// <summary>
        /// Conecta el servicio a la instancia PLC con el ID indicado y actualiza la lista de tags.
        /// </summary>
        /// <param name="id">ID de la instancia registrada en el Runtime Manager.</param>
        public void Connect(int id)
        {
            lock (_lock)
            {
                _plcInstance = SimulationRuntimeManager.CreateInterface(id);
                _plcInstance.UpdateTagList();
            }
        }

        /// <summary>
        /// Devuelve la lista de tags disponibles en la instancia conectada (nombre y tipo).
        /// </summary>
        public List<TagItemDto> GetTags()
        {
            if (_plcInstance == null) throw new Exception("No conectado a ninguna instancia.");

            lock (_lock)
            {
                _plcInstance.UpdateTagList();
                var result = new List<TagItemDto>();

                foreach (STagInfo tag in _plcInstance.TagInfos)
                {
                    if (TypeNames.TryGetValue(tag.PrimitiveDataType, out string typeName))
                    {
                        result.Add(new TagItemDto { Name = tag.Name, Type = typeName });
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// Lee el valor actual de un tag por nombre y tipo.
        /// </summary>
        /// <param name="tag">Nombre exacto del tag.</param>
        /// <param name="type">Tipo del tag (ej: "Bool", "DInt (Int32)").</param>
        public string ReadValue(string tag, string type)
        {
            if (_plcInstance == null) throw new Exception("No conectado.");

            lock (_lock)
            {
                object value;
                switch (type)
                {
                    case "Bool": value = _plcInstance.ReadBool(tag); break;
                    case "Byte (UInt8)": value = _plcInstance.ReadUInt8(tag); break;
                    case "SInt (Int8)": value = _plcInstance.ReadInt8(tag); break;
                    case "Int (Int16)": value = _plcInstance.ReadInt16(tag); break;
                    case "UInt (UInt16)": value = _plcInstance.ReadUInt16(tag); break;
                    case "DInt (Int32)": value = _plcInstance.ReadInt32(tag); break;
                    case "UDInt (UInt32)": value = _plcInstance.ReadUInt32(tag); break;
                    case "LInt (Int64)": value = _plcInstance.ReadInt64(tag); break;
                    case "ULInt (UInt64)": value = _plcInstance.ReadUInt64(tag); break;
                    case "Real (Float)": value = _plcInstance.ReadFloat(tag); break;
                    case "LReal (Double)": value = _plcInstance.ReadDouble(tag); break;
                    default: throw new Exception("Tipo no soportado.");
                }
                return FormatValue(value);
            }
        }

        /// <summary>
        /// Escribe un valor en un tag por nombre y tipo.
        /// </summary>
        /// <param name="tag">Nombre exacto del tag.</param>
        /// <param name="raw">Valor a escribir como string (ej: "true", "42").</param>
        /// <param name="type">Tipo del tag (ej: "Bool", "DInt (Int32)").</param>
        public void WriteValue(string tag, string raw, string type)
        {
            if (_plcInstance == null) throw new Exception("No conectado.");

            lock (_lock)
            {
                switch (type)
                {
                    case "Bool": _plcInstance.WriteBool(tag, ParseBool(raw)); break;
                    case "Byte (UInt8)": _plcInstance.WriteUInt8(tag, byte.Parse(raw, CultureInfo.InvariantCulture)); break;
                    case "SInt (Int8)": _plcInstance.WriteInt8(tag, sbyte.Parse(raw, CultureInfo.InvariantCulture)); break;
                    case "Int (Int16)": _plcInstance.WriteInt16(tag, short.Parse(raw, CultureInfo.InvariantCulture)); break;
                    case "UInt (UInt16)": _plcInstance.WriteUInt16(tag, ushort.Parse(raw, CultureInfo.InvariantCulture)); break;
                    case "DInt (Int32)": _plcInstance.WriteInt32(tag, int.Parse(raw, CultureInfo.InvariantCulture)); break;
                    case "UDInt (UInt32)": _plcInstance.WriteUInt32(tag, uint.Parse(raw, CultureInfo.InvariantCulture)); break;
                    case "LInt (Int64)": _plcInstance.WriteInt64(tag, long.Parse(raw, CultureInfo.InvariantCulture)); break;
                    case "ULInt (UInt64)": _plcInstance.WriteUInt64(tag, ulong.Parse(raw, CultureInfo.InvariantCulture)); break;
                    case "Real (Float)": _plcInstance.WriteFloat(tag, float.Parse(raw, CultureInfo.InvariantCulture)); break;
                    case "LReal (Double)": _plcInstance.WriteDouble(tag, double.Parse(raw, CultureInfo.InvariantCulture)); break;
                    default: throw new Exception("Tipo no soportado.");
                }
            }
        }

        /// <summary>
        /// Parsea un string como bool admitiendo múltiples formatos (true/false, 1/0, si/no, on/off).
        /// </summary>
        private static bool ParseBool(string raw)
        {
            switch (raw.Trim().ToLowerInvariant())
            {
                case "1": case "true": case "on": case "si": case "sí": return true;
                case "0": case "false": case "off": case "no": return false;
                default: return bool.Parse(raw);
            }
        }

        /// <summary>
        /// Formatea un valor numérico a string usando cultura invariante para evitar
        /// problemas con separadores decimales según el sistema operativo.
        /// </summary>
        private static string FormatValue(object value)
        {
            if (value is float f) return f.ToString(CultureInfo.InvariantCulture);
            if (value is double d) return d.ToString(CultureInfo.InvariantCulture);
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Crea y enciende una nueva instancia PLC con el nombre y configuración de red indicados.
        /// Lanza una excepción si ya existe una instancia con el mismo nombre.
        /// </summary>
        public void CreateInstance(CreateInstanceRequest req)
        {
            // Comprobar si ya existe...
            var instanciasActuales = SimulationRuntimeManager.RegisteredInstanceInfo;
            if (instanciasActuales != null)
            {
                foreach (var info in instanciasActuales)
                {
                    if (info.Name.Equals(req.Name, StringComparison.OrdinalIgnoreCase))
                        throw new Exception($"Ya existe una instancia con el nombre '{req.Name}'.");
                }
            }

            // Configurar el modo de red GLOBAL antes de registrar la instancia
            if (!string.IsNullOrEmpty(req.NetworkType))
            {
                if (req.NetworkType.ToLower() == "softbus")
                {
                    SimulationRuntimeManager.NetworkMode = ENetworkMode.Softbus;
                }
                else if (req.NetworkType.ToLower() == "tcpip")
                {
                    SimulationRuntimeManager.NetworkMode = ENetworkMode.TCPIPSingleAdapter;
                    // Nota: Aquí podrías guardar req.IpAddress en tu base de datos o 
                    // imprimirla en consola como hacías antes, a la espera del TIA Portal.
                }
            }

            // Registrar la nueva instancia (usando tu formato de CPU no especificada)
            IInstance newInstance = SimulationRuntimeManager.RegisterInstance(ECPUType.CPU1500_SW_OC_Unspecified, req.Name);

            // Encenderla
            newInstance.PowerOn();
        }

        /// <summary>
        /// Apaga y elimina la instancia PLC con el nombre indicado.
        /// Lanza una excepción si no se encuentra ninguna instancia con ese nombre.
        /// </summary>
        public void DeleteInstance(string name)
        {
            // 1. Buscamos si la instancia existe
            var instanciasActuales = SimulationRuntimeManager.RegisteredInstanceInfo;
            bool existe = false;

            if (instanciasActuales != null)
            {
                foreach (var info in instanciasActuales)
                {
                    if (info.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        existe = true;
                        break;
                    }
                }
            }

            if (!existe)
            {
                throw new Exception($"No se encontró ninguna instancia activa con el nombre '{name}'.");
            }

            // 2. Nos conectamos a ella para apagarla y destruirla
            IInstance instanceToDelete = SimulationRuntimeManager.CreateInterface(name);

            if (instanceToDelete.OperatingState != EOperatingState.Off)
            {
                instanceToDelete.PowerOff();
            }

            instanceToDelete.UnregisterInstance();
        }
    }
}