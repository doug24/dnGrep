using UglyToad.PdfPig.Content;

namespace dnGREP.Engines.PdfPig
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
