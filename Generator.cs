        public class DataGeneratorSine
        {
            private readonly double minValue;
            private readonly double maxValue;
            private readonly double cycleLengthInHours;
            private readonly double offsetInMilliseconds;
            private readonly Func<double> seed;
            private readonly Func<double, Data> translate = g => new Data(Data.Field.DoubleLong, (Int32)g);

            public DataGeneratorSine(Func<double> seed, double minValue, double maxValue, double cycleLengthInHours, double offsetInHours, Func<double, Data> translate = null)
            {
                this.seed = seed;
                this.minValue = minValue;
                this.maxValue = maxValue;
                this.cycleLengthInHours = TimeSpan.FromHours(cycleLengthInHours).TotalMilliseconds;
                this.offsetInMilliseconds = TimeSpan.FromHours(offsetInHours).TotalMilliseconds;

                if (translate != null)
                    this.translate = translate;
            }

            public Data GenerateInt(SelectiveAccess? selectiveAccess)
            {
                int count;
                double temp = cycleLengthInHours;

                //for (count = 0; temp > 1; count++)
                //{
                //    temp = temp / 10;
                //}

                var offset = TimeSpan.FromHours(24).TotalMilliseconds - offsetInMilliseconds;

                var position = (offset + seed()) % cycleLengthInHours;

                var generatedValue = minValue + ((maxValue - minValue) / 2) + ((maxValue - minValue) / 2 *
                                        Math.Sin(2 * Math.PI * position /*/ Math.Pow(10, count)*/));
                //Func<double, double> gen;
                // delta = maksimumas - minimumas;
                // sinusas = (Math.Sin(seed()) + 1) / 2;
                // minimumas + (sinusas * delta)

                return translate(generatedValue);
            }
        }
