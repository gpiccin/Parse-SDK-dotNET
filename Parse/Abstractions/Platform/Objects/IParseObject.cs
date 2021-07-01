using System;
using System.Collections.Generic;
using System.Text;

namespace Parse.Abstractions.Platform.Objects
{
    /// <summary>
    /// Define that the class could be converted as a Parse Object.
    /// </summary>
    public interface IParseObject
    {
        public void SetParseClient(ParseClient parseClient);

        public ParseClient GetParseClient();
    }
}
