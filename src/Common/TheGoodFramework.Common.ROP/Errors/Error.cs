using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TGF.Common.ROP.Errors
{
    /// <summary>
    /// Common iterface for Error and possible future different types of errors.
    /// </summary>
    public interface IError
    {
        string Code { get; }
        string Message { get; }
    }

    /// <summary>
    /// Struct representing an error with an error Code and error Message.
    /// </summary>
    public readonly struct Error : IError
    {
        public string Code { get; }
        public string Message { get; }
        public Error(string aCode, string aMessage)
        {
            Code = aCode;
            Message = aMessage;
        }
    }
}
