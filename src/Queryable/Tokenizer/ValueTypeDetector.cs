using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Text;

namespace Queryable.Tokenizer
{
    internal static class ValueTypeDetector
    {
        internal static string DetectDynamicType(string value)
        {
            if (double.TryParse(value, out _))
                return value;
            else if (DateTime.TryParse(value, out _))
                return value;
            else if (Guid.TryParse(value, out _))
                return value;
            else if (bool.TryParse(value, out _))
                return value;
            else return $"\"{value}\"";

        }
    }
}