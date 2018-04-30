using System;
using System.Collections.Generic;
using System.Linq;
using Elgsis.DP.Protocols.Dlms.Cosem;
using Elgsis.DP.Protocols.Dlms.Cosem.IC;
using Elgsis.DP.Protocols.Asn;
using Elgsis.DP;
using Elgsis.Virtual.Device.Dlms;

namespace VirtualDevs.Device.Dlms
{
    struct GeneratorColumn
    {
        public readonly CaptureObjectDefinition Cod;
        public readonly Func<double, object> func;

        public GeneratorColumn(CaptureObjectDefinition cod, Func<double, object> func)
        {
            this.Cod = cod;
            this.func = func;
        }
    }

    class ProfileClassImitator : IAttributesHandler
    {
        private readonly Func<DateTimeOffset> timeSource;
        private readonly GeneratorColumn[] columns;
        private readonly UInt32 capturePeriod;
        private readonly UInt32 profileEntries;
        private readonly DateTimeOffset firstEntryTime;

        private readonly long capturePeriodInTicks;
        private readonly uint entriesInUse;
        private readonly int numberOfCaptures;
        private readonly DateTimeOffset lastCaptureTime;

        public ProfileClassImitator(
            Func<DateTimeOffset> timeSource,
            ITableGenerator tableGenerator,
            UInt32 capturePeriod,
            UInt32 profileEntries,
            DateTimeOffset firstEntryTime
            )
        {
            this.columns = columns ?? throw new ArgumentNullException(nameof(columns)); //ITableGenerator
            this.timeSource = timeSource ?? throw new ArgumentNullException(nameof(timeSource));
            this.profileEntries = profileEntries;
            this.capturePeriod = capturePeriod;
            this.firstEntryTime = firstEntryTime;

            this.capturePeriodInTicks = TimeSpan.FromSeconds(capturePeriod).Ticks;
            this.numberOfCaptures = CalcNumOfCaptures(capturePeriodInTicks, timeSource);
            this.lastCaptureTime = CalcLastCaptureTime(numberOfCaptures, capturePeriodInTicks, timeSource);
            this.entriesInUse = CalcEntriesInUse(timeSource(), lastCaptureTime, capturePeriod);
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

            foreach( var col in columns)
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
            ValidateSelectiveAccess(access);

            var entry = (EntryDescriptor)access.Value.Descriptor;

            if (entry.FromEntry == 0)
                return DataCoder.Encode(new Data[] { });

            
            var result = new List<Data[]>();

            var capturePeriodInTicks = TimeSpan.FromSeconds(capturePeriod).Ticks;

            var numberOfCaptures = (int)((timeSource().Subtract(firstEntryTime).TotalSeconds) / capturePeriod);
            numberOfCaptures = numberOfCaptures - (int)entry.FromEntry + 1;
            var lastCaptureTime = new DateTimeOffset(numberOfCaptures * capturePeriodInTicks, timeSource().Offset);

            for (int i = 0; i <= entry.ToEntry - entry.FromEntry; i++)
            {
                result.Add( 
                    Enumerable.Range(0, columns.Length + 1)
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
                var value = columns[column - 1].func(index);
                return DataCoder.Encode(value);
            }
        }

        private void ValidateSelectiveAccess(SelectiveAccess? access)
        {
            if (!access.HasValue)
                throw new NotImplementedException();

            if (access.Value.Index != 1)
                throw new NotImplementedException();
        }

        // calculates number of captures from first capture
        private int CalcNumOfCaptures( long capturePeriodInTicks, Func<DateTimeOffset> timeSource)
        {
            var numberOfCaptures = (int)((timeSource().Subtract(firstEntryTime).TotalSeconds) / capturePeriod);
            return numberOfCaptures;
        }

        private DateTimeOffset CalcLastCaptureTime( int numberOfCaptures, long capturePeriodInTicks, Func<DateTimeOffset> timeSource)
        {
            return new DateTimeOffset(numberOfCaptures * capturePeriodInTicks, timeSource().Offset);
        }

        private uint CalcEntriesInUse( DateTimeOffset lastCapture, DateTimeOffset firstCapture, uint capturePeriod)
        {
            var diff = lastCapture - firstCapture;
            return (uint)diff.TotalSeconds / capturePeriod + 1; 
        }
    }
}
