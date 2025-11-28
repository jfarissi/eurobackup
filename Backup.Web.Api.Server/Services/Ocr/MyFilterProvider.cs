using System.Collections.Generic;
using UglyToad.PdfPig.Filters;
using UglyToad.PdfPig.Filters.Dct.JpegLibrary;
using UglyToad.PdfPig.Filters.Jbig2.PdfboxJbig2;
using UglyToad.PdfPig.Tokens;

namespace Backup.Web.Api.Server.Services.Ocr
{
    public sealed class MyFilterProvider : BaseFilterProvider
    {
        public static readonly MyFilterProvider Instance = new MyFilterProvider();

        private MyFilterProvider() : base(GetDictionary()) { }

        private static Dictionary<string, IFilter> GetDictionary()
        {
            var dct = new JpegLibraryDctDecodeFilter();
            var jbig2 = new PdfboxJbig2DecodeFilter();

            var ascii85 = new Ascii85Filter();
            var asciiHex = new AsciiHexDecodeFilter();
            var ccitt = new CcittFaxDecodeFilter();
            var flate = new FlateFilter();
            var runLength = new RunLengthFilter();
            var lzw = new LzwFilter();

            return new Dictionary<string, IFilter>
            {
                { NameToken.Ascii85Decode.Data, ascii85 },
                { NameToken.Ascii85DecodeAbbreviation.Data, ascii85 },
                { NameToken.AsciiHexDecode.Data, asciiHex },
                { NameToken.AsciiHexDecodeAbbreviation.Data, asciiHex },
                { NameToken.CcittfaxDecode.Data, ccitt },
                { NameToken.CcittfaxDecodeAbbreviation.Data, ccitt },
                { NameToken.DctDecode.Data, dct },
                { NameToken.DctDecodeAbbreviation.Data, dct },
                { NameToken.FlateDecode.Data, flate },
                { NameToken.FlateDecodeAbbreviation.Data, flate },
                { NameToken.Jbig2Decode.Data, jbig2 },
                // JPX filter removed due to runtime TypeLoad issues; rely on page render fallback
                { NameToken.RunLengthDecode.Data, runLength },
                { NameToken.RunLengthDecodeAbbreviation.Data, runLength },
                { NameToken.LzwDecode.Data, lzw },
                { NameToken.LzwDecodeAbbreviation.Data, lzw }
            };
        }
    }
}


