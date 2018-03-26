using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            Debug.Write(GenerateSin(10, 20, 24, 6));
        }

        static double GenerateSin( double min, double max, double periodInHours, double offsetInHours)
        {

            var miliSeconds = TimeSpan.FromHours(periodInHours).TotalMilliseconds;

            int count;    
            for (count = 0; miliSeconds > 1; count++)
            {
                miliSeconds = miliSeconds / 10;
            }

            DateTime e = DateTime.Now;
            var position =(TimeSpan.FromHours(offsetInHours).TotalMilliseconds + e.TimeOfDay.TotalMilliseconds) % TimeSpan.FromHours(periodInHours).TotalMilliseconds;
            var generatedValue = min + ((max - min)/2) + ((max - min) / 2 * Math.Sin(2 * Math.PI * position / Math.Pow(10, count)));

            return generatedValue;
        }


    }
}
