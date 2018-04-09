using Elgsis.DP.Protocols.Dlms.Cosem;
using System;
using System.Collections.Generic;
using Elgsis.DP.Protocols.Dlms.Cosem.IC;
using Elgsis.DP.Protocols.Asn;
using System.Linq;
using System.Diagnostics;

namespace VirtualDevs.Device.Dlms
{
    static class FakeMeterDataGenerationHelper
    {
        public enum SortMethod
        {
            Fifo,
            Lifo,
            Largest,
            Smallest,
            NearestToZero,
            FarestFromZero
        }

        public struct CaptureObject
        {
            private readonly CaptureObjectDefinition captureObjectDefinition;
            private readonly Func<object, UInt32, Func<object>, object> generator;
            private readonly object startValue;
            private readonly Func<object> offset;

            public CaptureObject(
                CaptureObjectDefinition captureObjectDefinition,
                object startValue,
                Func<object, UInt32, Func<object>, object> generator,
                Func<object> offset)
            {
                this.captureObjectDefinition = captureObjectDefinition;
                this.startValue = startValue;
                this.generator = generator;
                this.offset = offset;
            }

            public CaptureObjectDefinition CaptureObjectDefinition
            {
                get
                {
                    return this.captureObjectDefinition;
                }
            }

            public Func<object, UInt32, Func<object>, object> Generator
            {
                get
                {
                    return this.generator;
                }
            }

            public object StatrtValue
            {
                get
                {
                    return this.startValue;
                }
            }

            public Func<object> Offset
            {
                get
                {
                    return this.offset;
                }
            }

        }

        public static class ValuesGenerators
        {
            public static CosemDateTime TimeStamp(DateTimeOffset timeStamp, Func<object, object> offset)
            {
                return new CosemDateTime(
                        new CosemDate((ushort)timeStamp.Year, (byte)timeStamp.Month, (byte)timeStamp.Day, (byte)timeStamp.DayOfWeek),
                        new CosemTime((byte)timeStamp.Hour, (byte)timeStamp.Minute, (byte)timeStamp.Second));
            }

            public static UInt32 EnergyValue(DateTimeOffset timeStamp, Func<object, object> offset)
            {
                //var ticks = timeStamp.UtcTicks;
                var ticks = 0x438;
                return (UInt32)ticks;
            }

            public static UInt64 AverageValue(DateTimeOffset timeStamp, Func<object, object> offset)
            {
                return (UInt64)0x599819;
                //return (UInt64)timeStamp.UtcTicks;
            }

            public static Int16 InstantsValue(DateTimeOffset timeStamp, Func<object, object> offset)
            {
                return (Int16)0x004F;
                //return (Int16)(new Random((int)timeStamp.Ticks).Next(0, Int16.MaxValue));
            }

            public static IEnumerable<object> EnergyValue(UInt32 startValue, Func<UInt32> offset)
            {
                while (true)
                {
                    yield return startValue += offset();
                }
            }

            public static object EnergyValue(object startValue, UInt32 entryIndex, Func<object> offset)
            {
                UInt32 value = Convert.ToUInt32(startValue);

                for (UInt32 i = 1; i < entryIndex; i++)
                {
                    value += Convert.ToUInt32(offset());
                }
                return value;
            }

        }

        private class LoadProfileBufferGenertor
        {
            private readonly SelectiveAccessDescriptor selectiveAccessDescriptor;
            private readonly DateTimeOffset meterTime;
            private readonly CaptureObject[] captureObjects;
            private readonly UInt32 capturePeriod;
            private readonly UInt32 profileEntries;

            public LoadProfileBufferGenertor(
                SelectiveAccessDescriptor selectiveAccessDescriptor,
                DateTimeOffset meterTime,
                CaptureObject[] captureObjects,
                UInt32 capturePeriod,
                UInt32 profileEntries)
            {
                this.selectiveAccessDescriptor = selectiveAccessDescriptor;
                this.meterTime = meterTime;
                this.captureObjects = captureObjects;
                this.profileEntries = profileEntries;
                this.capturePeriod = capturePeriod;
            }

            public IEnumerable<byte[]> LoadProfileBufferGen(int blockSize)
            {
                var sendBuffer = new byte[blockSize];
                var strorageBuffer = new byte[] { 0x01 };

                strorageBuffer = strorageBuffer.Concat(BerCoder.EncodeLength((int)profileEntries)).ToArray();

                var rowGen = RowGenerator().GetEnumerator();

                bool available;

                while (true)
                {
                    available = false;
                    // fill storage buffer with enough bytes 
                    while (strorageBuffer.Length < sendBuffer.Length)
                    {
                        // check if any row available
                        available = rowGen.MoveNext();
                        // if available put to storage buffer
                        if (available)
                        {
                            strorageBuffer = strorageBuffer.Concat(rowGen.Current).ToArray();
                        }
                        else
                        {   // if no more bytes left to fill storage buffer - break
                            break;
                        }
                    }
                    // copy from storage buffer to send buffer
                    if (available)
                    {
                        Array.Copy(strorageBuffer, 0, sendBuffer, 0, sendBuffer.Length);
                    }
                    else
                    {
                        sendBuffer = strorageBuffer;
                    }


                    //Debug.Print(string.Format("SEND BUFFER: {0}", BitConverter.ToString(sendBuffer)));
                    yield return sendBuffer;

                    if (!available)
                    {
                        break;
                    }

                    // create tmp buffer with size of bytes left to send 
                    var tmpBuffer = new byte[strorageBuffer.Length - sendBuffer.Length];
                    // copy bytes left to send to tmp buffer
                    Array.Copy(strorageBuffer, sendBuffer.Length, tmpBuffer, 0, strorageBuffer.Length - sendBuffer.Length);
                    strorageBuffer = tmpBuffer;

                    // clear buffer
                    sendBuffer = new byte[blockSize];
                }
            }

            public IEnumerable<byte[]> RowGenerator()
            {
                //var dt  = new DateTimeOffset(meterTime.Year, meterTime.Month, meterTime.Day, meterTime.Hour, meterTime.Minute, meterTime.Second, meterTime.Millisecond, meterTime.Offset);
                var dt = new DateTimeOffset(meterTime.Year, meterTime.Month, meterTime.Day, meterTime.Hour, meterTime.Minute, 0, 0, meterTime.Offset);

                // return new row for each entry
                for (UInt32 i = 0; i < profileEntries; i++)
                {
                    var objectList = new List<object>();
                    foreach (CaptureObject captureObject in captureObjects)
                    {
                        objectList.Add(captureObject.Generator(captureObject.StatrtValue, i, captureObject.Offset));

                        //if (captureObject.DataType == typeof(DateTimeOffset))
                        //{
                        //    objectList.Add(ValuesGenerators.TimeStamp(dt, (x) => x));
                        //}
                        //else if (captureObject.DataType == typeof(LoadProfileType))
                        //{
                        //    objectList.Add(LoadProfileType.Planned());
                        //}
                        //else if (captureObject.DataType == typeof(UInt32))
                        //{
                        //    objectList.Add(ValuesGenerators.EnergyValue(dt, (x) => x));
                        //}
                        //else if (captureObject.DataType == typeof(UInt64))
                        //{
                        //    objectList.Add(ValuesGenerators.AverageValue(dt, (x) => x));
                        //}
                        //else if (captureObject.DataType == typeof(Int16))
                        //{
                        //    objectList.Add(ValuesGenerators.InstantsValue(dt, (x) => x));
                        //}
                        //else
                        //{
                        //    throw new NotImplementedException("Capture object type not supported yet");
                        //}
                    }

                    dt -= TimeSpan.FromSeconds(capturePeriod);
                    var d = DataCoder.Encode(new DataStructure(objectList.ToArray()));

                    var e = AxdrCoder.Encode(d);
                    //Debug.Print(string.Format("ROW: {0} - {1}", i, BitConverter.ToString(e)));
                    yield return e;
                }

            }

            private DateTimeOffset GetCurrentIntegrationPeriod()
            {
                return new DateTimeOffset();
            }
        }

        public static class Generators
        {
            public static Func<double, double, double> ScaledGen(double maxValue, double minValue, double cycleLengthInHours, Func<double, double> f)
            {
                return (x, y) => {

                    double delta = maxValue - minValue;
                    double relate = capturePeriodInHours / cycleLengthInHours;
                    double x = relate * valueIndex;

                    var generatedValue = minValue + delta * (function(x));

                    return new Data(Data.Field.DoubleLong, (int)generatedValue);
                };
            }

            public static Func<double, double> Sin = (x) => (1 + Math.Sin(x * Math.PI * 2)) / 2;
        }

        public class LoadProfileGenerator
        {
            private readonly CaptureObject[] captureObjects;
            private readonly UInt32 capturePeriod;
            private readonly UInt32 profileEntries;

            public LoadProfileGenerator(CaptureObject[] captureObjects, UInt32 capturePeriod, UInt32 profileEntries)
            {
                this.captureObjects = captureObjects;
                this.profileEntries = profileEntries;
                this.capturePeriod = capturePeriod;
            }

            public Func<int, IEnumerable<byte[]>> GetLoadProfileGeneratorCallback(SelectiveAccessDescriptor selectiveAccessDescriptor, DateTimeOffset meterTime)
            {
                LoadProfileBufferGenertor gen = new LoadProfileBufferGenertor(
                    selectiveAccessDescriptor,
                    meterTime,
                    captureObjects,
                    capturePeriod,
                    profileEntries);

                return gen.LoadProfileBufferGen;
            }
        }

        public static Data GenerateValueRelativeToDayTime(double minValue, double maxValue, double cycleLengthInHours, DateTimeOffset seed)
        {
            double cycleLengthInMili = TimeSpan.FromHours(cycleLengthInHours).TotalMilliseconds;

            var position = seed.TimeOfDay.TotalMilliseconds % cycleLengthInMili;

            int count;
            double temp = cycleLengthInMili;
            for (count = 0; temp > 1; count++)
            {
                temp = temp / 10;
            }
            var fractionGuaranteed = position / Math.Pow(10, count);

            var sin = Math.Sin(Math.PI * fractionGuaranteed * 2);

            var delta = (maxValue - minValue);
            var generatedValue = minValue + (delta * (1 + sin)) / 2;

            return new Data(Data.Field.DoubleLong, (int)generatedValue);
        }

        public static Data GenerateValueRelativeToFunctionArg(double minValue, double maxValue, double cycleLengthInHours,
                                                                int capturePeriodInHours, int valueIndex, Func<double, double> function)
        {
            double delta = maxValue - minValue;
            double relate = capturePeriodInHours / cycleLengthInHours;
            double x = relate * valueIndex;

            var generatedValue = minValue + delta * (function(x));

            return new Data(Data.Field.DoubleLong, (int)generatedValue);
        }

        public static Func<double, double> Sin = (x) => (1 + Math.Sin(x * Math.PI * 2)) / 2;

        public class GeneratorRelativeToFunctionArg
        {
            private readonly double minValue;
            private readonly double maxValue;
            private readonly double cycleLengthInHours;
            private readonly Func<double, double> function;

            public GeneratorRelativeToFunctionArg(double minValue, double maxValue, double cycleLengthInHours, Func<double, double> function)
            {
                this.minValue = minValue;
                this.maxValue = maxValue;

                if (cycleLengthInHours != 0)
                    this.cycleLengthInHours = cycleLengthInHours;
                else
                    throw new ArgumentException();

                this.function = function;
            }

            public Data GenerateValueRelativeToFunctionArg( int valueIndex, double capturePeriodInHours)
            {
                double delta = maxValue - minValue;
                double relate = capturePeriodInHours / cycleLengthInHours;
                double x = relate * valueIndex;

                var generatedValue = minValue + delta * (function(x));

                return new Data(Data.Field.DoubleLong, (int)generatedValue);
            }

        }

        public class InstantaneousDataGenerator
        {
            private readonly double minValue;
            private readonly double maxValue;
            private readonly long cycleLengthInTicks;
            private readonly DateTimeOffset meterStart;
            private readonly Func<double, double> func;
            private readonly Func<DateTimeOffset> seed;

            public InstantaneousDataGenerator(double minValue, double maxValue, double cycleLengthInHours,
                                        DateTimeOffset meterStart, Func<double, double> func,
                                            Func<DateTimeOffset> seed)
            {
                this.minValue = minValue;
                this.maxValue = maxValue;

                if (cycleLengthInHours != 0)
                    this.cycleLengthInTicks = TimeSpan.FromHours(cycleLengthInHours).Ticks;
                else
                    throw new ArgumentException();

                this.meterStart = meterStart;
                this.func = func;
                this.seed = seed;
            }

            public Data GenerateDouble(SelectiveAccess? selectiveAccess)
            {
                var fraction = 1 / (double)cycleLengthInTicks;

                var multiplier = seed().Subtract(meterStart).Ticks;

                var x = fraction * multiplier;

                var delta = maxValue - minValue;

                var generatedValue = minValue + delta * func(x);

                return new Data(Data.Field.DoubleLong, (int)generatedValue);
            }
        }

    }
}


