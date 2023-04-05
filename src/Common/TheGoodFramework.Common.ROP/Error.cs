using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TGF.Common.ROP
{
    /// <summary>
    /// Struct representing an error with an error Code and error Message.
    /// </summary>
    public struct Error
    {
        public string Code;
        public string Message;
        public Error(string aCode, string aMessage)
        {
            Code = aCode;
            Message = aMessage;
        }
    }
}
