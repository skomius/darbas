using System;
using System.Collections.Generic;
using System.Linq;
using Elgsis.DP.Protocols.Dlms.Cosem;
using Elgsis.DP.Protocols.Dlms.Cosem.IC;
using System.Diagnostics;
using Elgsis.DP.Protocols.Asn;
using Elgsis.DP;
using Elgsis.DP.Protocols.VirtualDevices.CosemClassImitators;
using static VirtualDevs.Device.Dlms.FakeMeterDataGenerationHelper;
using Elgsis.Virtual.Device.Dlms;

namespace VirtualDevs.Device.Dlms
{
    struct GeneratorColumn
    {

        public readonly CaptureObjectDefinition Cod;
        public readonly GeneratorRelativeToFunctionArg Gen;

        public GeneratorColumn(CaptureObjectDefinition cod, GeneratorRelativeToFunctionArg gen)
        {
            this.Cod = cod;
            this.Gen = gen;
        }
    }

    class ProfileClassImitator : IAttributesHandler
    {
        private readonly GeneratorColumn[] columns;
        private readonly UInt32 capturePeriod;
        private readonly UInt32 profileEntries;
        private readonly Func<DateTimeOffset> timeSource;

        public ProfileClassImitator(
            Func<DateTimeOffset> timeSource,
            GeneratorColumn[] columns,
            ObisCode logicalName,
            UInt32 capturePeriod,
            UInt32 profileEntries)
        {
            
            this.columns = columns ?? throw new ArgumentNullException(nameof(columns));
            this.timeSource = timeSource ?? throw new ArgumentNullException(nameof(timeSource)); 
            this.profileEntries = profileEntries;
            this.capturePeriod = capturePeriod;
        }

        public int ClassId => throw new NotImplementedException();

        public Func<SelectiveAccess?, Data>[] AttributesHandlers
        {
            get
            {
                return new Func<SelectiveAccess?, Data>[]
                     {
                         x => throw new NotImplementedException(),
                         GetData
                     };
            }
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


            var capturePeriodTicks = TimeSpan.FromSeconds(capturePeriod).Ticks;
            var numberOfCaptures = (int)((timeSource().Subtract(DateTimeOffset.MinValue).TotalSeconds) / capturePeriod);
            var lastCaptureTime = new DateTimeOffset(numberOfCaptures * capturePeriodTicks, timeSource().Offset);

            var capturePeriodDTOf = new DateTimeOffset(capturePeriodTicks, TimeSpan.Zero);

            for (int i = 0; i <= entry.ToEntry - entry.FromEntry; i++)
            {
                result.Add(
                    Enumerable.Range(0, columns.Length + 1)
                        .Select((column) => GenerateValue(column, numberOfCaptures, lastCaptureTime)).ToArray());

                lastCaptureTime = lastCaptureTime.AddTicks(-capturePeriodTicks);
                numberOfCaptures--;
            }
            return DataCoder.Encode(result.ToArray());
        }

        private Data GenerateValue(int column, int index, DateTimeOffset captureTime)
        {
           
            if (column == 0)
                return new Data(Data.Field.DateTime, captureTime.ToCosemDateTime());
            else
                return columns[column - 1].Gen.GenerateValueRelativeToFunctionArg(index, TimeSpan.FromSeconds(capturePeriod).TotalHours);
        }
    }
}
