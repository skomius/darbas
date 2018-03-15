using System;
using System.Linq;
using System.Windows.Forms;
using Elgsis.DP.Core;
using Elgsis.DP.Protocols.EsmbDi;
using Elgsis.DP.Protocols.Modbus;
using PTalk.Core;
using System.Threading;
using Elgsis.DP.Protocols.Modbus.Generic;
using System.Text;
using System.Collections.Generic;

namespace PTalk
{
    internal partial class GenericModbusDeviceProtocol : UserControl, IStreamPanel
    {
        public GenericModbusDeviceProtocol()
        {
            InitializeComponent();
        }

        public bool IsValid
        {
            get;
            set;
        }

        public bool IsEditable
        {
            get;
            set;
        }

        public string Caption
        {
            get
            {
                return "ModbusDevice";
            }
        }
        public IStreamLog Log { get; set; }

        public void Start(object stream, TimeSpan timeout, CancellationToken ct)
        {
            int slaveId = (int)numSlaveId.Value;
            var selectedTab = string.Empty;
            
            var proc = new ChannelCommandProcessor(Log);

            tabEsmbDI.Invoke((MethodInvoker)delegate {
                selectedTab = (string)tabEsmbDI.SelectedTab.Tag;
            });
            
            if (selectedTab == "RECORDS")
            {
                // var read = mbg.Read(jhjkhkhkjh
                var modbus = new RtuModbusProvider(1, new SenderReceiverSource());
                var genericModbusDevice = new GenericModbusDevice(GenericModBusDeviceHelper.ConvertFromJson(txtParameters.Text), modbus)
                var read = modBusDevice.Send(timeout, ct, readCmd);    

                proc.Process((IAsyncSenderReceiver)stream, read);

                if (read.Result.Error != null)
                {
                    Log.Error(string.Format("== Error on read received: {1} - {0}", (ModbusErrorCode)read.Result.Error, read.Result.Error));
                }
                else
                {
                    Log.Info(string.Format("== CRC OK; {0} bytes received", read.Result.Bytes.Length));
                }
            }
            else if (selectedTab == "PARAMETERS")
            {

            }
            else
            {
                throw new NotImplementedException(string.Format("Not implemented yet: {0}", selectedTab));
            }
        }
    }
}
