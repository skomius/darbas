using Elgsis.DP.Protocols.Asn;
using Elgsis.DP.Protocols.Dlms.Cosem;
using Elgsis.DP.Protocols.Dlms.Cosem.IC;
using System;
using System.Collections.Generic;
using System.Linq;
using VirtualDevs.Device.Dlms;

namespace Elgsis.Virtual.Device.Dlms
{
    //class BillingDataResult
    //{
    //    DateTime firstCaptureTime;
    //    TimeSpan capturePeriod;

    //    public BillingDataResult(DateTime firstTimeCapture, TimeSpan capturePeriod, int numberOfCaptures, int entriesInUse)
    //    {

    //    }

    //    public Data GenerateTable(EntryDescriptor entry)
    //    {
    //        return new Data();
    //    }

    //    public uint GetEntriesInUse()
    //    {
    //        return 0;
    //    }
    //}

    class BillingTable2 : BillingTable
    {
        public BillingTable2(GeneratorColumn[] generatorColumns) : base(generatorColumns)
        {
            
        }

    }

    class BillingTable : ITableGenerator
    {
        private readonly GeneratorColumn[] generatorColumns;

        public GeneratorColumn[] GeneratorColumns { get { return generatorColumns; } }

        public BillingTable(GeneratorColumn[] generatorColumns)
        {
            this.generatorColumns = generatorColumns ?? throw new ArgumentException(nameof(generatorColumns));
        }

        public Data GenerateTable(EntryDescriptor entry, DateTime timeSource, DateTime firstCaptureTime, TimeSpan capturePeriod, UInt32 profileEntries)
        {
            var result = new List<Data[]>();

            var lastCaptureDate = GetLastCaptureDate(timeSource, firstCaptureTime, capturePeriod);
            
            var entriesInUse = GetEntriesInUse(timeSource, firstCaptureTime, capturePeriod, profileEntries);

            // new BillingDataResult((DateTime firstTimeCapture, TimeSpan capturePeriod, int numberOfCaptures, int entriesInUse)

            var index = (int)(entriesInUse - entry.ToEntry);

            if (entriesInUse < entry.FromEntry)
                return DataCoder.Encode(new Data[] { });

            var last = entry.ToEntry;
            if (entriesInUse < entry.ToEntry)
                last = entriesInUse;
            else
                firstCaptureTime = AddTime(firstCaptureTime, index, capturePeriod);

            double seconds = TimeSpan.FromTicks(firstCaptureTime.Ticks).TotalSeconds;

            for (int i = 0; i <= last - entry.FromEntry; i++)
            {
                result.Add(
                    Enumerable.Range(0, generatorColumns.Length)
                        .Select((column) => GenerateValue(column, seconds)).ToArray());

                firstCaptureTime = AddTime(firstCaptureTime, 1, capturePeriod);
                //TODO: Gal galima greiciau? 
                seconds = TimeSpan.FromTicks(firstCaptureTime.Ticks).TotalSeconds;
            }

            //Return billing data rezults
            return DataCoder.Encode(result.ToArray());
        }
        
        private Data GenerateValue(int column, double index)
        {
                var value = generatorColumns[column].func(index);
                return DataCoder.Encode(value);
        }

        public uint GetEntriesInUse(DateTime timeSource, DateTime firstCaptureTime, TimeSpan capturePeriod, UInt32 profileEntries)
        {
            uint number;

            if (timeSource < firstCaptureTime)
                return 0;

            var lastCapture = GetLastCaptureDate(timeSource, firstCaptureTime, capturePeriod);
            var diff = lastCapture - firstCaptureTime;

            if (capturePeriod.TotalSeconds != 0)
            {
                number = (uint)(diff.TotalSeconds / capturePeriod.TotalSeconds + 1);
                //Problem profile entries
                return number > profileEntries ? profileEntries : number;
            }

            number = Count(firstCaptureTime, lastCapture);

            return number > profileEntries ? profileEntries : number;                    
        }

        public DateTime GetLastCaptureDate(DateTime timeSource, DateTime firstCaptureTime, TimeSpan capturePeriod)
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

        //private DateTime SubstractTime(DateTime captureTime, int count, TimeSpan capturePeriod)
        //{
        //    if (capturePeriod.TotalHours != 0)
        //    {
        //        return captureTime.Add(-TimeSpan.FromHours(capturePeriod.TotalHours * count));
        //    }

        //    return captureTime.AddMonths(-count);
        //}

        private DateTime AddTime(DateTime captureTime, int count, TimeSpan capturePeriod)
        {
            if (capturePeriod.TotalHours != 0)
            {
                return captureTime.Add(TimeSpan.FromHours(capturePeriod.TotalHours * count));
            }

            return captureTime.AddMonths(count);
        }

        public uint Count(DateTime firstCaptureTime, DateTime lastCaptureTime)
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
