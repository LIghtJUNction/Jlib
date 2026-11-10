using System;

namespace Jlib.Multicomponent {
    internal static class BreathableGas {
        internal static float Mass(MulticomponentCellSnapshot snapshot) {
            float mass = 0f;
            for (int i = 0; i < snapshot.ComponentCount; i++) {
                var item = snapshot.Components[i];
                if (item.Phase == MulticomponentPhase.Gas && item.Element != null
                    && (item.Element.id == SimHashes.Oxygen || item.Element.id == SimHashes.ContaminatedOxygen))
                    mass += item.Mass;
            }
            return mass;
        }
    }
}
