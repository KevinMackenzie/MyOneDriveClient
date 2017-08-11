using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contracts.Exceptions
{
    public class FactoryNotFoundException : Exception
    {
        public FactoryNotFoundException(string serviceName) : base(
            $"Could not find remove service with the name \"{serviceName}\"")
        {
        }
    }
}
