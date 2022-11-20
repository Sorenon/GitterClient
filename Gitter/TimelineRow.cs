using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gitter
{
    internal struct TimelineRow
    {
        public Commit? commit;
        public bool ty;
    }
}
