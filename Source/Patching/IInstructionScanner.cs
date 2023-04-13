using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace RimThreaded.Patching
{
    public interface IInstructionScanner
    {
        IEnumerable<int> ScanMethod(MethodBase method);
        bool IsInstructionTargeted(OpCode opcode, object operand);
    }
}
