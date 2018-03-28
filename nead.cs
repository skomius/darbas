    public class AttributesHandler
    {
        private readonly Func<SelectiveAccess?, Data>[] attributes;

        public AttributesHandler(params Func<SelectiveAccess?, Data>[] attributes)
        {
            this.attributes = attributes ?? throw new ArgumentNullException();
        }

        public Func<SelectiveAccess?, Data> this[int i]
        {
            get
            {
                return attributes[i - 1];
            }
        }
    }
}

        
        public class DataGeneratorSine
        {
            Func<double> test = () => DateTimeOffset.Now.DateTime.TimeOfDay.TotalMilliseconds;

            private readonly double minValue;
            private readonly double maxValue;
            private readonly double cycleLengthInHours;
            private readonly double offsetInMilliseconds;
            private readonly Func<double> seed;
            //private readonly Func<double, Data> translate; 

            public DataGeneratorSine(Func<double> seed, double minValue, double maxValue, double cycleLengthInHours, double offsetInHours)
            {
                this.seed = seed;
                this.minValue = minValue;
                this.maxValue = maxValue;
                this.cycleLengthInHours = TimeSpan.FromHours(cycleLengthInHours).TotalMilliseconds;
                this.offsetInMilliseconds = TimeSpan.FromHours(offsetInHours).TotalMilliseconds;
            }

            public Data GenerateInt(object o)
            {
                int count;
                double temp = cycleLengthInHours;

                for (count = 0; temp > 1; count++)
                {
                    temp = temp / 10;
                }

                var offset = TimeSpan.FromHours(24).TotalMilliseconds - offsetInMilliseconds;

                var position = (offset + seed()) % cycleLengthInHours;
                

                var delta = (maxValue - minValue);
                var generatedValue = minValue + (delta * (1 + Math.Sin(Math.PI * position / Math.Pow(10, count)))) / 2;

                return new Data(Data.Field.DoubleLong, (int)generatedValue);
            }
        }

        public static Data TimeStamp(DateTimeOffset timeStamp)
        {
            

            var date = DateTimeOffset.Now.ToCosemDateTime();

            return new Data(Data.Field.DateTime, (CosemDateTime)date);
        }
    }
    
 linqpad
 int min = 10;
int max = 20;
Func<double, double> f = null;

Func<double, double> sin = (x) =>
{
	return 10 + 5 * (1 + Math.Sin(x));
};

//double g = f(Math.Sin(DateTimeOffset.Now.Minute));

f = sin;

Enumerable.Range(1, 10).Select(x => new { index = x, f = f(x)}).Dump();


struct GeneratorColumn {

        public readonly CaptureObjectDefinition Cod;
        public readonly Func<object, Data> generator;

        public GeneratorColumn(CaptureObjectDefinition cod, Func<object, Data> generator)
        {
            this.Cod = cod;
            this.generator = generator;
        }
    }

    class ProfileClassImitatorXXX: IImitateClass
    {
        private readonly ObisCode logicalName;
        //private readonly Func<SelectiveAccessDescriptor, DateTimeOffset, Func<int, IEnumerable<byte[]>>> profileBufferGenertor;
        //private readonly FakeMeterDataGenerationHelper.CaptureObject[] captureObjects;

        private readonly GeneratorColumn[] columns;


        private readonly UInt32 capturePeriod;
        private readonly UInt32 profileEntries;
        private readonly Func<DateTimeOffset> timeSource;
        //private readonly UInt32 capturePeriod;
        //private readonly Enum sortMethod;
        //private readonly object sortObject;
        //private readonly UInt32 entriesInUse;
        //private readonly UInt32 profileEntries;


        public ProfileClassImitator(
            Func<DateTimeOffset> timeSource,
            GeneratorColumn[] columns,
            ObisCode logicalName, 
            //Func<SelectiveAccessDescriptor, DateTimeOffset, Func<int, IEnumerable<byte[]>>> profileBufferGenertor,
            //FakeMeterDataGenerationHelper.CaptureObject[] captureObjects,
            UInt32 capturePeriod, 
            UInt32 profileEntries)
        {
            this.columns = columns;
            this.timeSource = timeSource;
            //this.profileBufferGenertor = profileBufferGenertor;
            this.logicalName = logicalName;
            //this.captureObjects = captureObjects;
            this.profileEntries = profileEntries;
            this.capturePeriod = capturePeriod;
        }

        public int ClassId
        {
            get
            {
                return (int)CosemClassIdEnum.ProfileGeneric;
            }
        }

        public ObisCode LogicalName
        {
            get
            {
                return logicalName;
            }
        }

        public void Update(DateTimeOffset toDate)
        {
        }

        public Data GetData(SelectiveAccess access)
        {
            if (access.Index != 1)
                throw new NotImplementedException();

            var entry = (EntryDescriptor)access.Descriptor;

            DateTimeOffset current;              // timeSource + (4 * integrationPeriod)
            int currentIndex = (int)entry.FromEntry;
            var result = new List<object[]>();

            while (currentIndex <= entry.ToEntry)
            {
                //current = current.Add(integrationPeriod)
                
                currentIndex++;
            }
            return DataCoder.Encode(result); 
        }

        public object GetAttributeValue(int attribute, SelectiveAccessDescriptor selector, DateTimeOffset meterTime)
        {
            switch (attribute)
            {
                case 2:
                    // will return data generator
                    //return profileBufferGenertor(selector, meterTime);
                case 3:
                    // will return array of capture objects
                    return GetCaptureObjects();
                case 4:
                    return capturePeriod;
                // return sort method default
                case 5:
                    return FakeMeterDataGenerationHelper.SortMethod.Fifo;
                // by default meters returns some kind an empty 
                case 6:
                    Debug.Print("Reading CAPTURE OBJECTS");
                    return new CaptureObjectDefinition(0, ObisCode.Parse("000000000000"), 0, 0);
                // entries in use (should be dynamic)
                case 7:
                    return profileEntries;
                case 8:
                    return profileEntries;
                    //return CosemGenerators.ToCosem((uint)this.dataContainer.MaxCapacity);
                default:
                    throw new NotImplementedException("attribute " + attribute);
            }
        }

        private CaptureObjectDefinition[] GetCaptureObjects()
        {
            return columns.Select(x => x.Cod).ToArray();

            //var obj = new CaptureObjectDefinition[captureObjects.Length];
            //for (var count = 0; count < captureObjects.Length; count++)
            //{
            //    obj[count] = captureObjects[count].CaptureObjectDefinition;
            //}

            //return obj;
        }
    }
}


        [Test]
        public void IfProfileClassSecondAtrributeHandlerProvided_ShouldReturnBuffer()
        {

        //    var object1 = new GeneratorColumn(new CaptureObjectDefinition(7, ObisCode.Parse("0000010000FF"), 2, 0), FakeMeterDataGenerationHelper.TimeStamp);
        //    var object2 = new GeneratorColumn(new CaptureObjectDefinition(3, ObisCode.Parse("01001F0700FF"), 2, 0), new DataGeneratorSine(() => 1, 2000, 4000, 24, 0).GenerateInt);


        //    var profileClassImitator = new Elgsis.DP.Protocols.VirtualDevices.CosemClassImitators.ProfileClassImitator( test,  );


        //    var parametersHandlers = new Dictionary<ObisCode, AttributesHandler>();
        //    parametersHandlers.Add(ObisCode.Parse("0000150007FF"), 
        //        new AttributesHandler(null, );   

        //var rawParameters = LoadParams(G3BMeterRawParameters);

        //    var handler = new VirtualDlmsMeterHandler(null, parametersHandlers, rawParameters, false);

        //    var requestCurrent = CreateRequestNormalApdu(new DlmsAttributeSelection(currentL1));

        //    var r = handler.GetxDlmsApduResponse(requestCurrent, new MacAddress(dataLink.Src), meterNr);

        //    var response = (xDlmsApdu)r;

        //    Data? d = null;
        //    DataAccessResult? dar = null;

        //    response.Switch()
        //        .Case(xDlmsApdu.Field.GetResponse, (GetResponse gr) => gr.Switch()
        //            .Case(GetResponse.Field.GetResponseNormal, (GetResponseNormal grn) => grn.Result.Switch()
        //                .Case(GetDataResult.Field.Data, (Data gdr) => d = gdr)
        //                .Case(GetDataResult.Field.DataAccessResult, (DataAccessResult dr) => dar = dr)
        //                .End())
        //            .End())
        //        .End();

        //    Debug.Write((int)d.Value.Value);
        //    Assert.AreEqual((int)DataAccessResult.Success, 0);
        //    //Assert.AreEqual((int)DataAccessResult.Success, 0);
        }
    
    
