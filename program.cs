using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//
using Elgsis.DP.Protocols.Dlms.Cosem.IC;
using Elgsis.DP;
using Elgsis.DP.Protocols.VirtualDevices.CosemClassImitators;
using Elgsis.DP.Protocols.Esd;
using Elgsis.DP.Core;
using System.Threading;
using Elgsis.DP.Server.Sockets;
using Elgsis.DP.Protocols.Dlms.Cosem;
using Elgsis.DP.Protocols.Asn;
using NDesk.Options;
using System.IO;
using Newtonsoft.Json;
using Elgsis.DP.Services.Histograms;
using System.Diagnostics;
using HdrHistogram;
using Newtonsoft.Json.Serialization;
using Elgsis.DP.Tests.Protocols.Asn;
using NPOI.XSSF.UserModel;
using Elgsis.DP.Protocols.Elgama.Meters.GxB;
using static Elgsis.DP.Protocols.VirtualDevices.FakeMeterDataGenerationHelper;
using Elgsis.Virtual.Device.Dlms;
using Elgsis.DP.Protocols.VirtualDevices;
using VirtualDevs.Device.Dlms;
using static VirtualDevs.Device.Dlms.FakeMeterDataGenerationHelper;

namespace VirtualDevs
{
    class MeterTypes
    {
        public const string DLMS = "DLMS";
        public const string IEC1142 = "IEC1142";
    }

    class StreamTypes
    {
        public const string SERIAL = "SERIAL";
        public const string TCPIP = "TCPIP";
    }

    class Program
    {
        private static Random randomSecond = new Random(60);
        private static Random randomImei = new Random(100);

        private static SignalQulityGenerator signalQulityGenerator = new SignalQulityGenerator();
        private static Random randomTechnologyGenerator = new Random(1);
        private static Random uptimeGenerator = new Random(2);

        //private static Dictionary<string, VirtualDevs.Device.Dlms.VirtualDlmsMeterHandler> virtualDlmsMeterHandlers = new Dictionary<string, VirtualDevs.Device.Dlms.VirtualDlmsMeterHandler>();
        //private static Dictionary<int, VirtualDevs.Device.Dlms.VirtualIEC1142MeterHandler> virtualIEC1142MeterHandlers = new Dictionary<int, VirtualDevs.Device.Dlms.VirtualIEC1142MeterHandler>();
        //private static Dictionary<ModemVersion, IDictionary<ObisCode, EsdParameter>> esdParameters = new Dictionary<ModemVersion, IDictionary<ObisCode, EsdParameter>>();
        //private static Dictionary<string, Dictionary<GxBAccessLevel, Dictionary<ObisCode, Dictionary<byte, byte[]>>>> meterRawParameters = new Dictionary<string, Dictionary<GxBAccessLevel, Dictionary<ObisCode, Dictionary<byte, byte[]>>>>();
        //private static Dictionary<string, VirtualDevs.Device.Dlms.VirtualDlmsMeterHandler> meterHandlersByModel = new Dictionary<string, VirtualDevs.Device.Dlms.VirtualDlmsMeterHandler>();

        private static readonly Type[] filter = new Type[]
           {
                typeof(AsyncReceiveBytesRoutine),
                typeof(AsyncSendBytesRoutine),
                typeof(AsyncReceiveDatagramRoutine)
           };

        private static readonly Logging log = new Logging("VirtualDevs");
        private static readonly string cfgFile = "configuration.json";
        private static Configuration cfg;

        static void Main(string[] args)
        {
            HistogramService.CreateHistogram("virtual-server-session");
            HistogramService.CreateHistogram("socket-server");
            HistogramService.CreateHistogram("creating");
            HistogramService.CreateHistogram("creating-modem");
            HistogramService.CreateHistogram("creating-meter");
            HistogramService.CreateHistogram("creating-load-modem");

            if (args.Length == 0)
            {
                if (!File.Exists(cfgFile))
                {
                    File.WriteAllText(cfgFile, JsonConvert.SerializeObject(Configuration.CreateDefault(), Formatting.Indented, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() }));
                }
                throw new Exception(string.Format("Pass cfg file as first argument. Default: {0}", cfgFile));
            }

            var configurationFile = args[0];

            Console.WriteLine(string.Format("Loading configuration: {0}", configurationFile));
            cfg = Configuration.Load(configurationFile);

            #region OPTIONS
            bool showHelp = false;
            bool plugAndPlayOnly = false;
            bool plugAndPlayEmulator = false;
            bool verbose = false;
            string meterType = "DLMS";
            string streamType = "TCPIP";
            string serialPort = cfg.SerialPort;


            var options = new OptionSet
            {
                {
                        "p|plugAndPlayOnly", "Only Plug And Play",
                         r => plugAndPlayOnly = r != null
                },
                {
                        "pe|plugAndPlayEmulator", "Plug And Play Emulator",
                         r => plugAndPlayEmulator = r != null
                },
                {
                        "v|verbose", "Verbose",
                         v => verbose = v != null
                },
                {
                        "mt|meterType=", string.Format("Update type: DLMS|IEC1142. Default: {0}", meterType),
                        (string s) => meterType = s
                },
                {
                        "st|streamType=", string.Format("Stream type: TCPIP|SERIAL. Default: {0}", streamType),
                        (string s) => streamType = s
                },
                 {
                        "sp|serialPort=", string.Format("Serial port. Default: {0}", cfg.SerialPort),
                        (string s) => serialPort = s
                },
                {
                        "h|help", "Show this message and exit",
                        h => showHelp = h != null
                },
            };

            try
            {
                options.Parse(args);
            }
            catch (OptionException e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine("Try 'ProtocolServer --help' for more information.");
                return;
            }
            if (showHelp)
            {
                ShowHelp(options);
                return;
            }
            #endregion 

            if (verbose)
            {
                log.Sync = true;
                log.Enable();
            }
            Console.WriteLine(Logging.FormatMessage("Starting..."));

            var cts = new CancellationTokenSource();

            var nodes = LoadDevices(cfg.NodesListPath);

            var parametersHandlers = new Dictionary<ObisCode, IAttributesHandler>()
            { };

            var config = PrepareDeviceConfigurations(meterType, nodes, parametersHandlers);

            var servers = CreateVirtualMetersServer(meterType, config, cts, nodes);

            foreach (var server in servers)
                server.Start();

            Console.ReadKey();
        }

        private static IEnumerable<SocketServer> CreateVirtualMetersServer(string meterType, Dictionary<string, VirtualDevs.Device.Dlms.VirtualDlmsMeterHandler> virtualDlmsMeterHandlers, CancellationTokenSource cts, SingleNodeInfo[] nodes)
        {
            Console.WriteLine(Logging.FormatMessage("Prepare device configurations"));
            //PrepareDeviceConfigurations(meterType, nodes);
            Console.WriteLine(Logging.FormatMessage($"Creating devices: {nodes.Length}"));

            foreach (var n in nodes)
            {
                log.Debug($"Device: {n}");
                var s = CreateVirtualDevicesSocketServer(n, virtualDlmsMeterHandlers, cts);
                yield return new SocketServer(n.Port, (socket) => s.ProcessStateMachine(cts.Token), 1, cfg.IgnoreUsedPorts);
            }
        }

        private static Dictionary<string, VirtualDevs.Device.Dlms.VirtualDlmsMeterHandler> PrepareDeviceConfigurations(string meterType, SingleNodeInfo[] nodes, IDictionary<ObisCode, IAttributesHandler> parametersHandlers)
        {
            var virtualDlmsMeterHandlers = new Dictionary<string, VirtualDevs.Device.Dlms.VirtualDlmsMeterHandler>();
           // var virtualIEC1142MeterHandlers = new Dictionary<int, VirtualDevs.Device.Dlms.VirtualIEC1142MeterHandler>();
            var esdParameters = new Dictionary<ModemVersion, IDictionary<ObisCode, EsdParameter>>();
            var meterRawParameters = new Dictionary<string, Dictionary<GxBAccessLevel, Dictionary<ObisCode, Dictionary<byte, byte[]>>>>();
            var meterHandlersByModel = new Dictionary<string, VirtualDevs.Device.Dlms.VirtualDlmsMeterHandler>();

            // create meter handlers

            switch (meterType)
            {
                case MeterTypes.DLMS:
                    if (meterType == MeterTypes.DLMS)
                    {

                        var map = new Dictionary<GxBAccessLevel, string>(){
                            { GxBAccessLevel.Public, "MeterDataPublicLN"},
                            { GxBAccessLevel.CollectorLn, "MeterDataCollectorLN"},
                            { GxBAccessLevel.Management, "MeterDataManagementLN"}
                        };

                        // load node parameters
                        foreach (var node in nodes)
                        {

                            var rawParameters = new Dictionary<GxBAccessLevel, Dictionary<ObisCode, Dictionary<byte, byte[]>>>();

                            var modem = new ModemConfiguration
                            {
                                DeviceId = node.ModemDeviceId,
                                FirmwareVersion = new ModemVersion(node.ModemFwVersion, node.ModemFwProduct, node.ModemFwDate, node.ModemFwTime),
                                BootLoaderVersion = new ModemVersion(node.ModemBlVersion, node.ModemBlProduct, node.ModemBlDate, node.ModemBlTime),
                                GsmModuleType = new GsmModuleType(node.ModemGsmModuleName, node.ModemGsmModuleVersion)
                            };

                            if (!esdParameters.ContainsKey(modem.FirmwareVersion))
                            {
                                esdParameters[modem.FirmwareVersion] = LoadVirtualMclDeviceTree(modem.FirmwareVersion).EsdParameters;
                            }

                            if (meterHandlersByModel.ContainsKey(node.MeterIdentification))
                            {
                                virtualDlmsMeterHandlers[node.MeterSerialNumber] = meterHandlersByModel[node.MeterIdentification];
                                continue;
                            }

                            // load Meter association trees
                            foreach (var kv in map)
                            {
                                var value = (string)node.GetType().GetProperty(kv.Value).GetValue(node);

                                if (!meterRawParameters.ContainsKey(value))
                                {
                                    if (!File.Exists(value))
                                    {
                                        throw new ValueException($"Device parameters not exist for {kv.Key} - {node} - {value}");
                                    }

                                    var desirealizedObjectTree = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<byte, string>>>(File.ReadAllText(value));

                                    var tempD = new Dictionary<ObisCode, Dictionary<byte, byte[]>>();

                                    foreach (var element in desirealizedObjectTree)
                                    {
                                        var obisCode = new ObisCode(element.Key.Hex());

                                        var tempReal = new Dictionary<byte, byte[]>();
                                        foreach (var k in element.Value)
                                        {
                                            tempReal.Add(k.Key, k.Value.Hex());
                                        }
                                        tempD.Add(obisCode, tempReal);
                                    }

                                    rawParameters.Add(kv.Key, tempD);
                                }
                                else
                                    throw new ValueException("Do something");
                            }

                            var virtualDlmsMeterHandler = new VirtualDevs.Device.Dlms.VirtualDlmsMeterHandler(
                                parametersHandlers,
                                rawParameters
                                );

                            virtualDlmsMeterHandlers[node.MeterSerialNumber] = virtualDlmsMeterHandler;
                            meterHandlersByModel[node.MeterIdentification] = virtualDlmsMeterHandler;
                        }
                    }
                    break;
                case MeterTypes.IEC1142:
                    break;
                default:
                    throw new NotImplementedException(string.Format("Meter type not supported: {0}", meterType));
            }

            return virtualDlmsMeterHandlers;
        }

        private static VirtualDevicesSocketServer CreateVirtualDevicesSocketServer(SingleNodeInfo node, Dictionary<string, VirtualDevs.Device.Dlms.VirtualDlmsMeterHandler> virtualDlmsMeterHandlers, CancellationTokenSource cts)
        {
            var senderReceiver = new SenderReceiverSource();
            var meterStateMachine = CreateMeterStateMachine(int.Parse(node.MeterSerialNumber), virtualDlmsMeterHandlers[node.MeterSerialNumber], cts.Token);
            var t = new VirtualDevicesSocketServer(senderReceiver, senderReceiver, node.Port.ToString(), meterStateMachine);

            return t;
        }

        private static VirtualDeviceStateMachine CreateModemStateMachine(int modemSerialNumber, string ip, string imei, Dictionary<ModemVersion, IDictionary<ObisCode, EsdParameter>> esdParameters, ModemConfiguration cfg, CancellationTokenSource cts)
        {
            var modemParametersHandlers = new Dictionary<ObisCode, Func<Data>>() {
                { FakeModemHandler.DcAppFwVersion, () => DataCoder.Encode(cfg.FirmwareVersion) },
                { FakeModemHandler.DcBlFwVersion, () => DataCoder.Encode(cfg.BootLoaderVersion) },
                { FakeModemHandler.DcModemSignalQuality, () => DataCoder.Encode(signalQulityGenerator.GetNewValue()) },
                { FakeModemHandler.DdConnTechnology, () => DataCoder.Encode(new Elgsis.DP.Protocols.Dlms.Cosem.Enum((byte)randomTechnologyGenerator.Next(0, 255))) },
                { FakeModemHandler.DcReboot, () => DataCoder.Encode((UInt32)uptimeGenerator.Next(0, Int32.MaxValue))},
                { FakeModemHandler.DcClock, () => DataCoder.Encode(new CosemDateTime(DateTimeOffset.Now)) },
                { FakeModemHandler.DiGsmModuleType, () => DataCoder.Encode(cfg.GsmModuleType)},
            };

            var mclDeviceInfo = new ModemDeviceMainInfo(cfg.FirmwareVersion, cfg.BootLoaderVersion, cfg.DeviceId, GetFullMclserialNumber(modemSerialNumber, cfg.DeviceId), imei, ip);
            var hmeter = HistogramService.Measure("creating-load-modem");
            var deviceTree = esdParameters[cfg.FirmwareVersion];
            hmeter.Dispose();

            return CreateFakeMclStateMachine(GetFullMclserialNumber(modemSerialNumber, cfg.DeviceId), CreateFakeMclHandler(mclDeviceInfo, deviceTree, modemParametersHandlers), cts.Token);
        }

        private static Thread PlugAndPlayServerThread(List<NodeInfo> nodesList, Tuple<string, int>[] cluster, int repeatPeriodOne, int repeatPeriodTwo, int firstRepeatCount, CancellationToken ct)
        {

            var plugAndPlayProcess = new PlugAndPlayProcess(nodesList, cluster, repeatPeriodOne, repeatPeriodTwo, firstRepeatCount, false, ct);

            return new Thread(new ThreadStart(plugAndPlayProcess.ProcessMessaging));
        }

        private static FakeModemHandler CreateFakeMclHandler(ModemDeviceMainInfo mclDeviceInfo, IDictionary<ObisCode, EsdParameter> tree, IDictionary<ObisCode, Func<Data>> parametersHandlers)
        {
            return new FakeModemHandler(mclDeviceInfo, tree, parametersHandlers);
        }

        private static VirtualDeviceStateMachine CreateMeterStateMachine(int address, VirtualDevs.Device.Dlms.VirtualDlmsMeterHandler virtualDlmsMeterHandler, CancellationToken ct)
        {
            return new VirtualDeviceStateMachine(AsyncRuntimeRunner.RunDeep((new VirtualDlmsMeter(virtualDlmsMeterHandler, address)).Listen(TimeSpan.MaxValue, ct), filter).GetEnumerator(), string.Format("METER_{0}", address));
        }

        private static VirtualDeviceStateMachine CreateFakeMclStateMachine(string number, FakeModemHandler fakeMclHandler, CancellationToken ct)
        {
            var fakeMcl = new Elgsis.DP.Protocols.VirtualDevices.FakeMcl(number, fakeMclHandler);
            return new VirtualDeviceStateMachine(AsyncRuntimeRunner.RunDeep(fakeMcl.Listen(TimeSpan.MaxValue, ct), filter).GetEnumerator(), string.Format("MCL_{0}", number));
        }

        private static NodeInfo CreateVirtualNodeEventInformation(int meterAddress, int mclAddress, string deviceId, string ip, int port, string imei)
        {
            var dtNow = DateTimeOffset.Now;

            var dt = new DateTimeOffset(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, (byte)randomSecond.Next(0, 59), dtNow.Offset);
            dt.AddMinutes(1);
            //log.Debug(dt.ToString());
            return new NodeInfo(GetFullMeterSerialNumber(meterAddress), GetFullMclserialNumber(mclAddress, deviceId), ip, port, imei, dt); ;
        }

        private static Thread StartIEC1142MeterTaskThread(string serialPort, int baudrate)
        {
            var process = new VirtualIEC1142DeviceTask(serialPort, baudrate);
            return new Thread(new ThreadStart(process.StartProcess));
        }

        private static Thread StartDlmsMeterTaskThread(string serialPort, int baudrate)
        {
            var process = new VirtualDlmsDeviceTask(serialPort, baudrate);
            return new Thread(new ThreadStart(process.StartProcess));
        }

        private static string GetFullMclserialNumber(int address, string deviceId)
        {
            return string.Format("SIS{0}{1}", deviceId, address.ToString("D8"));
        }

        private static string GetFullMeterSerialNumber(int address)
        {
            return string.Format("{0}", address.ToString("D8"));
        }

        private static string GetRandomImei(int modemaddress, int length = 15)
        {
            const string chars = "0123456789";
            var a = GetFullMeterSerialNumber(modemaddress);

            var sb = new StringBuilder();
            sb.Append(new string(Enumerable.Repeat(chars, length - a.Length).Select(s => s[randomImei.Next(s.Length)]).ToArray()));
            sb.Append(a);
            return sb.ToString();
        }

        private static MclDeviceTree LoadVirtualMclDeviceTree(ModemVersion version)
        {
            Dictionary<string, Dictionary<ulong, Func<MclDeviceTree>>> parametersTree = MclDeviceTrees.Tree;

            //var version = new Elgsis.DP.Protocols.Esd.Version("4.1.0.371", "MCL 5 IP GR1", "Jun 15 2016", "10:11:38");
            var versionNr = EsdDeviceTreeProvider.VersionToInteger(version.FwVersion);
            var takeVersion = EsdDeviceTreeProvider.FindVersion(parametersTree[version.Product].Keys, versionNr);

            MclDeviceTree mclDeviceTree = parametersTree[version.Product][takeVersion]();

            return mclDeviceTree;
        }

        private class SignalQulityGenerator
        {
            private readonly Random rssiRandom = new Random(1);

            public SignalQulityGenerator()
            {

            }

            public Elgsis.DP.Protocols.VirtualDevices.SignalQuality GetNewValue()
            {
                var rssi = (byte)rssiRandom.Next(0, 32);
                //byte rssi = 32;
                var dbm = (sbyte)(rssi * 2 - 113);
                return new Elgsis.DP.Protocols.VirtualDevices.SignalQuality(rssi, dbm);
            }
        }

        private static List<NodeInfo> GetNodesList(string fileName)
        {

            var nodesList = new List<NodeInfo>();
            string line = "";
            int c = 0;
            try
            {
                using (var file = new StreamReader(fileName))
                {
                    while ((line = file.ReadLine()) != null)
                    {
                        c++;
                        line = line.Replace(Environment.NewLine, "").Replace(" ", "");

                        if (line == "")
                            continue;

                        var p = line.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                        //Console.WriteLine(string.Format("\"{0}\"", line));
                        nodesList.Add(CreateVirtualNodeEventInformation(int.Parse(p[4]), int.Parse(p[0]), p[1], p[2], int.Parse(p[3]), p[5]));
                    }
                }
            }
            catch (Exception ex)
            {

                Console.WriteLine(Logging.FormatMessage(string.Format("EXCEPTION: {0}: {1} - {2}", c, line, ex.ToString())));
                throw;
            }
            return nodesList;
        }

        private static SingleNodeInfo[] LoadDevices(string path)
        {
            var nodesList = new List<SingleNodeInfo>();
            var header = new string[] { };
            var rows = new List<string[]>();

            using (var f = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                var sheet = new XSSFWorkbook(f).GetSheet("sheet1");

                //for (int row=0; row <= sheet.LastRowNum; row++)
                for (int row = 0; row <= sheet.LastRowNum; row++)
                {
                    //Console.WriteLine($"{sheet.GetRow(row).GetCell(0).StringCellValue}");
                    var r = sheet.GetRow(row).Cells.Select(x => x.ToString()).ToArray();
                    if (header.Length == 0)
                        header = r;
                    else
                        rows.Add(r);
                }
            }

            foreach (var r in rows)
            {
                var node = new SingleNodeInfo();
                for (var i = 0; i < header.Length; i++)
                {
                    var prop = node.GetType().GetProperty(header[i]);
                    prop.SetValue(node, Convert.ChangeType(r[i], prop.PropertyType), null);
                }
                nodesList.Add(node);
            }
            return nodesList.ToArray();
        }

        static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("Usage: ProtocolServer [-l listen port] [-p proxy port start] [-c proxy port count]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }
    }
}
