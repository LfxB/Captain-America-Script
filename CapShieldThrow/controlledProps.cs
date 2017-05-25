using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GTA; // This is a reference that is needed! do not edit this
using GTA.Native; // This is a reference that is needed! do not edit this
using GTA.Math;

namespace CapShieldThrow
{
    class controlledProps
    {
        public Entity _prop { get; set; }
        public Vector3 _direction { get; set; }
        public int _ElapsedTime { get; set; }
    }
}
