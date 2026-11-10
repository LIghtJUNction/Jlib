using Jlib.Multicomponent;

internal static class RenderCellCacheCoverage {
    private sealed class Provider : IMulticomponentCellProvider, IMulticomponentCellBatchProvider {
        internal int Reads, Copies, Disposals, LastCount;
        public bool IsReady => true;
        public bool TryGetCell(int cell, out MulticomponentCellSnapshot snapshot) {
            Reads++; snapshot=default; return true;
        }
        public bool TryGetCellBatch(int first, int count, out IMulticomponentCellBatch batch) {
            Copies++; LastCount=count; batch=new Batch(this,first,count); return true;
        }
        private sealed class Batch(Provider owner, int first, int count) : IMulticomponentCellBatch {
            public bool TryGetCell(int cell, out MulticomponentCellSnapshot snapshot) {
                snapshot=default; return cell>=first && cell<first+count;
            }
            public void Dispose() { owner.Disposals++; }
        }
    }
    internal static void Run() {
        var provider=new Provider(); var view=new object();
        Program.Check(MulticomponentCellAccess.RegisterProvider(provider), "render provider registers");
        using (var cache=new RenderCellCache()) {
            for (int i=0;i<128;i++) Program.Check(cache.TryGetCell(i,130,1,view,out _), "render cell readable");
            Program.Equal(1,provider.Copies,"128 adjacent cells use one native copy");
            Program.Equal(0,provider.Reads,"batch avoids individual native calls");
            cache.TryGetCell(129,130,1,view,out _);
            Program.Equal(2,provider.LastCount,"last batch stays within grid");
            Program.Equal(1,provider.Disposals,"old batch released at boundary");
            cache.TryGetCell(129,130,2,view,out _);
            Program.Equal(3,provider.Copies,"new frame refreshes copied data");
            MulticomponentCellAccess.Enabled=false;
            Program.Check(!cache.TryGetCell(129,130,2,view,out _),"disabled provider cannot reuse cached data");
            MulticomponentCellAccess.Enabled=true;
        }
        Program.Equal(3,provider.Disposals,"every retained batch released");
        MulticomponentCellAccess.UnregisterProvider(provider);
    }
}
