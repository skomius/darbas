using Elgsis.Asynchronous;
using Elgsis.DP.Core;
using Elgsis.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Elgsis.DP.Protocols.Modbus.Generic
{
    class GenericModbusDevice : IDevice
    {

        private readonly IModbusProvider modBusProvider;
        private readonly Dictionary<ParameterContext, ModBusRegistryMap> modBusRegistryMap;

        public GenericModbusDevice(ModBusRegistryMap[] modBusRegistryMap, IModbusProvider modBusProvider)
        {
            this.modBusProvider = modBusProvider ?? throw new ArgumentNullException(nameof(modBusProvider));
            this.modBusRegistryMap = modBusRegistryMap.ToDictionary(x => x.DeviceData.Parameter, x => x) ?? throw new ArgumentNullException(nameof(modBusRegistryMap));
        }

        public GenericModbusDevice(Dictionary<ParameterContext, ModBusRegistryMap> modBusRegistryMap, IModbusProvider modBusProvider)
        {
            this.modBusProvider = modBusProvider ?? throw new ArgumentNullException(nameof(modBusProvider));
            this.modBusRegistryMap = modBusRegistryMap ?? throw new ArgumentNullException(nameof(GenericModbusDevice.modBusRegistryMap));
        }

        public DeviceProperty[] Parameters
        {
            get
            {
                return modBusRegistryMap.Values.Select(x => x.DeviceData).ToArray();
            }
        }

        public AsyncMethod<DeviceCommand[], DeviceCommandResult[]> Send(TimeSpan timeout, CancellationToken ct, params DeviceCommand[] commands)
        {
            return new AsyncMethod<DeviceCommand[], DeviceCommandResult[]>(SendAsync(timeout, ct, commands), commands);
        }
        IEnumerable<IAsync> SendAsync(TimeSpan timeout, CancellationToken ct, params DeviceCommand[] commands)
        {
            var commandResultsList = new List<DeviceCommandResult>();

            foreach (var dc in commands.OfType<ReadDataCommand>())
            {
                if (!modBusRegistryMap.ContainsKey(dc.ParameterContext))
                {
                    throw new Exception($"Paramter [{dc.Param}] was not found in registry map");
                }

                var tempModBusRegistryMap = modBusRegistryMap[dc.ParameterContext];

                var read = modBusProvider.ReadAnalogOutputHoldingRegisters(
                    new ReadAOHRRequest(
                        tempModBusRegistryMap.ModBusDataDescription.Adress,
                        tempModBusRegistryMap.ModBusDataDescription.Length),
                    timeout, ct);

                yield return read;

                if (!read.Succeeded)
                    yield break;

                if (read.Result.Error != null)
                    throw new ProtocolException(
                        $"Fail to read parameter at adress {tempModBusRegistryMap.ModBusDataDescription.Adress}." +
                        $" Error code {read.Result.Error.Value}");

                var result = GetResults(tempModBusRegistryMap.DeviceData.Type, read.Result.Bytes);
                commandResultsList.Add(new DeviceCommandResult(dc, result));
            }
            yield return commandResultsList.ToArray().AsResult();
        }

        private static object GetResults(string type, byte[] bytes)
        {
            switch (type)
            {
                case "Double":
                    var valueShift = ModbusHelper.GetSingleShift(bytes, 0, ModbusSingle.HighWordFirst);
                    return valueShift.Value;
                default:
                    throw new Exception($"No data conversion to type {type}");
            }
        }
    }
}
