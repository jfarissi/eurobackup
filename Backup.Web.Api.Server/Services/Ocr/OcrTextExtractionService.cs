using System.Text;
using System.Collections.Generic;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using Tesseract;
using System.Linq;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Backup.Web.Api.Server.Services.Ocr
{
    public interface IOcrTextExtractionService
    {
        Task<string> ExtractTextFromScannedPdfAsync(string absoluteFilePath, string language, int dpi = 300, CancellationToken ct = default);
    }

    public class OcrTextExtractionService : IOcrTextExtractionService
    {
        private readonly IConfiguration configuration;

        public OcrTextExtractionService(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public Task<string> ExtractTextFromScannedPdfAsync(string absoluteFilePath, string language, int dpi = 300, CancellationToken ct = default)
        {
            var sb = new StringBuilder();
            dpi = this.configuration.GetValue<int?>("Ocr:Dpi") ?? dpi;
            var configuredPath = this.configuration.GetValue<string>("Ocr:TessdataPath") ?? string.Empty;
            string tessdataDir;
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                var last = Path.GetFileName(configuredPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                tessdataDir = string.Equals(last, "tessdata", StringComparison.OrdinalIgnoreCase)
                    ? configuredPath
                    : Path.Combine(configuredPath, "tessdata");
            }
            else
            {
                tessdataDir = Path.Combine(AppContext.BaseDirectory, "tessdata");
            }

            // Ensure Tesseract can locate tessdata (point directly to tessdata)
            try { Environment.SetEnvironmentVariable("TESSDATA_PREFIX", tessdataDir); } catch { }

            // Build languages to try: configured combo, then split tokens, then common fallbacks
            var langCode = string.IsNullOrWhiteSpace(language) ? "eng" : language;
            var langsToTry = new List<string>();
            langsToTry.Add(langCode);
            foreach (var token in langCode.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                langsToTry.Add(token);
            foreach (var fallback in new[] { "eng", "fra", "nld" })
                langsToTry.Add(fallback);
            langsToTry = langsToTry.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // Quick preflight: at least one traineddata exists
            bool anyLangAvailable = Directory.Exists(tessdataDir) && langsToTry.Any(l => File.Exists(Path.Combine(tessdataDir, l + ".traineddata")));
            if (!anyLangAvailable) return Task.FromResult(string.Empty);

            var parsingOptions = new ParsingOptions()
            {
                UseLenientParsing = true,
                SkipMissingFonts = true,
                FilterProvider = MyFilterProvider.Instance
            };

            using var document = PdfDocument.Open(absoluteFilePath, parsingOptions);
            int pageIndex = 0;
            foreach (var page in document.GetPages())
            {
                if (ct.IsCancellationRequested) break;
                foreach (var pdfImage in page.GetImages())
                {
                    if (ct.IsCancellationRequested) break;
                    if (pdfImage.TryGetPng(out var bytes))
                    {
                        using var pix = CreatePixFromPngBytesWithEnhancement(bytes);
                        var text = ProcessPixWithLanguages(pix, tessdataDir, dpi, langsToTry);
                        if (!string.IsNullOrWhiteSpace(text)) sb.AppendLine(text);
                    }
                    else if (pdfImage.RawBytes != null && pdfImage.RawBytes.Length > 0)
                    {
                        // RawBytes may be a valid JPEG; try OCR directly from bytes
                        var raw = pdfImage.RawBytes.ToArray();
                        using var pix = CreatePixFromImageBytesWithEnhancement(raw);
                        var text = ProcessPixWithLanguages(pix, tessdataDir, dpi, langsToTry);
                        if (!string.IsNullOrWhiteSpace(text)) sb.AppendLine(text);
                    }
                }

                // Fallback: If no text extracted from images on this page, render full page and OCR
                if (sb.Length == 0)
                {
                    try
                    {
                        using var docLib = Docnet.Core.DocLib.Instance;
                        using var docReader = docLib.GetDocReader(absoluteFilePath, new Docnet.Core.Models.PageDimensions(dpi, dpi));
                        using var pageReader = docReader.GetPageReader(pageIndex);
                        int width = pageReader.GetPageWidth();
                        int height = pageReader.GetPageHeight();
                        var rawBytes = pageReader.GetImage(); // BGRA32

                        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                        var data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                        try
                        {
                            Marshal.Copy(rawBytes, 0, data.Scan0, rawBytes.Length);
                        }
                        finally
                        {
                            bmp.UnlockBits(data);
                        }

                        using var enhanced = EnhanceForOcr(bmp);
                        using var ms = new MemoryStream();
                        enhanced.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        var pngBytes = ms.ToArray();
                        using var pix = Pix.LoadFromMemory(pngBytes);
                        var text = ProcessPixWithLanguages(pix, tessdataDir, dpi, langsToTry);
                        if (!string.IsNullOrWhiteSpace(text)) sb.AppendLine(text);
                    }
                    catch { }
                }

                pageIndex++;
            }

            return Task.FromResult(sb.ToString());
        }

        private static string ProcessPixWithLanguages(Pix pix, string tessdataDir, int dpi, List<string> langsToTry)
        {
            string bestText = string.Empty;
            foreach (var lang in langsToTry)
            {
                if (!File.Exists(Path.Combine(tessdataDir, lang + ".traineddata"))) continue;
                try
                {
                    using var engine = new TesseractEngine(tessdataDir, lang, EngineMode.LstmOnly);
                    try { engine.SetVariable("user_defined_dpi", (dpi > 0 ? dpi : 300).ToString()); } catch { }
                    try { engine.SetVariable("preserve_interword_spaces", "1"); } catch { }

                    string text = string.Empty;
                    using (var p1 = engine.Process(pix, PageSegMode.Auto)) text = p1.GetText();
                    if (string.IsNullOrWhiteSpace(text)) { using var p2 = engine.Process(pix, PageSegMode.SparseTextOsd); text = p2.GetText(); }
                    if (string.IsNullOrWhiteSpace(text)) { using var p3 = engine.Process(pix, PageSegMode.SingleBlock); text = p3.GetText(); }

                    if (!string.IsNullOrWhiteSpace(text) && text.Length > bestText.Length)
                    {
                        bestText = text;
                    }
                }
                catch { }
            }
            return bestText;
        }
        private static Pix CreatePixFromPngBytesWithEnhancement(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            using var bmp = new Bitmap(ms);
            using var enhanced = EnhanceForOcr(bmp);
            using var outMs = new MemoryStream();
            enhanced.Save(outMs, System.Drawing.Imaging.ImageFormat.Png);
            var png = outMs.ToArray();
            return Pix.LoadFromMemory(png);
        }

        private static Pix CreatePixFromImageBytesWithEnhancement(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            using var bmp = new Bitmap(ms);
            using var enhanced = EnhanceForOcr(bmp);
            using var outMs = new MemoryStream();
            enhanced.Save(outMs, System.Drawing.Imaging.ImageFormat.Png);
            var png = outMs.ToArray();
            return Pix.LoadFromMemory(png);
        }

        private static Bitmap EnhanceForOcr(Bitmap input)
        {
            // Convert to grayscale and apply simple global threshold
            var width = input.Width;
            var height = input.Height;
            var output = new Bitmap(width, height, PixelFormat.Format24bppRgb);

            using (var g = Graphics.FromImage(output))
            {
                g.DrawImage(input, 0, 0, width, height);
            }

            var data = output.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            try
            {
                int stride = data.Stride;
                int bytes = Math.Abs(stride) * height;
                byte[] buffer = new byte[bytes];
                Marshal.Copy(data.Scan0, buffer, 0, bytes);

                // Build grayscale histogram for Otsu thresholding
                int[] hist = new int[256];
                for (int y = 0; y < height; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        int i = row + x * 3;
                        byte b = buffer[i + 0];
                        byte g = buffer[i + 1];
                        byte r = buffer[i + 2];
                        byte gray = (byte)((r * 299 + g * 587 + b * 114) / 1000);
                        hist[gray]++;
                    }
                }

                int total = width * height;
                long sumAll = 0;
                for (int t = 0; t < 256; t++) sumAll += t * (long)hist[t];
                long sumB = 0;
                int wB = 0;
                double maxVar = -1;
                int threshold = 128;
                for (int t = 0; t < 256; t++)
                {
                    wB += hist[t];
                    if (wB == 0) continue;
                    int wF = total - wB;
                    if (wF == 0) break;
                    sumB += t * (long)hist[t];
                    double mB = sumB / (double)wB;
                    double mF = (sumAll - sumB) / (double)wF;
                    double varBetween = wB * (double)wF * (mB - mF) * (mB - mF);
                    if (varBetween > maxVar)
                    {
                        maxVar = varBetween;
                        threshold = t;
                    }
                }

                // apply threshold and compute black pixel ratio
                int blackCount = 0;
                for (int y = 0; y < height; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        int i = row + x * 3;
                        byte b = buffer[i + 0];
                        byte g2 = buffer[i + 1];
                        byte r = buffer[i + 2];
                        byte gray = (byte)((r * 299 + g2 * 587 + b * 114) / 1000);
                        byte v = gray > threshold ? (byte)255 : (byte)0;
                        buffer[i + 0] = v;
                        buffer[i + 1] = v;
                        buffer[i + 2] = v;
                        if (v == 0) blackCount++;
                    }
                }

                Marshal.Copy(buffer, 0, data.Scan0, bytes);
            }
            finally
            {
                output.UnlockBits(data);
            }

            // Invert if mostly inverted (white text on black background)
            double blackRatio = 0.0;
            try
            {
                blackRatio = GetBlackRatio(output);
            }
            catch { }
            if (blackRatio > 0.9)
            {
                InvertBitmap(output);
            }

            return output;
        }

        private static double GetBlackRatio(Bitmap bmp)
        {
            int width = bmp.Width;
            int height = bmp.Height;
            var data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                int stride = data.Stride;
                int bytes = Math.Abs(stride) * height;
                byte[] buffer = new byte[bytes];
                Marshal.Copy(data.Scan0, buffer, 0, bytes);
                int black = 0;
                for (int y = 0; y < height; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        int i = row + x * 3;
                        byte v = buffer[i]; // grayscale binary image (B=G=R)
                        if (v == 0) black++;
                    }
                }
                return black / (double)(width * height);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        private static void InvertBitmap(Bitmap bmp)
        {
            int width = bmp.Width;
            int height = bmp.Height;
            var data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            try
            {
                int stride = data.Stride;
                int bytes = Math.Abs(stride) * height;
                byte[] buffer = new byte[bytes];
                Marshal.Copy(data.Scan0, buffer, 0, bytes);
                for (int i = 0; i < buffer.Length; i++)
                {
                    buffer[i] = (byte)(255 - buffer[i]);
                }
                Marshal.Copy(buffer, 0, data.Scan0, bytes);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }
    }
}


