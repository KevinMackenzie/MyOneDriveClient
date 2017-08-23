using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage
{
    public static class ExtMath
    {
        public static T Clamp<T>(T x, T min, T max) where T : IComparable<T>
        {
            var minResult = x.CompareTo(min);
            if (minResult < 0)
                return min;
            if (minResult == 0)
                return min;

            var maxResult = x.CompareTo(max);
            if (maxResult > 0)
                return max;
            if (maxResult == 0)
                return max;

            return x;
        }
    }
}
