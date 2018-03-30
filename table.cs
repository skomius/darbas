 public Data GetData(SelectiveAccess? access)
        {
            if (!access.HasValue)
                throw new NotImplementedException();

            if (access.Value.Index != 1)
                throw new NotImplementedException();

            var entry = (EntryDescriptor)access.Value.Descriptor;

            if (entry.FromEntry == 0)
            {
                return DataCoder.Encode(new Data[] { });
            }

            var result = new List<Data[]>();

            var timeofEntry = timeSource().AddSeconds(capturePeriod * entry.FromEntry);

            for (int i = 0; i <= entry.ToEntry - entry.FromEntry; i++)
            {
                result.Add(Enumerable.Range(0, columns.Length + 1).Select((column) => generateValues(column, timeofEntry)).ToArray());
                timeofEntry = timeofEntry.AddSeconds(capturePeriod);
            }
            return DataCoder.Encode(result.ToArray());
        }

        private Data generateValues(int index, DateTimeOffset timeOfEntry)
        {
            if (index == 0)
                return new Data(Data.Field.DateTime, timeOfEntry.ToCosemDateTime());
            else
                return columns[index - 1].generator(null);
        }
