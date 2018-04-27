using Elgsis.DP;
using Elgsis.DP.Protocols.Asn;
using Elgsis.DP.Protocols.Dlms.Cosem;
using Elgsis.DP.Protocols.Dlms.Cosem.IC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VirtualDevs.Device.Dlms;

namespace Elgsis.Virtual.Device.Dlms
{
    class BillingImitator : IAttributesHandler
    {
        private readonly Func<DateTimeOffset> timeSource;
        private readonly GeneratorColumn[] columns;
        private readonly UInt32 capturePeriod = 0;
        private readonly UInt32 profileEntries;

        public BillingImitator(
            Func<DateTimeOffset> timeSource,
            GeneratorColumn[] columns,
            UInt32 capturePeriod,
            UInt32 profileEntries
            )
        {
            this.columns = columns ?? throw new ArgumentNullException(nameof(columns));
            this.timeSource = timeSource ?? throw new ArgumentNullException(nameof(timeSource));
            this.profileEntries = profileEntries;           
        }
        public Func<DateTimeOffset> TimeSource { get { return timeSource; } }

        public GeneratorColumn[] Colummns { get { return columns; } }

        public UInt32 CapturePeriod { get { return capturePeriod; } }

        public UInt32 ProfileEntries { get { return profileEntries; } }

        public int ClassId => throw new NotImplementedException();

        public Func<SelectiveAccess?, Data>[] AttributesHandlers
        {
            get
            {
                return new Func<SelectiveAccess?, Data>[]
                     {
                         x => throw new NotImplementedException(),
                         GetData,
                         GetCaptureObjects,
                         GetCapturePeriod,
                         null,
                         null,
                         GetEntryInUse,
                         GetprofileEntries
                     };
            }
        }

        public Data GetCaptureObjects(SelectiveAccess? selective)
        {
            var listCapObjDef = new List<CaptureObjectDefinition>();
            listCapObjDef.Add(new CaptureObjectDefinition(8, ObisCode.Parse("0100010000FF"), 2, 0));

            foreach (var col in columns)
            {
                listCapObjDef.Add(col.Cod);
            }

            return DataCoder.Encode(listCapObjDef.ToArray());
        }

        public Data GetCapturePeriod(SelectiveAccess? selective)
        {
            return DataCoder.Encode(capturePeriod);
        }

        public Data GetprofileEntries(SelectiveAccess? selective)
        {
            return DataCoder.Encode(profileEntries);
        }

        public Data GetEntryInUse(SelectiveAccess? selective)
        {
            return DataCoder.Encode(profileEntries);
        }

        public Data GetData(SelectiveAccess? access)
        {
            if (!access.HasValue)
                throw new NotImplementedException();

            if (access.Value.Index != 1)
                throw new NotImplementedException();

            var entry = (EntryDescriptor)access.Value.Descriptor;

            if (entry.FromEntry == 0)
                return DataCoder.Encode(new Data[] { });


            var result = new List<Data[]>();

            var today = DateTimeOffset.Now;
            var lastCaptureDate = DateTimeOffset.Parse($"{today.Month}/1/{today.Year} 00:00:00 AM");
            int numberOfCaptures = (int)entry.FromEntry; 

            var captureDateByIndex = lastCaptureDate.AddMonths((int)-entry.FromEntry + 1);

            for (int i = 0; i <= entry.ToEntry - entry.FromEntry; i++)
            {
                result.Add(
                    Enumerable.Range(0, columns.Length + 1)
                        .Select((column) => GenerateValue(column, numberOfCaptures, captureDateByIndex)).ToArray());

                captureDateByIndex = captureDateByIndex.AddMonths(-1);
                numberOfCaptures++;
            }
            return DataCoder.Encode(result.ToArray());
        }

        private Data GenerateValue(int column, int index, DateTimeOffset captureTime)
        {
            if (column == 0)
                return DataCoder.Encode(captureTime.ToCosemDateTime());
            else
            {
                var value = columns[column - 1].func(index);
                return DataCoder.Encode(value);
            }
        }
    }
}
