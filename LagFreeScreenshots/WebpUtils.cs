using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using WebPWrapper;

namespace LagFreeScreenshots
{
    public static class WebpUtils
    {
        internal static byte[] ProduceXmpDescription(string description)
        {
            var s = @"
<?xpacket begin='?' id='W5M0MpCehiHzreSzNTczkc9d'?>
<x:xmpmeta xmlns:x='adobe:ns:meta/' x:xmptk='Adobe XMP Core 5.4-c002 1.000000, 0000/00/00-00:00:00'>
   <rdf:RDF xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'>
<rdf:Description rdf:about=''
  xmlns:exif='http://ns.adobe.com/exif/1.0/'>
  <exif:UserComment>"+ description +@"</exif:UserComment>
";
            // TODO: do we need to xml-encode description here?
            return Encoding.UTF8.GetBytes(s);
        }

        public static bool IsWebpSupported()
        {
            try
            {
                return Assembly.Load("libwebpwrapper") != null;
            }
            catch (Exception) { }
            return false;
        }

        public static void SaveToFile(string filePath, Bitmap bitmap, string description, int compression)
        {
            using WebP webp = new();
            var rawXmp = ProduceXmpDescription(description);
            webp.EncodeWithMeta(bitmap, filePath, rawXmp, quality: compression, speed: 0, multithreads: true);
        }
    }
}