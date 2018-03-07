using System;
using System.Linq;
using System.Threading;
using Elgsis.Asynchronous;
using System.Collections.Generic;
using Elgsis.DP.Core;
using Elgsis.DP.Protocols.Dlms.Cosem.IC;
using Elgsis.DP.Protocols.Dlms.Cosem;
using Elgsis.DP.Protocols.Dlms.Cosem.Services;
using Elgsis.DP.Protocols.Base.ParameterHelpers;
using Elgsis.DP.Core.RawMeterData;
using Elgsis.Parameters;
using Elgsis.DP.Protocols.Base.Profile;

namespace Elgsis.DP.Protocols.Esd
{
    class EsdNativeWrapper : IDevice
    {
        struct AttributeRef
        {
            private readonly ObisCode code;
            private readonly byte id;

            public AttributeRef(ObisCode code, byte id)
            {
                if (code == null)
                    throw new ArgumentNullException("code");

                this.code = code;
                this.id = id;
            }

            public byte Id
            {
                get
                {
                    return this.id;
                }
            }

            public ObisCode Code
            {
                get
                {
                    return this.code;
                }
            }
        }

        struct TransformInfo
        {
            private readonly ReadCommand rc;
            private readonly Func<object, object> value;

            public TransformInfo(ReadCommand rc, Func<object, object> value)
            {
                this.rc = rc;
                this.value = value;
            }

            public Func<object, object> GetValue
            {
                get
                {
                    return this.value;
                }
            }

            public ReadCommand ReadCommand
            {
                get
                {
                    return this.rc;
                }
            }
        }

        struct GetAttributeInfo
        {
            private readonly DlmsAttribute attribute;
            private readonly InvokeIdAndPriority.Flags iap;

            public GetAttributeInfo(DlmsAttribute attribute, InvokeIdAndPriority.Flags getAttributeInfo)
            {
                this.attribute = attribute;
                this.iap = getAttributeInfo;
            }

            public InvokeIdAndPriority.Flags Info
            {
                get
                {
                    return this.iap;
                }
            }

            public DlmsAttribute Attribute
            {
                get
                {
                    return this.attribute;
                }
            }
        }

        struct GetValueInfo
        {
            private readonly AttributeRef attribute;
            private readonly Func<object, object> value;
            private readonly Type returnType;

            private readonly InvokeIdAndPriority.Flags iap;

            public GetValueInfo(ObisCode code, byte attribute, Func<object, object> value, Type returnType, InvokeIdAndPriority.Flags iap = InvokeIdAndPriority.Flags.Priority | InvokeIdAndPriority.Flags.InvokeIdZero)
                : this(new AttributeRef(code, attribute), value, returnType, iap)
            {
            }

            public GetValueInfo(AttributeRef attribute, Func<object, object> value, Type returnType, InvokeIdAndPriority.Flags iap)
            {
                this.attribute = attribute;
                this.value = value;
                this.returnType = returnType;
                this.iap = iap;
            }

            public InvokeIdAndPriority.Flags Iap
            {
                get
                {
                    return this.iap;
                }
            }

            public Type ReturnType
            {
                get
                {
                    return returnType;
                }
            }

            public Func<object, object> Value
            {
                get
                {
                    return this.value;
                }
            }

            public AttributeRef Attribute
            {
                get
                {
                    return this.attribute;
                }
            }
        }

        private static readonly HashSet<ObisCode> ignoreDataClass = new HashSet<ObisCode>
        {
            Classes.ParametersCode
        };

        private static readonly Dictionary<string, GetValueInfo> classesAttributes = new Dictionary<string, GetValueInfo>
        {
            { "gsm-signal-strength", new GetValueInfo(Classes.SignalCode, 2, x => ((SignalQuality)x).Sq, typeof(sbyte)) },
            { "device-name", new GetValueInfo(Classes.FwVersionCode, 2, x => ((Version)x).Product, typeof(string)) },
            { "fw-version", new GetValueInfo(Classes.FwVersionCode, 2, x => ((Version)x).FwVersion, typeof(string)) },
            { "fw-build-time", new GetValueInfo(Classes.FwVersionCode, 2, x => ((Version)x).FwTimestamp, typeof(string)) },
            { "dev-uptime", new GetValueInfo(Classes.RebootCode2, 2, x => TimeSpan.FromSeconds((UInt32)x), typeof(TimeSpan)) },
            { "dev-uptime-v1", new GetValueInfo(Classes.RebootCode, 2, x => TimeSpan.FromSeconds((UInt32)x), typeof(TimeSpan)) },
            { "modem-technology", new GetValueInfo(Classes.ConnectionCode, 2, x => x.ToString(), typeof(TimeSpan)) },
            { "clock", new GetValueInfo(ClockClass.ClockCode, 2, x => x.ToString(), typeof(string)) },
        };

        private static readonly DeviceTableWrapper<ObisCode>[] tables = new[]
{
            new DeviceTableWrapper<ObisCode>(new DeviceTable(TableType.Log, string.Empty, "device.syslog", "RO", "Table", new FieldOption[] { }, new Constraint[] { }, string.Empty, null), Classes.SysLog),
            new DeviceTableWrapper<ObisCode>(new DeviceTable(TableType.Log, string.Empty, "device.eventlog", "RO", "Table", new FieldOption[] { }, new Constraint[] { }, string.Empty, null), Classes.EventsLog),
        };

        private static readonly HashSet<string> fingerPrintParameters = new HashSet<string>
        {
            "dc_app_fw_version",
            "dc_bl_fw_version",
            "di_manufacturer"
        };

        #region Parameters set
        private static readonly HashSet<string> parameterSetValues = new HashSet<string>
        {
            "dev_menu_lang_admin",
            "dev_menu_lang_public",
            "dev_menu_lang_super_admin",
            "dev_menu_lang_user",
            "dev_reboot_period",
            "dev_time_zone",
            "dev_ntp_time_sync_timeout",
            "dev_fw_update_mode",
            "dev_ac_fault_cluster1_url1",
            "dev_ac_fault_cluster1_url2",
            "dev_ac_fault_cluster1_url3",
            "dev_ac_fault_cluster2_url1",
            "dev_ac_fault_cluster2_url2",
            "dev_ac_fault_cluster2_url3",
            "dev_ac_fault_fix_time",
            "dev_ac_retry_fault_msg",
            "dev_ac_fault_msg_tmo",
            "dev_pnp_cluster1_url1",
            "dev_pnp_cluster1_url2",
            "dev_pnp_cluster1_url3",
            "dev_pnp_cluster2_url1",
            "dev_pnp_cluster2_url2",
            "dev_pnp_cluster2_url3",
            "dev_pnp_msg_tmo",
            "dev_pnp_retry_count",
            "in1_rq_proceed_tmo",
            "in1_baud_rate",
            "in1_data_bits",
            "in1_stop_bits",
            "in1_parity",
            "in1_byte_wait_tmo",
            "in1_menu_grant",
            "ingprs_rq_proceed_tmo",
            "ingprs_byte_wait_tmo",
            "ingprs_gprs_idle_tmo",
            "ingprs_gprs_conn_check_period",
            "ingprs_signal_ind_level1",
            "ingprs_signal_ind_level2",
            "ingprs_min_tolerated_signal_level",
            "ingprs_menu_grant",
            "out1_event_proceed_tmo",
            "out1_baud_rate",
            "out1_data_bits",
            "out1_stop_bits",
            "out1_parity",
            "out1_byte_wait_tmo",
            "out1_byte_wait_tmo",
            "out1_answ_wait_tmo",
            "out1_next_rq_pause",
            "out1_answ_buffer_size",
            "out1_menu_grant",
            "pr1_provider_code",
            "pr1_provider_name",
            "pr1_gprs_apn",
            "pr1_gprs_listen_port",
            "pr1_gprs_protocol",
            "pr1_dns_server1_ip",
            "pr1_dns_server2_ip",
            "pr1_ntp_server1_url",
            "pr1_ntp_server2_url",
            "pr1_ntp_server3_url",
            "pr1_rat_selected",
            "pr2_provider_code",
            "pr2_provider_name",
            "pr2_gprs_apn",
            "pr2_gprs_listen_port",
            "pr2_gprs_protocol",
            "pr2_dns_server1_ip",
            "pr2_dns_server2_ip",
            "pr2_ntp_server1_url",
            "pr2_ntp_server2_url",
            "pr2_ntp_server3_url",
            "pr2_rat_selected",
            "pr3_provider_code",
            "pr3_provider_name",
            "pr3_gprs_apn",
            "pr3_gprs_listen_port",
            "pr3_gprs_protocol",
            "pr3_dns_server1_ip",
            "pr3_dns_server2_ip",
            "pr3_ntp_server1_url",
            "pr3_ntp_server2_url",
            "pr3_ntp_server3_url",
            "pr3_rat_selected",
            "pr4_provider_code",
            "pr4_provider_name",
            "pr4_gprs_apn",
            "pr4_gprs_listen_port",
            "pr4_gprs_protocol",
            "pr4_dns_server1_ip",
            "pr4_dns_server2_ip",
            "pr4_ntp_server1_url",
            "pr4_ntp_server2_url",
            "pr4_ntp_server3_url",
            "pr4_rat_selected",
            "pr5_provider_code",
            "pr5_provider_name",
            "pr5_gprs_apn",
            "pr5_gprs_listen_port",
            "pr5_gprs_protocol",
            "pr5_dns_server1_ip",
            "pr5_dns_server2_ip",
            "pr5_ntp_server1_url",
            "pr5_ntp_server2_url",
            "pr5_ntp_server3_url",
            "pr5_rat_selected",
            "pr6_provider_code",
            "pr6_provider_name",
            "pr6_gprs_apn",
            "pr6_gprs_listen_port",
            "pr6_gprs_protocol",
            "pr6_dns_server1_ip",
            "pr6_dns_server2_ip",
            "pr6_ntp_server1_url",
            "pr6_ntp_server2_url",
            "pr6_ntp_server3_url",
            "pr6_rat_selected",
            "device-name",
            "fw-version",
            "ingprs_enable_time_sync",
            "dev_bat_allowed_drop",
            "dev_bat_max_time_to_charge",
            "dev_bat_check_period1",
            "dev_bat_load_time",
            // LT, PAR 
            "p1107_enable",
            "p1107_break_duration",
            "p1107_initial_baud_rate",
            "p1107_switch_baud_rate_delay",
            "p1107_answ_buffer_size",
            "p1107_session_tmo",
            "p1107_next_session_pause",
            "p1107_error_report_enable",
            "p1107_trace_enable",
            "gprs_passive_client_enabled",
            "gprs_passive_client_url1",
            "gprs_passive_client_url2",
            "gprs_passive_client_url3",
            "out2_event_proceed_tmo",
            "out2_baud_rate",
            "out2_data_bits",
            "out2_stop_bits",
            "out2_parity",
            "out2_flow_control",
            "out2_byte_wait_tmo",
            "out2_answ_wait_tmo",
            "out2_next_rq_pause",
            "out2_answ_buffer_size",
            "out2_service_answ_delay",
            "out2_menu_grant",
            "inusb_rq_proceed_tmo",
            "inusb_baudrate",
            "inusb_byte_wait_tmo",
            "inusb_menu_grant",
            "rpt_repeat_cnt",
            "rpt_repetition_tmo",
            "rpt_interval",
            "rpt_type",
            // MCL 4.x versions
            "ingprs_gprs_reboot_clip_sms_ph1",
            "ingprs_gprs_reboot_clip_sms_ph2",
            "ingprs_gprs_reboot_clip_sms_ph3",
            "ingprs_gprs_conn_check_period",
            "ingprs_gprs_conn_check_mode",
            "ingprs_gprs_conn_check_server_url1",
            "ingprs_gprs_conn_check_server_url2",
            "ingprs_gprs_conn_check_server_url3",
            "dev_ac_fault_report_ways",
            "dev_ac_fault_report_ph_no1",
            "dev_ac_fault_report_ph_no2",
            "dev_led_indication_enable",
            "dev_reboot_mode",
            "dev_reboot_time",
            "ingprs_interval_of_delayed_reg",
            "pr1_sms_allowed_ph1",
            "pr1_sms_allowed_ph2",
            "pr2_sms_allowed_ph1",
            "pr2_sms_allowed_ph2",
            "pr3_sms_allowed_ph1",
            "pr3_sms_allowed_ph2",
            "pr4_sms_allowed_ph1",
            "pr4_sms_allowed_ph2",
            "pr5_sms_allowed_ph1",
            "pr5_sms_allowed_ph2",
            "pr6_sms_allowed_ph1",
            "pr6_sms_allowed_ph2",
            //
            "csd_allow_all_incoming_calls",
            "csd_accept_phone_1",
            "csd_accept_phone_2",
            "csd_accept_phone_3",
            "csd_accept_phone_4",
            "csd_accept_phone_5",
            "csd_accept_phone_6",
            "in2_rq_proceed_tmo",
            "in2_baud_rate",
            "in2_data_bits",
            "in2_stop_bits",
            "in2_parity",
            "in2_flow_control",
            "in2_byte_wait_tmo",
            "in2_service_answ_delay",
            "in2_menu_grant",
            "in2_cl_state",
            //RF
            "rf_mode",
            "rf_rq_proceed_tmo",
            "rf_network_id",
            //"rf_node_id",
            "rf_destination_node_id",
            "rf_answer_wait_tmo",
            "rf_rx_byte_wait_tmo",
            "rf_tx_byte_wait_tmo",
            "rf_channel",
            "rf_power",
            "rf_menu_grant",
            "rf_ping_answer_scater",
        };
        #endregion 

        private static readonly HashSet<ObisCode> SimpleClasses = new HashSet<ObisCode>
        {
            "0000BE0300FF".Hex(),
            "0000BE0500FF".Hex()
        };

        //private readonly HashSet<ObisCode> simpleClasses;
        private readonly CosemDlmsProvider cosem;
        private readonly SecurityCredentials sc;
        private readonly MclDeviceTree deviceTree;

        private readonly int readPageSize;
        private const string AdminField = "dev_admin_psw";

        private readonly DeviceParameter[] parameters;

        private static Dictionary<MclDeviceTree, DeviceParameter[]> parametersCache = new Dictionary<MclDeviceTree, DeviceParameter[]>();
        private static readonly object lockObject = new object();
        private static Dictionary<string, IReadProfile> profileReadersCache = new Dictionary<string, IReadProfile>();

        public EsdNativeWrapper(SecurityCredentials sc, MclDeviceTree deviceTree, CosemDlmsProvider cosem, int pageSize = 9)
            : this(sc, deviceTree, cosem, SimpleClasses, pageSize)
        {
        }

        public EsdNativeWrapper(SecurityCredentials sc, MclDeviceTree deviceTree, CosemDlmsProvider cosem, HashSet<ObisCode> simpleClasses, int pageSize = 9)
        {
            if (cosem == null)
                throw new ArgumentNullException("cosem");

            if (simpleClasses == null)
                throw new ArgumentNullException("simpleClasses");

            //this.simpleClasses = simpleClasses;
            this.deviceTree = deviceTree;
            this.cosem = cosem;
            this.sc = sc;
            this.readPageSize = pageSize;

            if (!parametersCache.ContainsKey(deviceTree))
            {
                lock (lockObject)
                {
                    if (!parametersCache.ContainsKey(deviceTree))
                    {
                        var pc = new Dictionary<MclDeviceTree, DeviceParameter[]>(parametersCache);
                        pc[deviceTree] = GetParameters(deviceTree);
                        parametersCache = pc;
                    }
                }
            }

            this.parameters = parametersCache[deviceTree];
        }

        public DeviceProperty[] Parameters
        {
            get
            {
                return GetDeviceParameters().ToArray();
            }
        }

        private IEnumerable<DeviceProperty> GetDeviceParameters()
        {
            foreach (var p in parameters)
            {
                yield return p;
            }

            foreach (var table in tables)
            {
                yield return table.Param;
            }
        }

        public string[] FingerprintParameters
        {
            get { return fingerPrintParameters.ToArray(); }
        }

        private static DeviceParameter[] GetParameters(MclDeviceTree deviceTree)
        {
            var classDeviceParameters = new List<DeviceParameter>();

            foreach (var cls in deviceTree.DlmsClasses.Values)
            {
                foreach (var ca in classesAttributes.Where(x => x.Value.Attribute.Code == cls.LogicalName))
                {
                    var isUnique = true;

                    if (parameterSetValues.Contains(ca.Key))
                        isUnique = false;

                    classDeviceParameters.Add(new DeviceParameter(ca.Key, "RO", ca.Value.ReturnType.Name, new FieldOption[] { }, new Constraint[] { }, "", null, isUnique));
                }
            }
            //foreach (var cls in deviceTree.DlmsClasses.Values
            //                                        .OfType<DataClassBase>()
            //                                        .Where(x => !ignoreDataClass.Contains(x.LogicalName))
            //                                        .Where(x => !classesAttributes.Any(c => c.Value.Attribute.Code == x.LogicalName)))
            //{
            //    classDeviceParameters.Add(new DeviceParameter(cls.Tag, "RO", cls.Value.T.Name, new FieldOption[] { }, new Constraint[] { }, "", null));
            //}

            foreach (var cls in deviceTree.DlmsClasses.Values
                .Where(x => !ignoreDataClass.Contains(x.LogicalName))
                .Where(x => !classesAttributes.Any(c => c.Value.Attribute.Code == x.LogicalName))
                .Where(x => x.Attributes.ContainsKey(2)))
            {
                string access = "RO";

                if (deviceTree.DlmsAccessList.ContainsKey(cls.LogicalName))
                {
                    var rights = deviceTree.DlmsAccessList[cls.LogicalName];

                    if (rights.Where(x => x.Key != "factory").Select(x => x.Value).SelectMany(x => x.Attributes)
                        .Where(x => x.Key == 2)
                        .Any(x => x.Value == AttributeAccessMode.ReadAndWrite || x.Value == AttributeAccessMode.AuthenticatedReadAndWrite))

                        access = "RW";
                }

                var isUnique = true;

                if (parameterSetValues.Contains(cls.Tag))
                    isUnique = false;

                if (cls is DataClassConstrainedBase)
                {
                    var clsc = (DataClassConstrainedBase)cls;

                    classDeviceParameters.Add(new DeviceParameter(cls.Tag, access, clsc.T.Name, clsc.Options, clsc.Constraints, clsc.Units, null, isUnique));
                }
                else
                {
                    classDeviceParameters.Add(new DeviceParameter(cls.Tag, access, "object", new FieldOption[] { }, new Constraint[] { }, "", null, isUnique));
                }
            }

            return deviceTree.EsdParameters.Values
                    .Select(x => new DeviceParameter(x.Tag, x.Access, x.T.Name, x.Options, x.Constraints, x.Units, null))
                    .Union(classDeviceParameters)
                    .ToArray();
        }

        public AsyncMethod<DeviceCommand[], DeviceCommandResult[]> Send(TimeSpan timeout, CancellationToken ct, params DeviceCommand[] commands)
        {
            return new AsyncMethod<DeviceCommand[], DeviceCommandResult[]>(SendAsync(timeout, ct, commands), commands);
        }

        private IEnumerable<IAsync> SendAsync(TimeSpan timeout, CancellationToken ct, params DeviceCommand[] commands)
        {
            var allResults = new List<DeviceCommandResult>();

            #region SecurityCredentials
            if (!sc.Equals(default(SecurityCredentials)))
            {
                var paramsClass = GetParametersClass();

                var read = paramsClass.ReadParameters(new[] { AdminField }, cosem, readPageSize, timeout, ct);

                yield return read;
                if (!read.Succeeded) yield break;

                if (!sc.Password.Equals(read.Result[AdminField]))
                    throw new InvalidOperationException("Provided credentials are invalid");
            }
            #endregion

            #region ReadMetadataCommand
            var metadataCommands = commands.OfType<ReadMetadataCommand>().ToArray();
            if (metadataCommands.Length > 0)
            {
                foreach (var cmd in metadataCommands)
                {
                    var metaData = ExecuteMetaDataCommand(cmd, timeout, ct);
                    yield return metaData;

                    if (!metaData.Succeeded) yield break;

                    allResults.Add(metaData.Result);
                }
            }
            #endregion

            #region ReadTableCommand
            var tableCommands = commands.OfType<ReadTableCommand>().ToArray();
            if (tableCommands.Length > 0)
            {
                var readTable = ReadTable(tableCommands, timeout, ct);

                yield return readTable;
                if (!readTable.Succeeded)
                    yield break;

                allResults.AddRange(readTable.Result);
            }

            #endregion

            #region ReadCommand
            var readParams = commands.OfType<ReadCommand>().ToArray();

            if (readParams.Length > 0)
            {
                var readAttributes = new Dictionary<GetAttributeInfo, List<TransformInfo>>();

                foreach (var rp in readParams.ToArray())
                {
                    var getAttr = FindDlmsAttribute(rp.Param);

                    if (getAttr.HasValue)
                    {
                        var getAttrInfo = getAttr.Value.Key;
                        var transform = getAttr.Value.Value;

                        if (!readAttributes.ContainsKey(getAttrInfo))
                            readAttributes.Add(getAttrInfo, new List<TransformInfo>());

                        readAttributes[getAttrInfo].Add(new TransformInfo(rp, transform));

                        readParams = readParams.Where(x => x != rp).ToArray();
                    }
                }

                foreach (var ra in readAttributes)
                {
                    var readAttribute = ra.Key.Attribute.Get(cosem, ra.Key.Info, timeout, ct);

                    yield return readAttribute;
                    if (!readAttribute.Succeeded) yield break;

                    foreach (var rc in ra.Value)
                    {
                        var res = rc.GetValue(readAttribute.Result.GetData<object>());
                        allResults.Add(new DeviceCommandResult(rc.ReadCommand, res));
                    }
                }


                var readObsoleteParams = new List<ReadCommand>(readParams.Length);
                foreach (var rp in readParams.ToArray())
                {
                    if (!deviceTree.DlmsClassesByTag.ContainsKey(rp.Param))
                    {
                        readObsoleteParams.Add(rp);
                        continue;
                    }

                    var cls = deviceTree.DlmsClassesByTag[rp.Param];

                    if (cls.Attributes.ContainsKey(2)) // && simpleClasses.Contains(cls.Key))
                    {
                        var readAttribute = cls.Attributes[2].Get(cosem, timeout, ct);

                        yield return readAttribute;
                        if (!readAttribute.Succeeded)
                        {
                            if (!(readAttribute.Exception is ProtocolAccessException))
                                yield break;
                        }
                        else
                        {

                            if (readAttribute.Result.Succeeded)
                            {
                                object result = readAttribute.Result.GetData<object>();

                                if (cls is DataClassConstrainedBase)
                                {
                                    var dcc = (DataClassConstrainedBase)cls;

                                    if ((dcc.Factor != 1) && !(result is CosemTime) && !(result is CosemDate) && !(result is CosemDateTime))
                                        result = (Convert.ToInt32(result)) / dcc.Factor;
                                }

                                allResults.Add(new DeviceCommandResult(rp, result));
                            }
                            else
                            {
                                allResults.Add(new DeviceCommandResult(rp, readAttribute.Result.Error, false));
                            }
                        }
                    }


                }

                readParams = readObsoleteParams.ToArray();

                // Parameters packet

                if (readParams.Any())
                {
                    if (!deviceTree.DlmsClasses.ContainsKey(Classes.ParametersCode))
                        throw new InvalidOperationException("parameters class is not found");

                    var parametersClass = (ParametersClass)deviceTree.DlmsClasses[Classes.ParametersCode];

                    var read = parametersClass.ReadParameters(readParams.Select(x => x.Param).ToArray(), cosem, readPageSize, timeout, ct);

                    yield return read;
                    if (!read.Succeeded) yield break;

                    var result = readParams.Select(x =>
                    {
                        var esdParam = deviceTree.EsdParameters[x.Param];
                        var paramResult = read.Result[x.Param];

                        if (esdParam.Factor != 1)
                            paramResult = (Convert.ToInt32(paramResult)) / esdParam.Factor;

                        return new DeviceCommandResult(x, paramResult);
                    }).ToArray();

                    allResults.AddRange(result);
                }
            }

            #endregion

            #region WriteCommand
            var writeParams = commands.OfType<WriteCommand>().ToArray();

            bool rebootNeeded = false;

            foreach (var wp in writeParams.ToArray())
            {
                // Write Class parameters
                // ======================

                if (!deviceTree.DlmsClassesByTag.ContainsKey(wp.Param))
                    continue;

                var cls = deviceTree.DlmsClassesByTag[wp.Param];

                if (cls.Attributes.ContainsKey(2))
                {
                    object value = wp.Value;

                    if (cls is DataClassConstrainedBase)
                    {
                        var dcc = (DataClassConstrainedBase)cls;

                        if ((dcc.Factor != 1) && !(value is CosemTime) && !(value is CosemDate) && !(value is CosemDateTime))
                            value = (Convert.ToInt32(value)) * dcc.Factor;

                        value = Convert.ChangeType(value, dcc.T);
                    }

                    var writeAttribute = cls.Attributes[2].Set(value, cosem, InvokeIdAndPriority.Flags.InvokeIdOne | InvokeIdAndPriority.Flags.InvokeIdTwo | InvokeIdAndPriority.Flags.ServiceClass, timeout, ct);

                    yield return writeAttribute;
                    if (!writeAttribute.Succeeded)
                    {
                        if (!(writeAttribute.Exception is ProtocolAccessException))
                            yield break;
                    }
                    else
                    {
                        allResults.Add(new DeviceCommandResult(wp, writeAttribute.Result));
                        rebootNeeded = true;
                    }
                }

                writeParams = writeParams.Where(x => x != wp).ToArray();
            }

            if (writeParams.Length > 0)
            {
                // Write ESD parameters
                // ====================

                var esdWriteBlock = writeParams.Where(x => deviceTree.EsdParameters.ContainsKey(x.Param)).ToDictionary(x => deviceTree.EsdParameters[x.Param], x => x.Value);

                if (esdWriteBlock.Any())
                {
                    var writeBlockChecked = esdWriteBlock.ToDictionary(x => x.Key.Tag, x =>
                    {
                        var value = x.Value;

                        if (x.Key.Factor != 1)
                            value = (Convert.ToInt32(x.Value)) * x.Key.Factor;

                        return Convert.ChangeType(value, x.Key.T);
                    });

                    if (!deviceTree.DlmsClasses.ContainsKey(Classes.ParametersCode))
                        throw new InvalidOperationException("Parameters class was not found for write");

                    var parametersCls = (ParametersClass)deviceTree.DlmsClasses[Classes.ParametersCode];

                    var write = parametersCls.WriteParameters(writeBlockChecked, cosem, timeout, ct);

                    yield return write;
                    if (!write.Succeeded) yield break;

                    rebootNeeded = true;
                }
            }

            foreach (var pinChange in commands.OfType<PinChangeCommand>())
            {
                var simPinCode = deviceTree.DlmsClasses.ContainsKey(Classes.SimCode)
                    ? Classes.SimCode
                    : deviceTree.DlmsClasses.ContainsKey(Classes.SimCode2)
                        ? Classes.SimCode2 : new ObisCode();

                if (simPinCode == new ObisCode())
                    throw new InvalidOperationException("Sim Pin class was not found");

                var simCls = (ISimPinChange)deviceTree.DlmsClasses[simPinCode];

                var changeSimPin = simCls.ChangeSimPinInvoke(new SimPinChangeArgs(pinChange.OldValue, pinChange.NewValue), cosem, timeout, ct);

                yield return changeSimPin;
                if (!changeSimPin.Succeeded) yield break;
            }
            #endregion

            #region EsdKeyTransferCommand
            foreach (var pinChange in commands.OfType<EsdKeyTransferCommand>())
            {
                if (!deviceTree.DlmsClasses.ContainsKey(Classes.SecuritySetupCode))
                    throw new InvalidOperationException("Security-Setup class was not found");

                var securitySetup = (SecuritySetup)deviceTree.DlmsClasses[Classes.SecuritySetupCode];

                var keyTransferArgs = TranslateKeyTransfer(pinChange.ChangeKeys).ToArray();

                var transfer = securitySetup.GlobalKeyTransfer.Invoke(keyTransferArgs, cosem, timeout, ct);

                yield return transfer;
                if (!transfer.Succeeded) yield break;
            }
            #endregion

            #region RebootCommand
            if (commands.OfType<RebootCommand>().Any() || rebootNeeded)
            {
                var rebootCode = deviceTree.DlmsClasses.ContainsKey(Classes.RebootCode)
                    ? Classes.RebootCode
                    : deviceTree.DlmsClasses.ContainsKey(Classes.RebootCode2)
                        ? Classes.RebootCode2 // <-- OMFG!
                        : new ObisCode();

                if (rebootCode == new ObisCode())
                    throw new InvalidOperationException("Reboot class was not found");

                var rebootCls = (IReboot)deviceTree.DlmsClasses[rebootCode];

                var reboot = rebootCls.InvokeReboot(cosem, timeout, ct);

                yield return reboot;
                if (!reboot.Succeeded) yield break;
            }
            #endregion

            yield return allResults.ToArray().AsResult();
        }

        public AsyncMethod<Unit, DeviceCommandResult> ExecuteMetaDataCommand(ReadMetadataCommand command, TimeSpan timeout, CancellationToken ct, uint pageSize = 12)
        {
            return new AsyncMethod<Unit, DeviceCommandResult>(ExecuteMetaDataCommandAsync(command, timeout, ct, pageSize), Unit.Value);
        }

        private IEnumerable<IAsync> ExecuteMetaDataCommandAsync(ReadMetadataCommand command, TimeSpan timeout, CancellationToken ct, uint pageSize = 12)
        {
            var tableObis = tables.SingleOrDefault(x => x.Param.Name == command.Param);

            if (tableObis == null)
                throw new InvalidOperationException($"Table {command.Param} not found");

            ProfileGenericClass log = new ProfileGenericClass(tableObis.Attribute); ;

            var readEntries = log.EntriesInUse.Get(cosem, timeout, ct);

            yield return readEntries;
            if (!readEntries.Succeeded) yield break;

            var entriesInUse = readEntries.Result.GetData<UInt32>();

            var profileEntries = log.ProfileEntries.Get(cosem, timeout, ct);
            yield return profileEntries;
            if (!profileEntries.Succeeded) yield break;

            var bufferSize = profileEntries.Result.GetData<UInt32>();

            var logReader = new EventLogProcessor(log, pageSize, entriesInUse, cosem);

            profileReadersCache[command.Param] = logReader;

            var result = new DeviceCommandResult(
                command,
                new MeterReadOutMetadata(
                    RawDataType.Table,
                    RawDataFormat.LOG,
                    command.Param,
                    new object[] { new EntrySelector() },
                    new ParameterContext[] { },
                    entriesInUse,
                    bufferSize,
                    1));

            yield return result.AsResult();
        }

        public AsyncMethod<Unit, IReadProfile> CreateLogReader(ObisCode logObisCode, TimeSpan timeout, CancellationToken ct, uint pageSize = 12)
        {
            return new AsyncMethod<Unit, IReadProfile>(CreateLogReaderAsync(logObisCode, timeout, ct, pageSize), Unit.Value);
        }

        private IEnumerable<IAsync> CreateLogReaderAsync(ObisCode logObisCode, TimeSpan timeout, CancellationToken ct, uint pageSize = 12, bool readEventsLog = false)
        {
            var log = new ProfileGenericClass(logObisCode);

            var readEntries = log.EntriesInUse.Get(cosem, timeout, ct);

            yield return readEntries;
            if (!readEntries.Succeeded) yield break;

            var entriesInUse = readEntries.Result.GetData<UInt32>();

            yield return new EventLogProcessor(log, pageSize, entriesInUse, cosem).AsResult();
        }

        AsyncMethod<ReadTableCommand[], DeviceCommandResult[]> ReadTable(ReadTableCommand[] commands, TimeSpan timeout, CancellationToken ct)
        {
            return new AsyncMethod<ReadTableCommand[], DeviceCommandResult[]>(ReadTableAsync(commands, timeout, ct), commands);
        }

        IEnumerable<IAsync> ReadTableAsync(ReadTableCommand[] commands, TimeSpan timeout, CancellationToken ct)
        {
            var res = new List<DeviceCommandResult>();

            foreach (var command in commands)
            {
                if (profileReadersCache.ContainsKey(command.Param))
                {
                    var reader = profileReadersCache[command.Param];
                    var profileReader = new ProfileReader(reader, true);

                    var argument = command.Range as EntryRangeSelectArgument;

                    if (argument == null)
                        throw new NotImplementedException("Selector of type " + command.Range.GetType());

                    var read = profileReader.ReadByRangeCount(argument.From, argument.Count, timeout, ct);

                    yield return read;
                    if (!read.Succeeded) yield break;

                    var results = read.Result.Records.Select(x =>
                    {
                        var result = new List<object> { x.Time };
                        var rest = x.Value as string;
                        if (rest == null)
                            throw new InvalidOperationException("Table entry should be string");

                        return result.Concat(rest.Cast<object>()).ToArray();

                    });
                    res.Add(new DeviceCommandResult(command, results.ToArray()));
                }
                else
                {
                    throw new KeyNotFoundException($"Reader for table {command.Param} not found");
                }
            }
            yield return res.ToArray().AsResult();
        }


        private static IEnumerable<KeyDataArgs> TranslateKeyTransfer(EsdKeyInfo[] keys)
        {
            foreach (var k in keys)
            {
                var modemAccessRole = TranslateModemRole(k.SecurityLevel);
                var keyId = TranslateKeyId(k.KeyType);
                yield return new KeyDataArgs(modemAccessRole, keyId, k.Key);
            }
        }

        private static ModemAccessRoles TranslateModemRole(EsdSecurityLevels levels)
        {
            switch (levels)
            {
                case EsdSecurityLevels.UserHls:
                    return ModemAccessRoles.AccessRoleUser;
                case EsdSecurityLevels.AdminHls:
                    return ModemAccessRoles.AccessRoleAdmin;
                case EsdSecurityLevels.FwUpdateHls:
                    return ModemAccessRoles.AccessRoleFWUpdate;
            }

            throw new NotImplementedException(levels.ToString());
        }

        private static KeyId TranslateKeyId(KeyType keyType)
        {
            switch (keyType)
            {
                case KeyType.Encryption:
                    return KeyId.GlobalUnicastEncryptionKey;
                case KeyType.Authentication:
                    return KeyId.AuthenticationKey;
            }

            throw new NotImplementedException(keyType.ToString());
        }

        private ParametersClass GetParametersClass()
        {
            if (!deviceTree.DlmsClasses.ContainsKey(Classes.ParametersCode))
                throw new InvalidOperationException("parameters class was not found");

            return (ParametersClass)deviceTree.DlmsClasses[Classes.ParametersCode];
        }

        private ProfileGenericClass GetProfileClass(ObisCode profileCode)
        {
            if (!deviceTree.DlmsClasses.ContainsKey(profileCode))
                throw new InvalidOperationException($"for {profileCode} class was not found");

            return (ProfileGenericClass)deviceTree.DlmsClasses[profileCode];
        }

        private KeyValuePair<GetAttributeInfo, Func<object, object>>? FindDlmsAttribute(string param)
        {
            if (classesAttributes.ContainsKey(param))
            {
                var attributeInfo = classesAttributes[param];

                if (deviceTree.DlmsClasses.ContainsKey(attributeInfo.Attribute.Code))
                {
                    var cls = deviceTree.DlmsClasses[attributeInfo.Attribute.Code];

                    if (cls.Attributes.ContainsKey(attributeInfo.Attribute.Id))
                    {
                        return
                            new KeyValuePair<GetAttributeInfo, Func<object, object>>(
                                new GetAttributeInfo(cls.Attributes[attributeInfo.Attribute.Id], attributeInfo.Iap),
                                attributeInfo.Value);
                    }
                }
            }

            return null;
        }
    }
}
