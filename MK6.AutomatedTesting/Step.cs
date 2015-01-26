using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MK6.AutomatedTesting
{
    public abstract class Step
    {
        public delegate void RunStepDelegate(
            IterationContext context,
            Step step,
            CancellationToken cancellationToken);

        public abstract RunStepDelegate Runner { get; }
    }
}
