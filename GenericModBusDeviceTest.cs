using Elgsis.DP.Core;
using Elgsis.DP.Protocols;
using Elgsis.DP.Protocols.Modbus;
using Elgsis.DP.Protocols.Modbus.Generic;
using Elgsis.Parameters;
using NUnit.Framework;
using System;
using System.Threading;

namespace Elgsis.DP.Tests.Protocols.Modbus
{
    class GenericModBusDeviceTests
    {
        [Test]
        public void WhenMapPassed_ShouldReturnMappedParameters()
        {
            var modBusMap = new ModBusRegistryMap[]
            {
                new ModBusRegistryMap(
                    new ModBusDataDescription(0x005, 2),
                    new DeviceData(new ParameterContext(Parameter.Temperature, Context.Instantineous), DeviceParameter.ParameterType.Double))
            };

            var modbus = new RtuModbusProvider(1, new SenderReceiverSource());
            var modBusDevice = new GenericModbusDevice(modBusMap, modbus);

            Assert.AreEqual(1, modBusDevice.Parameters.Length);
            var deviceData = (DeviceData)modBusDevice.Parameters[0];

            Assert.AreEqual(Parameter.Temperature, deviceData.Parameter.Parameter);
            Assert.AreEqual(Context.Instantineous, deviceData.Parameter.Context);
        }

        [Test]
        public void WhenReadTemperature_ShouldReturnResult()
        {
           var modBusMap = new ModBusRegistryMap[]
           {
                new ModBusRegistryMap(
                    new ModBusDataDescription(0x005, 2),
                    new DeviceData(new ParameterContext(Parameter.Temperature, Context.Instantineous), DeviceParameter.ParameterType.Double))
           };

            var modbus = new RtuModbusProvider(1, new SenderReceiverSource());
            var modBusDevice = new GenericModbusDevice(modBusMap, modbus);

            var readCmd = new ReadDataCommand(new ParameterContext(Parameter.Temperature, Context.Instantineous));

            var read = modBusDevice.Send(TimeSpan.FromSeconds(6), CancellationToken.None, readCmd);

            read.GetDeepEnumerator()
                .ShouldSendBytes("01-03-00-05-00-02-D4-0A".Hex())
                .SetReceiveBytes("01-03-04-41-C2-25-18-55-69".Hex())
            .NextEnd();

            Assert.AreEqual(true, read.Result[0].Succeeded);
            Assert.AreEqual(true, "24.26811".Equals(read.Result[0].Result.ToString()));
        }

        [Test]
        public void WhenTwoReadCommands_ShouldReturnTowResults()
        {
            var modBusMap = new ModBusRegistryMap[]
            {
                new ModBusRegistryMap(
                    new ModBusDataDescription(0x005, 2),
                    new DeviceData(new ParameterContext(Parameter.Temperature, Context.Instantineous), DeviceParameter.ParameterType.Double))
            };

            var modbus = new RtuModbusProvider(1, new SenderReceiverSource());
            var modBusDevice = new GenericModbusDevice(modBusMap, modbus);

            var readCmd1 = new ReadDataCommand(new ParameterContext(Parameter.Temperature, Context.Instantineous));
            var readCmd2 = new ReadDataCommand(new ParameterContext(Parameter.Temperature, Context.Instantineous));

            var read = modBusDevice.Send(TimeSpan.FromSeconds(6), CancellationToken.None, readCmd1, readCmd2);

            read.GetDeepEnumerator()
                .ShouldSendBytes("01-03-00-05-00-02-D4-0A".Hex())
                .SetReceiveBytes("01-03-04-41-C2-25-18-55-69".Hex())
                .ShouldSendBytes("01-03-00-05-00-02-D4-0A".Hex())
                .SetReceiveBytes("01-03-04-41-C2-25-18-55-69".Hex())
            .NextEnd();

            Assert.AreEqual(true, read.Result[0].Succeeded);
            Assert.AreEqual(true, read.Result[1].Succeeded);
        }

        [Test]
        public void WhenEmptyRegistryMapPassed_ParametersAreEmpty()
        {
            var modBusMap = new ModBusRegistryMap[] { };

            var modbus = new RtuModbusProvider(1, new SenderReceiverSource());
            var modBusDevice = new GenericModbusDevice(modBusMap, modbus);

            Assert.AreEqual(0, modBusDevice.Parameters.Length);
        }

        [Test]
        public void WhenParameterWasNotInMap_ThrowError()
        {
            var modBusMap = new ModBusRegistryMap[] { };

            var modbus = new RtuModbusProvider(1, new SenderReceiverSource());
            var modBusDevice = new GenericModbusDevice(modBusMap, modbus);

            var readCmd = new ReadDataCommand(new ParameterContext(Parameter.Temperature, Context.Instantineous));
            var read = modBusDevice.Send(TimeSpan.FromSeconds(6), CancellationToken.None, readCmd);

            read.GetDeepEnumerator()
                .Finish();

            Assert.False(read.Succeeded);
            Assert.AreEqual("Paramter [Temperature General 89 | Context: Instantineous] was not found in registry map", read.Exception.Message);
        }

        [Test]
        public void WhenModbusReadFails_ShouldThrowError()
        {
            var modBusMap = new ModBusRegistryMap[]
            {
                new ModBusRegistryMap(
                    new ModBusDataDescription(0x00300, 2),
                    new DeviceData(new ParameterContext(Parameter.Temperature, Context.Instantineous), DeviceParameter.ParameterType.Double))
            };

            var modbus = new RtuModbusProvider(1, new SenderReceiverSource());
            var modBusDevice = new GenericModbusDevice(modBusMap, modbus);

            var readCmd1 = new ReadDataCommand(new ParameterContext(Parameter.Temperature, Context.Instantineous));

            var read = modBusDevice.Send(TimeSpan.FromSeconds(6), CancellationToken.None, readCmd1);

            read.GetDeepEnumerator()
                .ShouldSendBytes("01-03-03-00-00-02-C4-4F".Hex())
                .SetReceiveBytes("01-83-02-C0-F1".Hex())
            .NextEnd();

            
            Assert.False(read.Succeeded); 
            Assert.AreEqual("Fail to read parameter at adress 768. Error code 2", read.Exception.Message);
        }

        [Test]
        public void ConvertFromJson()
        {
            var str = @"[
	          {
	            ""modBusDataDescription"": {
	              ""adress"": 5,
	              ""length"": 2
	            },
	            ""deviceData"": {
	              ""parameter"": ""Temperature"",
	              ""context"": ""Instantineous"",
	              ""type"": ""Double""
	            }
	          },
	          {
	            ""modBusDataDescription"": {
	              ""adress"": 8,
	              ""length"": 2
	            },
	            ""deviceData"": {
	              ""parameter"": ""Humidity"",
	              ""context"": ""Instantineous"",
	              ""type"": ""Double""
	            }
	          }]";

            var mBRMArray = GenericModBusDeviceHelper.ConvertFromJson(str);

            Assert.AreEqual(2, mBRMArray.Length);

            Assert.AreEqual(5, mBRMArray[0].ModBusDataDescription.Adress);
            Assert.AreEqual(8, mBRMArray[1].ModBusDataDescription.Adress);

            Assert.AreEqual(2, mBRMArray[0].ModBusDataDescription.Length);
            Assert.AreEqual(2, mBRMArray[0].ModBusDataDescription.Length);

            Assert.AreEqual("Temperature", mBRMArray[0].DeviceData.Parameter.Parameter.Name);
            Assert.AreEqual("Humidity", mBRMArray[1].DeviceData.Parameter.Parameter.Name);

            Assert.AreEqual("Instantineous", mBRMArray[0].DeviceData.Parameter.Context.Name);
            Assert.AreEqual("Instantineous", mBRMArray[1].DeviceData.Parameter.Context.Name);

            Assert.AreEqual("Double", mBRMArray[0].DeviceData.Type);
            Assert.AreEqual("Double", mBRMArray[1].DeviceData.Type);
        }
    }
}
