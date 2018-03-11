using Elgsis.Asynchronous;
using Elgsis.DP.Core;
using Elgsis.DP.Protocols;
using Elgsis.DP.Protocols.ModBus;
using Elgsis.Parameters;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using TransportAPI;



namespace Elgsis.DP.Tests.Protocols.ModBus

{

    class GenericModBusDeviceTests
    {

        [TestFixtureSetUp]

        public void SetUp()
        {
        }

        //[Test]

        //public void WhenModBusTemperatureMapProvided_ShouldReturnValidParameter()
        //{
        //    var modBusMap = new ModBusRegistryMap[]
        //    {
        //        new ModBusRegistryMap(
        //            new ModBusDataDescription(0,12),
        //            new DeviceData(new ParameterContext(Parameter.Temperature, Context.Instantineous), DeviceParameter.ParameterType.Double))
        //    };
        //    var modbus = new RtuModBusProvider(1, new SenderReceiverSource());
        //    var modBusDevice = new ModBusGenerciDevice(modBusMap, modbus);
        //    var parameters = modBusDevice.Parameters;
        //    Assert.AreEqual(1, parameters.Length);
        //    var temperature = parameters.Single();
        //    Assert.AreEqual("temperature", temperature.Name);
        //    // ...
        //}

        [Test]
        public void WhenReadTemperature_ShouldReturnResult()

        {
            var modBusMap = new ModBusRegistryMap[]
            {
                new ModBusRegistryMap(
                    new ModBusDataDescription(0, 12),
                    new DeviceData(new ParameterContext(Parameter.Temperature, Context.Instantineous), DeviceParameter.ParameterType.Double))
            };

            var modbus = new RtuModBusProvider(1, new SenderReceiverSource());
            var modBusDevice = new ModBusGenerciDevice(modBusMap, modbus);

            var read = modBusDevice.Send(TimeSpan.FromSeconds(6), CancellationToken.None, new ReadDataCommand(ParameterContext.None));

            read.GetDeepEnumerator() 
            .ShouldSendBytes("01-03-00-05-00-02-D4-0A".Hex())
            .SetReceiveBytes("01-03-04-41-C2-25-18-55-69".Hex())
            .NextEnd();

            var parser = new PacketParser(((ReadAnswer)read.Result[0].Result).Bytes);

            Debug.Print(ModBusHelper.GetSingle(parser, ModBusSingle.HighWordFirst).ToString());

            Assert.AreEqual(true, read.Result[0].Succeeded);

            //Assert.That(read.Result[0].Result, Is.EqualTo(27.17462).Within(0.0001));
        }
    }

    class ModBusGenerciDevice : IDevice

    {
        enum TemperatureHumidityMeterAddresses : int
        {
            HumidityInt = 0x0000,
            TemperatureInt = 0x0001,
            DewPoitInt = 0x0002,

            HumidityFloat = 0x0003,
            TemperatureFloat = 0x0005,
            DewPointFloat = 0x0007
        }


        private readonly IModBusProvider modBusProvider;
        private readonly ModBusRegistryMap[] modBusDataDescriptions;

        private static readonly ModBusRegistryMap[] modBusmaps =
            { new ModBusRegistryMap( new ModBusDataDescription(0, 12), new DeviceData(new ParameterContext(Parameter.Temperature, Context.Instantineous), DeviceParameter.ParameterType.Double))};

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
        // public method

        public DeviceProperty[] Parameters
        {
            get
            {
                throw new NotImplementedException();
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

                ModBusRegistryMap tempModBusRegistryMap;
                foreach (var mbr in modBusDataDescriptions)
                {
                    if ((dc.pametercontext.parameter == mbr.parametercontext.parameter) && (dc.pametercontext.context == mbr.parameter.context))
                    {
                        tempModBusRegistryMap = mbr;
                    }
                    else
                    {
                        throw new ArgumentException("Pasirinkto parametro nera")
                    }
                }

                var read = modBusProvider.ReadAnalogOutputHoldingRegisters(new ReadAOHRRequest((int)TemperatureHumidityMeterAddresses.TemperatureFloat, 2), timeout, ct);
                yield return read;

                if (!read.Succeeded)

                    yield break;

                commandResultsList.Add(new DeviceCommandResult(dc, read.Result));
            }
            yield return commandResultsList.ToArray().AsResult();
        }

        public float parse()
        {

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
