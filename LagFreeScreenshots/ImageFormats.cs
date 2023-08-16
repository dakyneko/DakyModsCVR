using System.ComponentModel;

namespace LagFreeScreenshots;

internal enum ImageFormats
{
    [Description("Auto")]
    auto,
        
    [Description("WebP")]
    webp,
        
    [Description("PNG")]
    png,
        
    [Description("jpeg")]
    jpeg,
}
