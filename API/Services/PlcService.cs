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
using System.Linq;

namespace PlcSimWebApi
{
    public class PlcService
    {
        // Instancia global única (Singleton)
        private static readonly PlcService _instanceSingleton = new PlcService();
        public static PlcService Instance => _instanceSingleton;

        private IInstance _plcInstance;
        private readonly object _lock = new object();

        // Detección de cambios en salidas
        private Dictionary<string, string> _lastOutputValues = new Dictionary<string, string>();
        private List<object> _pendingOutputChanges = new List<object>();
        private readonly object _changesLock = new object();

        // Hilo de polling de salidas
        private System.Threading.Thread _pollThread;
        private volatile bool _pollRunning = false;

        // Throttling: baja frecuencia si Unity no consulta en ClientTimeoutS segundos
        private DateTime _lastClientPoll = DateTime.MinValue;
        private const int FastIntervalMs = 100;
        private const int SlowIntervalMs = 1000;
        private const int ClientTimeoutS = 5;

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
        /// Devuelve la lista completa de tags con nombre, tipo, valor actual y área (E/S).
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
                    if (TypeNames.TryGetValue(tag.PrimitiveDataType, out string typeName)
                        && !tag.Name.StartsWith("RTG")
                        && !tag.Name.StartsWith("F_SystemInfo"))
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

                        string area = tag.Area == EArea.Input ? "E" :
                                      tag.Area == EArea.Output ? "S" : "O";

                        result.Add(new
                        {
                            Name = tag.Name,
                            Type = typeName,
                            Value = valorActual,
                            Area = area
                        });
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// Devuelve los tags de salida que han cambiado desde la última consulta y limpia la lista.
        /// Registra el timestamp de la consulta para el throttling del hilo de polling.
        /// </summary>
        public List<object> GetOutputTagsWithValues()
        {
            _lastClientPoll = DateTime.UtcNow;

            lock (_changesLock)
            {
                var result = new List<object>(_pendingOutputChanges);
                _pendingOutputChanges.Clear();
                return result;
            }
        }

        // -- Instancias ---------------------------------------------------------

        /// <summary>
        /// Devuelve la lista de instancias PLC registradas en el Runtime Manager.
        /// </summary>
        public List<object> GetInstances()
        {
            Console.WriteLine("Lllego aqui");
            if (!SimulationRuntimeManager.IsRuntimeManagerAvailable)
                throw new Exception("Runtime Manager no disponible.");

            var result = new List<object>();
            foreach (SInstanceInfo info in SimulationRuntimeManager.RegisteredInstanceInfo)
            {
                result.Add(new { info.ID, info.Name });
            }
            Console.WriteLine("Voy a devolver");
            return result;
        }

        /// <summary>
        /// Conecta el servicio a la instancia PLC con el ID indicado y actualiza la lista de tags.
        /// Arranca el hilo de detección de cambios en salidas.
        /// </summary>
        /// <param name="id">ID de la instancia registrada en el Runtime Manager.</param>
        public void Connect(int id)
        {
            lock (_lock)
            {
                // Parar hilo anterior si existía
                StopPollThread();

                _plcInstance = SimulationRuntimeManager.CreateInterface(id);
                _plcInstance.UpdateTagList();

                lock (_changesLock)
                {
                    _lastOutputValues.Clear();
                    _pendingOutputChanges.Clear();
                }
            }

            StartPollThread();
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

        // -- Hilo de detección de cambios en salidas ----------------------------

        /// <summary>
        /// Arranca el hilo de polling de salidas en background.
        /// </summary>
        private void StartPollThread()
        {
            _pollRunning = true;
            _pollThread = new System.Threading.Thread(PollLoop)
            {
                IsBackground = true,
                Name = "PlcOutputPoller"
            };
            _pollThread.Start();
        }

        /// <summary>
        /// Para el hilo de polling de salidas esperando máximo 500ms.
        /// </summary>
        private void StopPollThread()
        {
            _pollRunning = false;
            _pollThread?.Join(500);
            _pollThread = null;
        }

        /// <summary>
        /// Bucle del hilo: duerme FastIntervalMs o SlowIntervalMs según si Unity sigue consultando.
        /// </summary>
        private void PollLoop()
        {
            while (_pollRunning)
            {
                int interval = (DateTime.UtcNow - _lastClientPoll).TotalSeconds > ClientTimeoutS
                    ? SlowIntervalMs
                    : FastIntervalMs;

                System.Threading.Thread.Sleep(interval);

                if (!_pollRunning) break;

                try { DetectarCambiosSalidas(); }
                catch { }
            }
        }

        /// <summary>
        /// Lee todas las salidas de golpe, compara con el estado anterior
        /// y acumula en _pendingOutputChanges solo las que han cambiado.
        /// </summary>
        private void DetectarCambiosSalidas()
        {
            STagInfo[] outputTags;
            SDataValueByName[] signals;

            lock (_lock)
            {
                if (_plcInstance == null) return;

                outputTags = _plcInstance.TagInfos
                    .Where(t => t.Area == EArea.Output
                             && TypeNames.ContainsKey(t.PrimitiveDataType)
                             && !t.Name.StartsWith("RTG")
                             && !t.Name.StartsWith("F_SystemInfo"))
                    .ToArray();

                if (outputTags.Length == 0) return;

                signals = outputTags
                    .Select(t => new SDataValueByName { Name = t.Name })
                    .ToArray();

                _plcInstance.ReadSignals(ref signals);
            }

            // Acumular cambios de este ciclo para emitirlos por WebSocket de golpe
            List<TagChangeDto> cambiosEsteCiclo = new List<TagChangeDto>();

            lock (_changesLock)
            {
                for (int i = 0; i < outputTags.Length; i++)
                {
                    if (signals[i].ErrorCode != ERuntimeErrorCode.OK) continue;

                    string name = outputTags[i].Name;
                    string newVal;
                    try { newVal = ReadValue(outputTags[i].Name, TypeNames[outputTags[i].PrimitiveDataType]); }
                    catch { continue; }
                    TypeNames.TryGetValue(outputTags[i].PrimitiveDataType, out string typeName);

                    if (!_lastOutputValues.TryGetValue(name, out string oldVal) || oldVal != newVal)
                    {
                        _lastOutputValues[name] = newVal;

                        for (int j = _pendingOutputChanges.Count - 1; j >= 0; j--)
                        {
                            if (((TagChangeDto)_pendingOutputChanges[j]).Name == name)
                                _pendingOutputChanges.RemoveAt(j);
                        }

                        // Crear el objeto una vez y reutilizarlo en ambas listas
                        var cambio = new TagChangeDto
                        {
                            Name = name,
                            Type = typeName,
                            Value = newVal,
                            Area = "S"
                        };

                        _pendingOutputChanges.Add(cambio);   // compatibilidad REST existente
                        cambiosEsteCiclo.Add(cambio);        // para WebSocket
                    }
                }
            }

            // Emitir fuera del lock — Broadcast es thread-safe internamente
            WebSocketHandler.Broadcast(cambiosEsteCiclo);
        }

        // -- Utilidades ---------------------------------------------------------

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
            var instanciasActuales = SimulationRuntimeManager.RegisteredInstanceInfo;
            if (instanciasActuales != null)
            {
                foreach (var info in instanciasActuales)
                {
                    if (info.Name.Equals(req.Name, StringComparison.OrdinalIgnoreCase))
                        throw new Exception($"Ya existe una instancia con el nombre '{req.Name}'.");
                }
            }

            if (!string.IsNullOrEmpty(req.NetworkType))
            {
                if (req.NetworkType.ToLower() == "softbus")
                {
                    SimulationRuntimeManager.NetworkMode = ENetworkMode.Softbus;
                }
                else if (req.NetworkType.ToLower() == "tcpip")
                {
                    SimulationRuntimeManager.NetworkMode = ENetworkMode.TCPIPSingleAdapter;
                }
            }

            IInstance newInstance = SimulationRuntimeManager.RegisterInstance(ECPUType.CPU1500_SW_OC_Unspecified, req.Name);
            newInstance.PowerOn();
        }

        /// <summary>
        /// Apaga y elimina la instancia PLC con el nombre indicado.
        /// Lanza una excepción si no se encuentra ninguna instancia con ese nombre.
        /// </summary>
        public void DeleteInstance(string name)
        {
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
                throw new Exception($"No se encontró ninguna instancia activa con el nombre '{name}'.");

            IInstance instanceToDelete = SimulationRuntimeManager.CreateInterface(name);

            if (instanceToDelete.OperatingState != EOperatingState.Off)
                instanceToDelete.PowerOff();

            instanceToDelete.UnregisterInstance();
        }
    }

    public class TagChangeDto
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }
        public string Area { get; set; }
    }
}