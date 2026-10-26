namespace Jlib.Runtime {
    /// <summary>Stable identity inspected by consumer bootstraps before loading their implementation.</summary>
    public static class JlibApi {
        public const int AbiVersion = 1;
        public const string StaticId = "LIghtJUNction.Jlib";
        public const string Version = "0.1.0";
        public const string PLibVersion = "4.25.0.0";

        /// <summary>Registers the consumer before its implementation OnLoad is called.</summary>
        public static void RegisterConsumer(System.Reflection.Assembly implementation, KMod.Mod owner) {
            PeterHan.PLib.Core.PUtil.RegisterModAssembly(implementation, owner);
        }
    }
}
