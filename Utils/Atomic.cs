using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage
{
    public class Atomic<T>
    {
        public Atomic(T value)
        {
            _value = value;
        }

        private readonly object _lock = "";
        private T _value;
        public T Value
        {
            get
            {
                T ret;
                lock (_lock)
                {
                    ret = _value;
                }
                return ret;
            }
            set
            {
                lock (_lock)
                {
                    _value = value;
                }
            }
        }
    }
}
