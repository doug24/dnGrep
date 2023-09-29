using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UglyToad.PdfPig.Content;

namespace dnGREP.Engines.Pdf2
{
    internal static class Extensions
    {
        public static bool Intersects(this Letter letter, TextRow row)
        {
            double top = letter.GlyphRectangle.Height > 0 ? letter.GlyphRectangle.Top :
                letter.Location.Y + letter.PointSize * 0.75;

            return (top > row.Baseline && letter.StartBaseLine.Y <= row.Baseline) ||
                row.Topline > letter.StartBaseLine.Y && row.Baseline <= letter.StartBaseLine.Y;
        }
    }
}
