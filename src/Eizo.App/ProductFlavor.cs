namespace Eizo;

internal static class ProductFlavor
{
#if EIZO_DEMO
    public const bool IsDemo = true;
    public const string ProductName = "Eizo Demo";
    public const string WindowTitle = "Eizo Demo · 映藏";
    public const string AppTitleText = "Eizo Demo  映藏";
    public const string InstanceKey = "Eizo.Demo.Main";
    public const string ProtocolScheme = "eizo-demo";
#else
    public const bool IsDemo = false;
    public const string ProductName = "Eizo";
    public const string WindowTitle = "Eizo 映藏";
    public const string AppTitleText = "Eizo  映藏";
    public const string InstanceKey = "Eizo.Main";
    public const string ProtocolScheme = "eizo";
#endif
}
