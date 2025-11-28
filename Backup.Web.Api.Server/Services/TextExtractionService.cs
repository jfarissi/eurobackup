using System.Text;
using System.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Backup.Web.Api.Server.Services
{
    public interface ITextExtractionService
    {
        Task<string> ExtractTextAsync(string absoluteFilePath, CancellationToken cancellationToken = default);
    }

    public class TextExtractionService : ITextExtractionService
    {
        public Task<string> ExtractTextAsync(string absoluteFilePath, CancellationToken cancellationToken = default)
        {
            try
            {
                if (Path.GetExtension(absoluteFilePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    var sb = new StringBuilder();
                    using var document = PdfDocument.Open(absoluteFilePath);
                    foreach (var page in document.GetPages())
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        var words = page.GetWords()?.ToList();
                        if (words != null && words.Count > 0)
                        {
                            foreach (var line in GroupWordsIntoLines(words))
                            {
                                // join words with single space to avoid gluing
                                sb.AppendLine(string.Join(" ", line.Select(w => w.Text)));
                            }
                        }
                        else
                        {
                            // fallback
                            sb.AppendLine(page.Text);
                        }
                    }
                    return Task.FromResult(sb.ToString());
                }

                // Fallback: not a PDF, try to read as text
                var text = File.ReadAllText(absoluteFilePath);
                return Task.FromResult(text);
            }
            catch
            {
                return Task.FromResult(string.Empty);
            }
        }
        private static IReadOnlyList<IReadOnlyList<Word>> GroupWordsIntoLines(IList<Word> words)
        {
            var lines = new List<LineBucket>();
            const double yTolerance = 2.5; // PDF units; small tolerance to group same baseline

            foreach (var word in words)
            {
                var y = word.BoundingBox.Bottom;
                LineBucket? target = null;
                foreach (var line in lines)
                {
                    if (Math.Abs(line.Y - y) <= yTolerance)
                    {
                        target = line; break;
                    }
                }
                if (target == null)
                {
                    target = new LineBucket { Y = y };
                    lines.Add(target);
                }
                target.Words.Add(word);
                // optional: keep Y as running average to stabilize
                target.Y = (target.Y * (target.Words.Count - 1) + y) / target.Words.Count;
            }

            // Sort lines from top to bottom (higher Y first in PDF coordinate space)
            lines.Sort((a, b) => b.Y.CompareTo(a.Y));

            // Sort words left-to-right within each line
            var result = new List<IReadOnlyList<Word>>(lines.Count);
            foreach (var line in lines)
            {
                line.Words.Sort((a, b) => a.BoundingBox.Left.CompareTo(b.BoundingBox.Left));
                result.Add(line.Words);
            }
            return result;
        }

        private sealed class LineBucket
        {
            public double Y { get; set; }
            public List<Word> Words { get; } = new List<Word>();
        }
    }
}


