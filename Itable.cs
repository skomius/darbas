using Elgsis.DP.Protocols.Asn;
using Elgsis.DP.Protocols.Dlms.Cosem;
using Elgsis.DP.Protocols.Dlms.Cosem.IC;
using System;
using System.Linq;
using VirtualDevs.Device.Dlms;

namespace Elgsis.Virtual.Device.Dlms
{
    interface ITableGenerator
    {
        Data GenerateTable(EntryDescriptor entry, int numberOfCaptures, DateTimeOffset lastCaptureTime);

        GeneratorColumn[] GeneratorColumns { get; }
    }

    public class ProfileTableGen : ITableGenerator
    {
        private readonly GeneratorColumn[] generatorColumns;

        GeneratorColumn[] GeneratorColumns { get{ return generatorColumns; } }

        ProfileTableGen(GeneratorColumn[] generatorColumns, )
        {
            this.generatorColumns = generatorColumns ?? throw new ArgumentNullException(nameof(generatorColumns));
        } 

        Data GenerateTable(EntryDescriptor entry, int numberOfCaptures, DateTimeOffset lastCaptureTime, )
        {
            var result = new List<Data[]>();

            for (int i = 0; i <= entry.ToEntry - entry.FromEntry; i++)
            {
                result.Add(
                    Enumerable.Range(0, GeneratorColumns.Length + 1)
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
                var value = GeneratorColumns[column - 1].func(index);
                return DataCoder.Encode(value);
            }
        }
    }

}
