using System;

namespace Plugin.GoogleClient
{
    /// <summary>
    /// Cross GoogleClient
    /// </summary>
    public static class CrossGoogleClient
    {
        public static readonly Lazy<IGoogleClientManager> Implementation = new(CreateGoogleClient, System.Threading.LazyThreadSafetyMode.PublicationOnly);

        /// <summary>
        /// Current plugin implementation to use
        /// </summary>
        public static IGoogleClientManager Current
        {
            get
            {
                var ret = Implementation.Value;
                if (ret == null)
                {
                    throw NotImplementedInReferenceAssembly();
                }


                return ret;
            }
        }

        public static IGoogleClientManager CreateGoogleClient()
        {
#if  NETSTANDARD2_0
            return null;
#else
            return new GoogleClientManager();
#endif
        }

        internal static Exception NotImplementedInReferenceAssembly() =>
            new NotImplementedException("This functionality is not implemented in the portable version of this assembly.  You should reference the NuGet package from your main application project in order to reference the platform-specific implementation.");

    }
}
