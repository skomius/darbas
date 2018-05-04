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
    class BillingTable : ITableGenerator
    {
        private readonly GeneratorColumn[] generatorColumns;

        public GeneratorColumn[] GeneratorColumns { get; }

        public BillingTable(GeneratorColumn[] generatorColumns)
        {
            this.generatorColumns = generatorColumns ?? throw new ArgumentException(nameof(generatorColumns));
        }

        public Data GenerateTable(EntryDescriptor entry, DateTimeOffset timeSource, DateTimeOffset firstCaptureTime, TimeSpan capturePeriod)
        {
            var result = new List<Data[]>();

            var lastCaptureDate = GetLastCaptureDate(timeSource, firstCaptureTime, capturePeriod);
            int numberOfCaptures = (int)entry.FromEntry;
            
            var captureTime = SubstractTime(lastCaptureDate, (int)entry.FromEntry - 1, capturePeriod);

            for (int i = 0; i <= entry.ToEntry - entry.FromEntry; i++)
            {
                result.Add(
                    Enumerable.Range(0, generatorColumns.Length + 1)
                        .Select((column) => GenerateValue(column, numberOfCaptures, captureTime)).ToArray());

                captureTime = SubstractTime(captureTime, 1, capturePeriod);
                numberOfCaptures++;
            }

            result.Reverse();
                 
            return DataCoder.Encode(result.ToArray());
        }

        public uint GetEntriesInUse(DateTimeOffset timeSource, DateTimeOffset firstCaptureTime, TimeSpan capturePeriod)
        {
            var lastCapture = GetLastCaptureDate(timeSource, firstCaptureTime, capturePeriod);
            var diff = lastCapture - firstCaptureTime;

            if (capturePeriod.TotalHours != 0)
            {
                return (uint)(diff.TotalHours/capturePeriod.TotalHours) + 1;
            }

            return Count(firstCaptureTime, lastCapture);
        }

        private Data GenerateValue(int column, int index, DateTimeOffset captureTime)
        {
            if (column == 0)
                return DataCoder.Encode(captureTime.ToCosemDateTime());
            else
            {
                var value = generatorColumns[column - 1].func(index);
                return DataCoder.Encode(value);
            }
        }

        public DateTimeOffset GetLastCaptureDate(DateTimeOffset timeSource, DateTimeOffset firstCaptureTime, TimeSpan capturePeriod)
        {
            var time = firstCaptureTime;

            if (capturePeriod.TotalHours != 0)
            {
                var timeSpan = capturePeriod;

                while (time <= timeSource)
                   time = time.Add(timeSpan);

                return time.Add(-timeSpan);
            }

            while (time <= timeSource)
                 time = time.AddMonths(1);

            return time.AddMonths(-1);
        }

        private DateTimeOffset SubstractTime(DateTimeOffset captureTime, int count, TimeSpan capturePeriod)
        {
            if (capturePeriod.TotalHours != 0)
            {
                return captureTime.Add(-TimeSpan.FromHours(capturePeriod.TotalHours * count));
            }

            return captureTime.AddMonths(-count);
        }

        public uint Count(DateTimeOffset firstCaptureTime, DateTimeOffset lastCaptureTime)
        { 
            var i = firstCaptureTime;
            uint d = 1;

            while (i < lastCaptureTime)
            {
                i = i.AddMonths(1);
                d++;
            }

            return d;
        }
    }
}



