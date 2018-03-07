using Elgsis.Asynchronous;
using Elgsis.DP.Core;
using Elgsis.DP.Protocols;
using Elgsis.DP.Protocols.ModBus;
using Elgsis.Parameters;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Elgsis.DP.Tests.Protocols.ModBus
{
    class GenericModBusDeviceTests
    {
        [Test]
        public void WhenModBusTemperatureMapProvided_ShouldReturnValidParameter()
        {
            var modBusMap = new ModBusRegistryMap[]
            {
                new ModBusRegistryMap(
                    new ModBusDataDescription(0,12), 
                    new DeviceData(new ParameterContext(Parameter.Temperature, Context.Instantineous), DeviceParameter.ParameterType.Double))
            };

            var modbus = new RtuModBusProvider(1, new SenderReceiverSource());
            var modBusDevice = new ModBusGenerciDevice(modBusMap, modbus);

            var parameters = modBusDevice.Parameters;

            Assert.AreEqual(1, parameters.Length);
            var temperature = parameters.Single();
            Assert.AreEqual("temperature", temperature.Name);
            // ...
        }
    }

    class ModBusGenerciDevice : IDevice
    {
        // public enum ModBusType { Tcp, Rtu };


        private readonly IModBusProvider modBusProvider;
        private readonly ModBusRegistryMap[] modBusDataDescriptions;

        public ModBusGenerciDevice(ModBusRegistryMap[] modBusDataDescriptions, IModBusProvider modBusProvider)
        {
            this.modBusProvider = modBusProvider ?? throw new ArgumentNullException(nameof(modBusProvider));
            this.modBusDataDescriptions = modBusDataDescriptions ?? throw new ArgumentNullException(nameof(modBusDataDescriptions));

            if (modBusDataDescriptions.Length == 0)
            {
                throw new ArgumentException();
            }


            //new DeviceData( new ParameterContext(Parameter.Temperature, Context.Instantineous)  DeviceProperty.ParameterType.Double)
        }


        public DeviceProperty[] Parameters => throw new NotImplementedException();

        public AsyncMethod<DeviceCommand[], DeviceCommandResult[]> Send(TimeSpan timeout, CancellationToken ct, params DeviceCommand[] commands)
        {
            return new AsyncMethod<DeviceCommand[], DeviceCommandResult[]>(SendAsync(timeout, ct, commands), commands);
        }

        IEnumerable<IAsync> SendAsync(TimeSpan timeout, CancellationToken ct, params DeviceCommand[] commands)
        {

            //var read = modbusprovider.readanalogoutputholdingregisters(new readaohrrequest());

            //yield return read;
            //if (!read.succeeded) yield break;



            yield break;
        }


    }

    class ModBusDataDescription
    {
        private readonly int adress;
        private readonly int length;

        public ModBusDataDescription(int adress, int length)
        {
            this.adress = adress;

            if (length == 0)
                throw new ArgumentException("length can't be zero");
            else
                this.length = length;
        }
        public int Adress
        {
            get { return this.adress; }
        }

        public int BusType
        {
            get { return (int)this.length; }
        }
    }

    class ModBusRegistryMap
    {
        private readonly ModBusDataDescription modBusdataDescription;
        private readonly DeviceData deviceData;
        public ModBusRegistryMap(ModBusDataDescription modBusdataDescription, DeviceData deviceData)
        {
            this.modBusdataDescription = modBusdataDescription ?? throw new ArgumentNullException(nameof(modBusdataDescription));
            this.deviceData = deviceData ?? throw new ArgumentNullException(nameof(deviceData));
        }
    }
}