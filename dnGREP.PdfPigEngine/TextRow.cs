using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace dnGREP.Engines.PdfPig
{
    internal class TextRow : IEquatable<TextRow>
    {
        private readonly List<Letter> letters = new();

        public TextRow(Letter letter)
        {
            letters.Add(letter);
            BaselineMin = letter.StartBaseLine.Y - letter.PointSize * 0.5;
            BaselineMax = letter.StartBaseLine.Y + letter.PointSize * 0.5;
        }

        public double BaselineMin { get; private set; }

        public double BaselineMax { get; private set; }

        public void AddLetter(Letter letter)
        {
            letters.Add(letter);
        }

        public IReadOnlyList<Letter> Letters => letters;

        public void SortLetters()
        {
            List<Letter> sortedLetters = new();
            foreach (var word in NearestNeighbourWordExtractor.Instance.GetWords(letters)
                .OrderBy(w => w.Letters[0].Location.X))
            {
                foreach (Letter letter in word.Letters)
                {
                    sortedLetters.Add(letter);
                }
            }

            letters.Clear();
            letters.AddRange(sortedLetters);

            //letters.Sort((a, b) => a.Location.X.CompareTo(b.Location.X));
        }

        public override int GetHashCode()
        {
            return BaselineMin.GetHashCode();
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as TextRow);
        }

        public bool Equals(TextRow? other)
        {
            if (other == null) return false;
            return other.BaselineMin == BaselineMin;
        }

        public override string ToString()
        {
            var text = string.Join("", letters.Select(l => l.Value));
            return $"{BaselineMin} - {BaselineMax} : {text}";
        }
    }

}
