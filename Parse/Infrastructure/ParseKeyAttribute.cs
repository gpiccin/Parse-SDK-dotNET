using System;
using System.Collections.Generic;
using System.Text;

namespace Parse
{
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class ParseKeyAttribute : Attribute
    {
    }
}
