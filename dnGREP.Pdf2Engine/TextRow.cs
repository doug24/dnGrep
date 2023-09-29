using System;
using System.Collections.Generic;
using System.Linq;
using UglyToad.PdfPig.Content;

namespace dnGREP.Engines.Pdf2
{
    internal class TextRow : IEquatable<TextRow>
    {
        private readonly List<Letter> letters = new();

        public TextRow(Letter letter) 
        {
            letters.Add(letter);
            Baseline = letter.StartBaseLine.Y;
            // sometimes the letter height is 0
            Topline = letter.GlyphRectangle.Height > 0 ?
                letter.GlyphRectangle.Top : 
                letter.StartBaseLine.Y + letter.PointSize * 0.75;
        }

        public double Baseline { get; private set; }

        public double Topline { get; private set; }

        public void AddLetter(Letter letter)
        {
            letters.Add(letter);

            if (letter.StartBaseLine.Y < Baseline)
            {
                Baseline = letter.StartBaseLine.Y;
            }
            if (letter.GlyphRectangle.Top > Topline)
            {
                Topline = letter.GlyphRectangle.Top;
            }
        }

        public override int GetHashCode()
        {
            return Baseline.GetHashCode();
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as TextRow);
        }

        public bool Equals(TextRow? other)
        {
            if (other == null) return false;
            return other.Baseline == Baseline;
        }

        public IReadOnlyCollection<Letter> Letters
        {
            get
            {
                letters.Sort((a, b) => a.Location.X.CompareTo(b.Location.X));
                return letters;
            }
        }

        public override string ToString()
        {
            var text = string.Join("", letters.Select(l => l.Value));
            return $"{Baseline} {text}";
        }
    }

}
