using System;

namespace Clarenza
{
    [Serializable]
    public sealed class CircuitState
    {
        public bool battery;
        public bool lamp;
        public bool closed;
        public bool observedLight;
        public bool observedOpen;
        public bool Powered { get { return battery && lamp && closed; } }
        public bool Complete { get { return observedLight && observedOpen; } }

        public void Observe()
        {
            if (Powered) observedLight = true;
            if (observedLight && battery && lamp && !closed) observedOpen = true;
        }
        public void Reset()
        {
            battery = lamp = closed = observedLight = observedOpen = false;
        }
    }
}
