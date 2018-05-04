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
    class ProfileTable : ITableGenerator
    {
        private readonly GeneratorColumn[] generatorColumns;

        public GeneratorColumn[] GeneratorColumns { get { return generatorColumns; } }

        public ProfileTable(GeneratorColumn[] generatorColumns)
        {
            this.generatorColumns = generatorColumns ?? throw new ArgumentException(nameof(generatorColumns));
        }

        public Data GenerateTable(EntryDescriptor entry, DateTimeOffset timeSource, DateTimeOffset firstCaptureTime, TimeSpan capturePeriod)
        { 
            var result = new List<Data[]>();

            var capturePeriodInTicks = capturePeriod.Ticks;

            var numberOfCaptures = (int)((timeSource.Subtract(firstCaptureTime).TotalSeconds) / capturePeriod.TotalSeconds);
            numberOfCaptures = numberOfCaptures - (int)entry.FromEntry + 1;
            var lastCaptureTime = firstCaptureTime.Add(TimeSpan.FromTicks(numberOfCaptures * capturePeriodInTicks));

            for (int i = 0; i <= entry.ToEntry - entry.FromEntry; i++)
            {
                result.Add(
                    Enumerable.Range(0, generatorColumns.Length + 1)
                        .Select((column) => GenerateValue(column, numberOfCaptures, lastCaptureTime)).ToArray());

                lastCaptureTime = lastCaptureTime.AddTicks(-capturePeriodInTicks);
                numberOfCaptures--;
            }
            //TODO: Need optimization. Temporary.  
            result.Reverse();

            return DataCoder.Encode(result.ToArray());
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

        public uint GetEntriesInUse(DateTimeOffset timeSource, DateTimeOffset firstCaptureTime, TimeSpan capturePeriod)
        {
            var capturePeriodInTicks = capturePeriod.Ticks;
            var numberOfCaptures = (int)((timeSource.Subtract(firstCaptureTime).TotalSeconds) / capturePeriod.TotalSeconds);
            var lastCaptureTime = firstCaptureTime.Add(TimeSpan.FromTicks(numberOfCaptures * capturePeriodInTicks));

            var diff = lastCaptureTime - firstCaptureTime;

            return (uint)(diff.TotalSeconds / capturePeriod.TotalSeconds) + 1;

        }


    //    private int CalcNumOfCaptures(long capturePeriodInTicks, Func<DateTimeOffset> timeSource, DateTimeOffset firstEntryTime)
    //    {
    //        var numberOfCaptures = (int)((timeSource().Subtract(firstEntryTime).TotalSeconds) / capturePeriodInTicks);
    //        return numberOfCaptures;
    //    }

    //    private DateTimeOffset CalcLastCaptureTime(int numberOfCaptures, long capturePeriodInTicks, Func<DateTimeOffset> timeSource)
    //    {
    //        return new DateTimeOffset(numberOfCaptures * capturePeriodInTicks, timeSource().Offset);
    //    }

    //    private uint CalcEntriesInUse(DateTimeOffset lastCapture, DateTimeOffset firstCapture, uint capturePeriod)
    //    {
    //        var diff = lastCapture - firstCapture;
    //        return (uint)diff.TotalSeconds / capturePeriod + 1;
    //    }
    }
}
