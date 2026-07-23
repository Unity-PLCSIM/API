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

        public void Connect(int id)
        {
            lock (_lock)
            {
                _plcInstance = SimulationRuntimeManager.CreateInterface(id);
                _plcInstance.UpdateTagList();
            }
        }

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

        private static bool ParseBool(string raw)
        {
            switch (raw.Trim().ToLowerInvariant())
            {
                case "1": case "true": case "on": case "si": case "sí": return true;
                case "0": case "false": case "off": case "no": return false;
                default: return bool.Parse(raw);
            }
        }

        private static string FormatValue(object value)
        {
            if (value is float f) return f.ToString(CultureInfo.InvariantCulture);
            if (value is double d) return d.ToString(CultureInfo.InvariantCulture);
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}