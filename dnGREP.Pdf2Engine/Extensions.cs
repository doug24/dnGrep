using UglyToad.PdfPig.Content;

namespace dnGREP.Engines.Pdf2
{
    internal static class Extensions
    {
        public static bool Intersects(this Letter letter, TextRow row)
        {
            return (letter.StartBaseLine.Y > row.BaselineMin && letter.StartBaseLine.Y <= row.BaselineMax);// ||
                //row.BaselineMax > letter.StartBaseLine.Y && row.BaselineMin <= letter.StartBaseLine.Y;
        }
    }
}
