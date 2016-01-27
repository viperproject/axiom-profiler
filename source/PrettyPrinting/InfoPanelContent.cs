using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Z3AxiomProfiler.PrettyPrinting
{
    public class InfoPanelContent
    {
        private readonly StringBuilder textBuilder = new StringBuilder();
        private readonly List<TextHighlightFormat> highlights = new List<TextHighlightFormat>(); 
        private string finalText;
        private bool finalized;


        public void finalize()
        {
            finalText = textBuilder.ToString();
            finalized = true;
        }
    }

    public class TextHighlightFormat
    {
        public static readonly Font DefaultFont = new Font("Consolas", 9, FontStyle.Regular);
        public static readonly Font TitleFont = new Font("Consolas", 15, FontStyle.Underline);
        public static readonly Font BoldFont = new Font(DefaultFont, FontStyle.Bold);

        public readonly int startIdx;
        public readonly Font highlightFont;
        public readonly Color highlightColor;

        public int length { get; private set; }

        public TextHighlightFormat(int startIndex, Color color, Font font)
        {
            startIdx = startIndex;
            highlightColor = color;
            highlightFont = font;
        }

        public static TextHighlightFormat defaultFormat(int startIndex)
        {
            return new TextHighlightFormat(startIndex, Color.Black, DefaultFont);
        }
    }
}