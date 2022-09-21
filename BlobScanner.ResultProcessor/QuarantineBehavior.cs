using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlobScanner.ResultProcessor
{
    public enum QuarantineBehavior
    {
        LogOnly = 0,
        DeleteOnly = 1,
        Move = 2
    }
}
